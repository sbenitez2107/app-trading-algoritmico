import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FtmoSimulationModalComponent } from './ftmo-simulation-modal.component';
import { API_BASE_URL } from '../../../app.config';
import {
  FtmoChainOutcome,
  FtmoMultiStartSummaryDto,
  FtmoSimulationStatus,
  IMOX_RETESTER_LOT_GRID,
} from '../../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

const API_URL = 'http://localhost/api-test';

/** Collapses template whitespace so the assertion reads the text a user sees. */
function visibleText(el: HTMLElement): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('FtmoSimulationModalComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoSimulationModalComponent, TranslateModule.forRoot(), HttpClientTestingModule],
      providers: [{ provide: API_BASE_URL, useValue: API_URL }],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function create(
    overrides: {
      strategyId?: string;
      strategyName?: string;
      symbol?: string | null;
    } = {},
  ): ComponentFixture<FtmoSimulationModalComponent> {
    const fixture = TestBed.createComponent(FtmoSimulationModalComponent);
    fixture.componentRef.setInput('strategyId', overrides.strategyId ?? 'strat-1');
    fixture.componentRef.setInput('strategyName', overrides.strategyName ?? 'My Strategy');
    fixture.componentRef.setInput('symbol', overrides.symbol ?? 'EURUSD');
    fixture.detectChanges();
    return fixture;
  }

  const flushUrl = `${API_URL}/api/strategies/strat-1/ftmo-breach/multi-start`;

  function expectOneRequest() {
    return httpMock.expectOne((req) => req.url === flushUrl);
  }

  // --- Phase 1d.1: form signals and canRun ---

  it('onOpen_PrefillsSourceGridFromConstant_BrokerToFtmo_AndInitialCapitalTo10000', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    expect(comp.sizeDecimals()).toBe(IMOX_RETESTER_LOT_GRID.sizeDecimals);
    expect(comp.step()).toBe(IMOX_RETESTER_LOT_GRID.step);
    expect(comp.minLot()).toBe(IMOX_RETESTER_LOT_GRID.minLot);
    expect(comp.maxLots()).toBe(IMOX_RETESTER_LOT_GRID.maxLots);
    expect(comp.brokerValue()).toBe('FTMO');
    expect(comp.sqxSymbol()).toBe('EURUSD');
    expect(comp.initialCapital()).toBe(10000);
    expect(comp.targetRiskPerTrade()).toBeNull();
    expect(comp.fxLow()).toBeNull();
    expect(comp.fxHigh()).toBeNull();
  });

  it('sourceGridFields_StayEditableAfterPrefill', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.maxLots.set(500);
    fixture.detectChanges();
    expect(comp.maxLots()).toBe(500);
    comp.targetRiskPerTrade.set(0.5);
    fixture.detectChanges();
    expect(comp.canRun()).toBe(true);
  });

  it('canRun_IsFalseWhileTargetRiskPerTradeIsEmpty', () => {
    const fixture = create();
    expect(fixture.componentInstance.canRun()).toBe(false);
  });

  it('canRun_IsTrueOnceAll8RequiredFieldsAreFilled_FxLowHighLeftEmpty', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.targetRiskPerTrade.set(0.5);
    fixture.detectChanges();
    expect(comp.canRun()).toBe(true);
    expect(comp.fxLow()).toBeNull();
    expect(comp.fxHigh()).toBeNull();
  });

  it('canRun_IsNotDisabledBySizeDecimalsEqualToZero', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.targetRiskPerTrade.set(0.5);
    comp.sizeDecimals.set(0);
    fixture.detectChanges();
    expect(comp.canRun()).toBe(true);
  });

  it('falsification_ATruthySizeDecimalsCheckWouldDisableRunAtZero', () => {
    // Falsification for hard rule 2 / task 1d.1.7: prove the test above can fail before trusting it.
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.targetRiskPerTrade.set(0.5);
    comp.sizeDecimals.set(0);
    fixture.detectChanges();
    // The real `canRun` uses `Number.isFinite`, not truthiness — simulate the buggy truthy check here
    // to prove it would produce `false`, the same red the real implementation must never show.
    const buggyCanRun = !!comp.sizeDecimals();
    expect(buggyCanRun).toBe(false);
    expect(comp.canRun()).toBe(true);
  });

  // --- Phase 1d.2: run lifecycle ---

  function fillRequiredFields(comp: FtmoSimulationModalComponent): void {
    comp.targetRiskPerTrade.set(0.5);
  }

  it('editingAnInputAfterACompletedRun_DoesNotSendANewRequest', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    expectOneRequest().flush({ strategyId: 'strat-1', runs: [] });
    comp.maxLots.set(999);
    fixture.detectChanges();
    httpMock.verify();
  });

  it('aSecondRunActivationWhileOneInFlight_SendsNoSecondRequest_AndRunStaysDisabled', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    expect(comp.running()).toBe(true);
    expect(comp.canRun()).toBe(false);
    comp.run();
    const req = expectOneRequest();
    req.flush({ strategyId: 'strat-1', runs: [] });
    expect(comp.running()).toBe(false);
  });

  it('a400Response_ShowsAnExplicitError_ReEnablesRun_AndRendersNoPartialResult', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    const req = expectOneRequest();
    req.flush({ message: 'bad query' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(comp.error()).toEqual({ key: 'ERRORS.INVALID_QUERY', detail: 'bad query' });
    expect(comp.running()).toBe(false);
    expect(comp.result()).toBeNull();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.ftmo-sim__error')).toBeTruthy();
  });

  it('aNetworkFailure_ShowsAnExplicitError_AndReEnablesRun', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    const req = expectOneRequest();
    req.error(new ProgressEvent('error'));
    fixture.detectChanges();
    expect(comp.error()).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
    expect(comp.running()).toBe(false);
  });

  it('destroyingTheFixtureWhileARequestIsInFlight_CancelsTheRequest', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    expectOneRequest();
    fixture.destroy();
    httpMock.verify();
  });

  it('falsification_OmittingTheRunningGuardWouldSendASecondRequest', () => {
    // Falsification for hard rule 7 / task 1d.2.7: prove the guard is load-bearing.
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    const req = expectOneRequest();
    comp.run(); // the real guarded run() must be a no-op here
    req.flush({ strategyId: 'strat-1', runs: [] });
    // `afterEach`'s `httpMock.verify()` fails if a second, unguarded request was ever opened.
  });

  // --- Phase 1d.3: panel composition and layout ---

  it('aSuccessfulMultiStartResult_RendersTwoSeparatelyLabelledPanels_DeployThenEvaluation', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    expectOneRequest().flush({
      strategyId: 'strat-1',
      runs: [makeRunDto(BacktestRunKind.Evaluation), makeRunDto(BacktestRunKind.Deploy)],
    });
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const panels = host.querySelectorAll('app-ftmo-run-panel');
    expect(panels.length).toBe(2);
  });

  it('aMissingRunKind_RendersNoRunHeldInThatSlot_InsteadOfOmittingIt', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    fillRequiredFields(comp);
    fixture.detectChanges();
    comp.run();
    expectOneRequest().flush({
      strategyId: 'strat-1',
      runs: [makeRunDto(BacktestRunKind.Deploy)],
    });
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('app-ftmo-run-panel').length).toBe(1);
    expect(host.textContent).toContain('FTMO_SIMULATION.NO_RUN_HELD');
  });

  it('falsification_SkippingMissingKindsInsteadOfTwoFixedSlots', () => {
    // Falsification for task 1d.3.5: prove a "skip missing kinds" implementation would fail the
    // NO_RUN_HELD assertion above (only present, no missing-slot marker rendered).
    const runs = [makeRunDto(BacktestRunKind.Deploy)];
    const presentOnly = runs.map((r) => r.kind);
    expect(presentOnly).not.toContain(BacktestRunKind.Evaluation);
    // The real component still renders NO_RUN_HELD for the missing slot (proven above); a
    // present-only render would never contain that key.
  });

  it('theDisclosureBlockIsVisible_BeforeAnyRunHasCompleted', () => {
    const fixture = create();
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('FTMO_SIMULATION.DISCLOSURE_ALWAYS_VISIBLE');
  });

  describe('with the real en/es dictionaries', () => {
    // The REAL dictionaries (PR1c 1c.5 precedent), not `forRoot()` with no loader: only a real
    // dictionary exposes a `{{param}}` placeholder the template forgot to fill (or filled with
    // `null`), because a missing key renders the key itself and hides the leak.
    beforeEach(() => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('en', en);
      translate.setTranslation('es', es);
      translate.use('en');
    });

    function runAndFail(
      fixture: ComponentFixture<FtmoSimulationModalComponent>,
      fail: (req: ReturnType<typeof expectOneRequest>) => void,
    ): HTMLElement {
      const comp = fixture.componentInstance;
      fillRequiredFields(comp);
      fixture.detectChanges();
      comp.run();
      fail(expectOneRequest());
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    function errorText(host: HTMLElement): string {
      return visibleText(host.querySelector('.ftmo-sim__error') as HTMLElement);
    }

    it('a400WithAMessage_RendersTheInterpolatedDetail_InEnglish', () => {
      const host = runAndFail(create(), (req) =>
        req.flush({ message: 'X' }, { status: 400, statusText: 'Bad Request' }),
      );
      expect(errorText(host)).toBe('The request was rejected: X');
    });

    it('a400WithAMessage_RendersTheInterpolatedDetail_InSpanish', () => {
      TestBed.inject(TranslateService).use('es');
      const host = runAndFail(create(), (req) =>
        req.flush({ message: 'X' }, { status: 400, statusText: 'Bad Request' }),
      );
      expect(errorText(host)).toBe('La solicitud fue rechazada: X');
    });

    it('aNetworkFailure_RendersRequestFailedText_InEnglishAndSpanish', () => {
      const fixture = create();
      const host = runAndFail(fixture, (req) => req.error(new ProgressEvent('error')));
      expect(errorText(host)).toBe('The request could not be completed. Try again.');
      TestBed.inject(TranslateService).use('es');
      fixture.detectChanges();
      expect(errorText(host)).toBe('No se pudo completar la solicitud. Intente nuevamente.');
    });

    it('a400WithNoMessage_FallsBackToNotReported_NeverAPlaceholderOrNull', () => {
      const host = runAndFail(create(), (req) =>
        req.flush({}, { status: 400, statusText: 'Bad Request' }),
      );
      const text = errorText(host);
      expect(text).toBe('The request was rejected: Not reported');
      expect(text).not.toContain('{{');
      expect(text).not.toContain('null');
    });

    it('a400WithANullMessage_InSpanish_FallsBackToNoReportado', () => {
      TestBed.inject(TranslateService).use('es');
      const host = runAndFail(create(), (req) =>
        req.flush({ message: null }, { status: 400, statusText: 'Bad Request' }),
      );
      const text = errorText(host);
      expect(text).toBe('La solicitud fue rechazada: No reportado');
      expect(text).not.toContain('null');
    });

    for (const locale of ['en', 'es'] as const) {
      it(`anEvaluatedResultWithBothPanels_RendersNoPlaceholderAndNoRawKey_${locale}`, () => {
        TestBed.inject(TranslateService).use(locale);
        const fixture = create();
        const comp = fixture.componentInstance;
        fillRequiredFields(comp);
        fixture.detectChanges();
        comp.run();
        expectOneRequest().flush({
          strategyId: 'strat-1',
          runs: [
            makeEvaluatedRunDto(BacktestRunKind.Deploy),
            makeEvaluatedRunDto(BacktestRunKind.Evaluation),
          ],
        });
        fixture.detectChanges();
        const host = fixture.nativeElement as HTMLElement;
        expect(host.querySelectorAll('app-ftmo-run-panel').length).toBe(2);
        const text = visibleText(host);
        expect(text).not.toContain('{{');
        expect(text).not.toContain('FTMO_SIMULATION.');
        const dict = locale === 'en' ? en : es;
        expect(text).toContain(dict.FTMO_SIMULATION.MODAL_TITLE);
        expect(text).toContain(dict.FTMO_SIMULATION.DISCLOSURE_ALWAYS_VISIBLE);
      });
    }

    it('theFormLabels_RenderTheirEnglishText_WithNoRawKey', () => {
      const host = create().nativeElement as HTMLElement;
      const labels = Array.from(host.querySelectorAll('label, legend')).map((l) =>
        visibleText(l as HTMLElement),
      );
      expect(labels).toEqual([
        en.FTMO_SIMULATION.FIELDS.BROKER,
        en.FTMO_SIMULATION.FIELDS.SQX_SYMBOL,
        en.FTMO_SIMULATION.FIELDS.INITIAL_CAPITAL,
        en.FTMO_SIMULATION.FIELDS.TARGET_RISK_PER_TRADE,
        en.FTMO_SIMULATION.FIELDS.SOURCE_LOT_GRID_LABEL,
        en.FTMO_SIMULATION.FIELDS.SIZE_DECIMALS,
        en.FTMO_SIMULATION.FIELDS.STEP,
        en.FTMO_SIMULATION.FIELDS.MIN_LOT,
        en.FTMO_SIMULATION.FIELDS.MAX_LOTS,
        en.FTMO_SIMULATION.FIELDS.FX_LOW,
        en.FTMO_SIMULATION.FIELDS.FX_HIGH,
      ]);
      const text = visibleText(host);
      expect(text).toContain(en.FTMO_SIMULATION.RUN);
      expect(text).not.toContain('{{');
      expect(text).not.toContain('FTMO_SIMULATION.');
    });

    it('theRunningState_RendersRunningText_WithNoPlaceholderOrRawKey', () => {
      const fixture = create();
      const comp = fixture.componentInstance;
      fillRequiredFields(comp);
      fixture.detectChanges();
      comp.run();
      fixture.detectChanges();
      const host = fixture.nativeElement as HTMLElement;
      expect(visibleText(host.querySelector('.ftmo-sim__loading') as HTMLElement)).toBe(
        en.FTMO_SIMULATION.RUNNING,
      );
      expect(visibleText(host)).not.toContain('{{');
      expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
      expectOneRequest().flush({ strategyId: 'strat-1', runs: [] });
    });

    it('aMissingRunKind_RendersNoRunHeldText_InEnglishAndSpanish', () => {
      const fixture = create();
      const comp = fixture.componentInstance;
      fillRequiredFields(comp);
      fixture.detectChanges();
      comp.run();
      expectOneRequest().flush({
        strategyId: 'strat-1',
        runs: [makeEvaluatedRunDto(BacktestRunKind.Deploy)],
      });
      fixture.detectChanges();
      const host = fixture.nativeElement as HTMLElement;
      expect(visibleText(host.querySelector('.ftmo-sim__no-run') as HTMLElement)).toBe(
        en.FTMO_SIMULATION.NO_RUN_HELD,
      );
      TestBed.inject(TranslateService).use('es');
      fixture.detectChanges();
      expect(visibleText(host.querySelector('.ftmo-sim__no-run') as HTMLElement)).toBe(
        es.FTMO_SIMULATION.NO_RUN_HELD,
      );
      expect(visibleText(host)).not.toContain('{{');
      expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
    });
  });

  const EVALUATED_SUMMARY: FtmoMultiStartSummaryDto = {
    startCount: 10,
    outcomes: [
      { outcome: FtmoChainOutcome.Phase1Breached, isCensored: false, count: 2, share: 0.2 },
      {
        outcome: FtmoChainOutcome.Phase1UndecidedAtEndOfData,
        isCensored: false,
        count: 1,
        share: 0.1,
      },
      { outcome: FtmoChainOutcome.Phase2Breached, isCensored: false, count: 3, share: 0.3 },
      {
        outcome: FtmoChainOutcome.Phase2UndecidedAtEndOfData,
        isCensored: false,
        count: 2,
        share: 0.2,
      },
      { outcome: FtmoChainOutcome.FundedBreached, isCensored: false, count: 0, share: 0 },
      {
        outcome: FtmoChainOutcome.FundedNoBreachAtEndOfData,
        isCensored: false,
        count: 2,
        share: 0.2,
      },
    ],
    fxRoundingSensitiveCount: 0,
    daysToPhase1Target: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    daysToPhase2Target: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    daysToBothTargets: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    fundedDaysToBreachFromFundedStart: { n: 3, min: 10, q1: 12, median: 15, q3: 18, max: 20 },
    fundedDaysToBreachFromChainStart: { n: 3, min: 40, q1: 42, median: 45, q3: 48, max: 50 },
    censoredRunway: { n: 0, min: null, q1: null, median: null, q3: null, max: null },
  };

  function makeEvaluatedRunDto(kind: BacktestRunKind) {
    return {
      ...makeRunDto(kind),
      status: FtmoSimulationStatus.Evaluated,
      refusal: null,
      summary: EVALUATED_SUMMARY,
      monthsWithoutStart: ['2024-02', '2024-03'],
      start1DiffersFromSingleStartAnchor: true,
    };
  }

  // --- Post-PR1 fixes (2026-10-01) ---

  describe('post-PR1 fixes', () => {
    beforeEach(() => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('en', en);
      translate.setTranslation('es', es);
      translate.use('en');
    });

    function runWith(
      fixture: ComponentFixture<FtmoSimulationModalComponent>,
      runs: unknown[],
    ): HTMLElement {
      const comp = fixture.componentInstance;
      fillRequiredFields(comp);
      fixture.detectChanges();
      comp.run();
      expectOneRequest().flush({ strategyId: 'strat-1', runs });
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    // Fix 1: broker prefill is always FTMO (user decision 2026-10-01), still editable.
    it('broker_IsPrefilledWithFtmo_AndTheModalTakesNoBrokerInput', () => {
      const fixture = create();
      expect(fixture.componentInstance.brokerValue()).toBe('FTMO');
      const input = (fixture.nativeElement as HTMLElement).querySelector(
        '#ftmo-broker',
      ) as HTMLInputElement;
      expect(input).not.toBeNull();
    });

    it('broker_StaysEditable_AndTheTypedValueIsWhatTheRequestCarries', () => {
      const fixture = create();
      const comp = fixture.componentInstance;
      comp.brokerValue.set('Darwinex');
      fillRequiredFields(comp);
      fixture.detectChanges();
      comp.run();
      const req = expectOneRequest();
      expect(req.request.params.get('broker')).toBe('Darwinex');
      req.flush({ strategyId: 'strat-1', runs: [] });
    });

    it('falsification_TheDefaultBrokerIsTheConstant_NotAnAccountBrokerLikeDarwinex', () => {
      const fixture = create();
      expect(fixture.componentInstance.brokerValue()).not.toBe('Darwinex');
      expect(fixture.componentInstance.brokerValue()).toBe('FTMO');
    });

    // Fix 2: the refusal names the broker from the SUBMITTED query.
    it('limitsNotConfigured_NamesTheSubmittedBroker_InEnglishAndSpanish', () => {
      const fixture = create();
      fixture.componentInstance.brokerValue.set('Darwinex');
      const host = runWith(fixture, [
        { ...makeRunDto(BacktestRunKind.Deploy), refusal: 2 },
        { ...makeRunDto(BacktestRunKind.Evaluation), refusal: 2 },
      ]);
      const refusals = Array.from(host.querySelectorAll('.ftmo-run-panel__refusal')).map((e) =>
        visibleText(e as HTMLElement),
      );
      expect(refusals).toEqual([
        "The broker 'Darwinex' has no FTMO limits configured.",
        "The broker 'Darwinex' has no FTMO limits configured.",
      ]);
      TestBed.inject(TranslateService).use('es');
      fixture.detectChanges();
      expect(visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement)).toBe(
        "El broker 'Darwinex' no tiene límites de FTMO configurados.",
      );
      expect(visibleText(host)).not.toContain('{{');
      expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
    });

    it('productNotTwoStep_NamesTheSubmittedBroker_NotTheAccount', () => {
      const fixture = create();
      const host = runWith(fixture, [{ ...makeRunDto(BacktestRunKind.Deploy), refusal: 1 }]);
      expect(visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement)).toBe(
        "The broker 'FTMO' is not configured as a two-step challenge product.",
      );
    });

    it('falsification_TheRefusalFollowsTheSubmittedBroker_NotAHardcodedOne', () => {
      const fixture = create();
      fixture.componentInstance.brokerValue.set('Other Broker');
      const host = runWith(fixture, [{ ...makeRunDto(BacktestRunKind.Deploy), refusal: 2 }]);
      const text = visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement);
      expect(text).toContain("'Other Broker'");
      expect(text).not.toContain('Darwinex');
      expect(text).not.toContain('account');
    });

    it('theRefusalKeepsTheSubmittedBroker_WhenTheFieldIsEditedAfterTheRun', () => {
      const fixture = create();
      const host = runWith(fixture, [{ ...makeRunDto(BacktestRunKind.Deploy), refusal: 2 }]);
      fixture.componentInstance.brokerValue.set('Edited After');
      fixture.detectChanges();
      expect(visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement)).toContain(
        "'FTMO'",
      );
    });

    // Fix 4: zero runs => one explicit message; one missing kind => the existing slot marker.
    it('aResponseWithZeroRuns_RendersOneExplicitNoBacktestsMessage_InEnglishAndSpanish', () => {
      const fixture = create();
      const host = runWith(fixture, []);
      const messages = host.querySelectorAll('.ftmo-sim__no-backtests');
      expect(messages.length).toBe(1);
      expect(visibleText(messages[0] as HTMLElement)).toBe(
        'This strategy has no imported backtests. Import its Deploy and Evaluation backtests with the Import backtests action on the strategy row.',
      );
      expect(host.querySelectorAll('.ftmo-sim__no-run').length).toBe(0);
      expect(visibleText(host)).not.toContain(en.FTMO_SIMULATION.NO_RUN_HELD);
      TestBed.inject(TranslateService).use('es');
      fixture.detectChanges();
      expect(visibleText(host.querySelector('.ftmo-sim__no-backtests') as HTMLElement)).toBe(
        es.FTMO_SIMULATION.NO_BACKTESTS_IMPORTED,
      );
      expect(visibleText(host)).not.toContain('{{');
      expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
    });

    it('exactlyOneMissingKind_StillRendersNoRunHeld_AndNoNoBacktestsMessage', () => {
      const fixture = create();
      const host = runWith(fixture, [makeRunDto(BacktestRunKind.Deploy)]);
      expect(host.querySelectorAll('.ftmo-sim__no-run').length).toBe(1);
      expect(visibleText(host.querySelector('.ftmo-sim__no-run') as HTMLElement)).toBe(
        en.FTMO_SIMULATION.NO_RUN_HELD,
      );
      expect(host.querySelector('.ftmo-sim__no-backtests')).toBeNull();
    });

    it('falsification_BothKindsPresent_RendersNeitherMessage', () => {
      const fixture = create();
      const host = runWith(fixture, [
        makeRunDto(BacktestRunKind.Deploy),
        makeRunDto(BacktestRunKind.Evaluation),
      ]);
      expect(host.querySelector('.ftmo-sim__no-backtests')).toBeNull();
      expect(host.querySelector('.ftmo-sim__no-run')).toBeNull();
    });

    // Fix 5: server disclosure -> i18n, rendered once for the whole result (user decision 2026-10-03).
    for (const locale of ['en', 'es'] as const) {
      it(`theResultDisclosureAndNotModelledList_RenderOnceInTheRealDictionary_${locale}`, () => {
        TestBed.inject(TranslateService).use(locale);
        const dict = locale === 'en' ? en : es;
        const fixture = create();
        const run = (kind: BacktestRunKind) => ({
          ...makeEvaluatedRunDto(kind),
          notModelled: ['Swap', 'FtmoCommission', 'IntradayEquity'],
          disclosure: 'RAW SERVER DISCLOSURE',
        });
        const host = runWith(fixture, [
          run(BacktestRunKind.Deploy),
          run(BacktestRunKind.Evaluation),
        ]);
        const blocks = host.querySelectorAll('.ftmo-sim__result-disclosure');
        expect(blocks.length).toBe(1);
        expect(visibleText(blocks[0] as HTMLElement)).toBe(dict.FTMO_SIMULATION.DISCLOSURE_RESULT);
        const items = Array.from(host.querySelectorAll('.ftmo-sim__not-modelled li')).map((li) =>
          visibleText(li as HTMLElement),
        );
        expect(items).toEqual([
          dict.FTMO_SIMULATION.NOT_MODELLED.SWAP,
          dict.FTMO_SIMULATION.NOT_MODELLED.FTMO_COMMISSION,
          dict.FTMO_SIMULATION.NOT_MODELLED.INTRADAY_EQUITY,
        ]);
        expect(host.querySelectorAll('.ftmo-run-panel__disclosure').length).toBe(0);
        expect(host.querySelectorAll('.ftmo-run-panel__not-modelled').length).toBe(0);
        const text = visibleText(host);
        expect(text).not.toContain('RAW SERVER DISCLOSURE');
        expect(text).not.toContain('{{');
        expect(text).not.toContain('FTMO_SIMULATION.');
      });
    }

    it('theSpanishNotModelledLabelsAreTheDecidedWording', () => {
      expect(es.FTMO_SIMULATION.NOT_MODELLED.SWAP).toBe('Swap');
      expect(es.FTMO_SIMULATION.NOT_MODELLED.FTMO_COMMISSION).toBe('Comisión de FTMO');
      expect(es.FTMO_SIMULATION.NOT_MODELLED.INTRADAY_EQUITY).toBe('Equity intradía');
    });

    it('theDisclosureCoversTheThreePoints_InBothLocales', () => {
      const en3 = en.FTMO_SIMULATION.DISCLOSURE_RESULT.toLowerCase();
      expect(en3).toContain('not independent');
      expect(en3).toContain('not probabilities or a forecast');
      expect(en3).toContain('swap');
      expect(en3).toContain('understate breaches');
      expect(en3).toContain('no reward withdrawal and no scaling plan');
      expect(en3).toContain('optimistic');
      const es3 = es.FTMO_SIMULATION.DISCLOSURE_RESULT.toLowerCase();
      expect(es3).toContain('no son pruebas independientes');
      expect(es3).toContain('ni un pronóstico');
      expect(es3).toContain('swap');
      expect(es3).toContain('subestiman');
      expect(es3).toContain('scaling plan');
      expect(es3).toContain('optimista');
    });

    it('anUnknownNotModelledValue_ShowsTheRawValueThroughTheParam_NeverALeakedPlaceholder', () => {
      for (const locale of ['en', 'es'] as const) {
        TestBed.inject(TranslateService).use(locale);
        const fixture = create();
        const host = runWith(fixture, [
          { ...makeEvaluatedRunDto(BacktestRunKind.Deploy), notModelled: ['Slippage'] },
          makeEvaluatedRunDto(BacktestRunKind.Evaluation),
        ]);
        const li = host.querySelectorAll('.ftmo-sim__not-modelled li');
        expect(li.length).toBe(1);
        const dict = locale === 'en' ? en : es;
        expect(visibleText(li[0] as HTMLElement)).toBe(
          dict.FTMO_SIMULATION.NOT_MODELLED.UNKNOWN.replace('{{value}}', 'Slippage'),
        );
        expect(visibleText(host)).not.toContain('{{');
        fixture.destroy();
      }
    });

    it('falsification_NoResultDisclosureBeforeARun_OrForAZeroRunResult', () => {
      expect(
        (create().nativeElement as HTMLElement).querySelector('.ftmo-sim__result-disclosure'),
      ).toBeNull();
      const host = runWith(create(), []);
      expect(host.querySelector('.ftmo-sim__result-disclosure')).toBeNull();
    });

    // Fix 6: before the first Run, one hint instead of two empty slots.
    for (const locale of ['en', 'es'] as const) {
      it(`beforeTheFirstRun_ShowsOneHintAndNoEmptySlots_${locale}`, () => {
        TestBed.inject(TranslateService).use(locale);
        const dict = locale === 'en' ? en : es;
        const host = create().nativeElement as HTMLElement;
        expect(host.querySelectorAll('.ftmo-sim__no-run').length).toBe(0);
        const hints = host.querySelectorAll('.ftmo-sim__before-run');
        expect(hints.length).toBe(1);
        expect(visibleText(hints[0] as HTMLElement)).toBe(dict.FTMO_SIMULATION.BEFORE_RUN_HINT);
        expect(dict.FTMO_SIMULATION.BEFORE_RUN_HINT).toContain(dict.FTMO_SIMULATION.RUN);
        expect(visibleText(host)).not.toContain('{{');
        expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
      });
    }

    it('falsification_AfterARun_TheHintIsGone_ForOneKindZeroKindsAndBothKinds', () => {
      for (const runs of [
        [makeRunDto(BacktestRunKind.Deploy)],
        [],
        [makeRunDto(BacktestRunKind.Deploy), makeRunDto(BacktestRunKind.Evaluation)],
      ]) {
        const host = runWith(create(), runs);
        expect(host.querySelector('.ftmo-sim__before-run')).toBeNull();
      }
    });

    it('beforeAnyRun_NoBacktestsMessageIsNotShown', () => {
      const host = create().nativeElement as HTMLElement;
      expect(host.querySelector('.ftmo-sim__no-backtests')).toBeNull();
    });
  });

  function makeRunDto(kind: BacktestRunKind) {
    return {
      runId: 'r1',
      kind,
      segment: 0,
      status: 0,
      refusal: 0,
      raceRefusal: null,
      storedProfitTargetPct: null,
      grain: 0,
      rules: {
        phase1TargetPct: 0.1,
        phase2TargetPct: 0.05,
        minTradingDaysPerPhase: 4,
        timeLimitDays: null,
      },
      starts: [],
      summary: null,
      monthsWithoutStart: [],
      start1DiffersFromSingleStartAnchor: false,
      fxLow: null,
      fxHigh: null,
      unscalableCount: 0,
      notModelled: [],
      disclosure: 'run disclosure',
    };
  }
});
