import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidatesDto,
  FtmoGroupSimulationDto,
} from '../../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationStatus } from '../../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import {
  FtmoRequestError,
  FtmoSimulationService,
} from '../../../core/services/ftmo-simulation.service';
import {
  TradingAccountDto,
  TradingAccountService,
} from '../../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

function candidate(id: string, needsFx = false): FtmoGroupCandidateDto {
  const run = {
    runId: `r-${id}`,
    symbol: 'EURUSD',
    tradeCount: 5,
    firstOpen: '2020-01-01T00:00:00',
    lastClose: '2021-01-01T00:00:00',
    hasInstrumentSpec: true,
    isCalibrated: true,
    profitCurrency: needsFx ? 'EUR' : 'USD',
    needsFxBand: needsFx,
    sourceTimeZoneId: 'Europe/Berlin',
  };
  return {
    strategyId: id,
    name: `Name ${id}`,
    symbol: 'EURUSD',
    deploy: run,
    evaluation: run,
    nameExistsOnOtherAccount: false,
  };
}

const CANDIDATES: FtmoGroupCandidatesDto = {
  tradingAccountId: 'acc',
  maxMembers: 4,
  candidates: [candidate('a'), candidate('b'), candidate('eur', true)],
};

function result(dailyLossLimitPct: number | null, peak: number): FtmoGroupSimulationDto {
  return {
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    sharedRefusal: null,
    dailyLossLimitPct,
    maxLossLimitPct: 0.1,
    members: [],
    duplicateIdsRemoved: [],
    duplicateNameWarnings: [],
    unknownStrategyIds: [],
    disclosures: [],
    kinds: [
      {
        kind: BacktestRunKind.Deploy,
        status: FtmoSimulationStatus.Evaluated,
        refusal: null,
        memberRefusals: [],
        window: null,
        coverage: [],
        run: null,
        diagnostics: {
          contributions: [],
          attribution: {
            decidingBreachStarts: 0,
            sharedCloseStarts: 0,
            unattributedStarts: 0,
            members: [],
          },
          peak: { peakConcurrentOpen: peak, firstReachedSource: null, memberIdsAtPeak: [] },
        },
      },
    ],
  };
}

interface Harness {
  fixture: ComponentFixture<FtmoGroupSimulationPageComponent>;
  simulateGroup: ReturnType<typeof vi.fn>;
}

function create(
  simulate: () => Observable<FtmoGroupSimulationDto> = () => of(result(null, 0)),
  accounts: () => Observable<TradingAccountDto[]> = () =>
    of([{ id: 'acc', name: 'SBDEMO2' } as TradingAccountDto]),
  lang: 'en' | 'es' = 'en',
): Harness {
  const simulateGroup = vi.fn(simulate);
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
    providers: [
      { provide: TradingAccountService, useValue: { getAll: accounts } },
      {
        provide: FtmoSimulationService,
        useValue: { getGroupCandidates: () => of(CANDIDATES), simulateGroup },
      },
    ],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupSimulationPageComponent);
  fixture.detectChanges();
  return { fixture, simulateGroup };
}

function el(f: ComponentFixture<unknown>): HTMLElement {
  return f.nativeElement as HTMLElement;
}

function runButton(f: ComponentFixture<unknown>): HTMLButtonElement {
  return el(f).querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

function typeRisk(f: ComponentFixture<unknown>, value: string): void {
  const risk = el(f).querySelector<HTMLInputElement>('input[name="targetRiskPerTrade"]')!;
  risk.value = value;
  risk.dispatchEvent(new Event('input'));
  f.detectChanges();
}

function select(h: Harness, ...ids: string[]): void {
  h.fixture.componentInstance.onSelectionChange(new Set(ids));
  h.fixture.detectChanges();
}

function activateRun(f: ComponentFixture<unknown>): void {
  el(f)
    .querySelector('form')!
    .dispatchEvent(new Event('submit', { cancelable: true }));
  f.detectChanges();
}

const SIM = (en as JsonTree)['FTMO_SIMULATION'];
const READOUT = (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['READOUT'];

/** The exact text of the FTMO daily limit reference line (the second reference). */
function dailyRef(f: ComponentFixture<unknown>): string {
  const refs = el(f).querySelectorAll('.ftmo-group-form__reference');
  return (refs[1]?.textContent ?? '').trim();
}

describe('FtmoGroupSimulationPageComponent run lifecycle', () => {
  it('sendsNothingWhileSelectingOrEditing', () => {
    const h = create();
    select(h, 'a', 'b');
    typeRisk(h.fixture, '25');
    expect(h.simulateGroup).not.toHaveBeenCalled();
  });

  it('keepsRunDisabledWithoutMembersOrRisk_AndEnablesItWithBoth', () => {
    const h = create();
    expect(runButton(h.fixture).disabled).toBe(true);
    select(h, 'a');
    expect(runButton(h.fixture).disabled).toBe(true);
    typeRisk(h.fixture, '25');
    expect(runButton(h.fixture).disabled).toBe(false);
  });

  it('sendsTheBuiltBodyOnceOnRun_WithHiddenFxAsNull', () => {
    const h = create();
    select(h, 'a', 'b');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    expect(h.simulateGroup).toHaveBeenCalledTimes(1);
    expect(h.simulateGroup).toHaveBeenCalledWith({
      memberStrategyIds: ['a', 'b'],
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 25,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 2,
      step: 0.01,
      minLot: 0.01,
      maxLots: 10,
    });
  });

  it('showsTheFxInputsOnlyWhenASelectedMemberNeedsABand', () => {
    const h = create();
    select(h, 'a');
    expect(el(h.fixture).querySelector('input[name="fxLow"]')).toBeNull();
    select(h, 'a', 'eur');
    expect(el(h.fixture).querySelector('input[name="fxLow"]')).not.toBeNull();
    expect(runButton(h.fixture).disabled).toBe(true);
    typeRisk(h.fixture, '25');
    expect(runButton(h.fixture).disabled).toBe(false);
  });

  it('blocksASecondRunWhileOneIsInFlight', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = create(() => pending);
    select(h, 'a');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    activateRun(h.fixture);
    expect(h.simulateGroup).toHaveBeenCalledTimes(1);
    expect(runButton(h.fixture).disabled).toBe(true);

    pending.next(result(null, 0));
    pending.complete();
    h.fixture.detectChanges();
    expect(runButton(h.fixture).disabled).toBe(false);
  });

  it('ignoresADirectSecondRunCallWhileInFlight_EvenIfTheFormWereBypassed', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = create(() => pending);
    select(h, 'a');
    typeRisk(h.fixture, '25');
    h.fixture.componentInstance.run();
    h.fixture.componentInstance.run();
    expect(h.simulateGroup).toHaveBeenCalledTimes(1);
  });

  it('cancelsTheInFlightRequestWhenTheScreenIsDestroyed', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = create(() => pending);
    select(h, 'a');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    expect(pending.observed).toBe(true);
    h.fixture.destroy();
    expect(pending.observed).toBe(false);
  });

  it('aBadRequestShowsATranslatedErrorReEnablesRunAndRendersNoResult', () => {
    const error: FtmoRequestError = { key: 'ERRORS.INVALID_QUERY', detail: 'too many' };
    const h = create(() => throwError(() => error));
    select(h, 'a');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    const alert = el(h.fixture).querySelector('.ftmo-group-page__run-error')!;
    expect(alert.textContent).toContain('too many');
    expect(alert.textContent).not.toContain('{{');
    expect(runButton(h.fixture).disabled).toBe(false);
    expect(h.fixture.componentInstance.result()).toBeNull();
  });

  it('aMissingErrorDetailFallsBackToTheTranslatedNotReportedText', () => {
    const error: FtmoRequestError = { key: 'ERRORS.INVALID_QUERY', detail: null };
    const h = create(() => throwError(() => error));
    select(h, 'a');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    const text = el(h.fixture).querySelector('.ftmo-group-page__run-error')!.textContent ?? '';
    expect(text).toContain(SIM['NOT_REPORTED']);
    expect(text).not.toContain('null');
  });

  it('aNewRunClearsThePreviousResultAndErrorFirst', () => {
    let calls = 0;
    const second = new Subject<FtmoGroupSimulationDto>();
    const h = create(() => (++calls === 1 ? of(result(null, 0)) : second));
    select(h, 'a');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    expect(h.fixture.componentInstance.result()).not.toBeNull();
    activateRun(h.fixture);
    expect(h.fixture.componentInstance.result()).toBeNull();
  });
});

describe('FtmoGroupSimulationPageComponent readout wiring', () => {
  it('showsKTimesRiskBeforeARun', () => {
    const h = create();
    select(h, 'a', 'b');
    typeRisk(h.fixture, '25');
    const text = el(h.fixture).querySelector('.ftmo-group-form__worst')!.textContent ?? '';
    expect(text).toContain('50');
    expect(text).toContain('0.50%');
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).toBeNull();
  });

  it('afterARunShowsTheObservedPeakAndFollowsTheEchoedDailyLimit', () => {
    // The backend echoes the daily limit as a fraction: 0.04 is 4%.
    const h = create(() => of(result(0.04, 3)));
    select(h, 'a', 'b');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    const observed = el(h.fixture).querySelector('.ftmo-group-form__observed')!.textContent ?? '';
    expect(observed).toContain('75');
    expect(observed).toContain(SIM['KIND']['DEPLOY']);
    expect(dailyRef(h.fixture)).toBe(READOUT['DAILY_REF'].replace('{{limit}}', '4'));
  });

  it('rendersTheRealEchoedFraction0_05As5Percent_AndDoesNotFlagA4PercentRisk (F3a RELIABILITY-001)', () => {
    const h = create(() => of(result(0.05, 1)));
    select(h, 'a', 'b');
    typeRisk(h.fixture, '200');
    activateRun(h.fixture);
    expect(dailyRef(h.fixture)).toBe(READOUT['DAILY_REF'].replace('{{limit}}', '5'));
    const flags = Array.from(el(h.fixture).querySelectorAll('.ftmo-group-form__exceeded')).map(
      (e) => (e.textContent ?? '').trim(),
    );
    expect(flags).not.toContain(READOUT['ABOVE_DAILY'].replace('{{limit}}', '5'));
    expect(flags.some((t) => t.includes('0.05'))).toBe(false);
  });

  it('recomputesOnEditWithoutARequest', () => {
    const h = create();
    select(h, 'a');
    typeRisk(h.fixture, '25');
    select(h, 'a', 'b');
    expect(el(h.fixture).querySelector('.ftmo-group-form__worst')!.textContent).toContain('50');
    expect(h.simulateGroup).not.toHaveBeenCalled();
  });
});

describe('FtmoGroupSimulationPageComponent result stays consistent with its inputs (F3a RELIABILITY-002)', () => {
  const TWO_ACCOUNTS = (): Observable<TradingAccountDto[]> =>
    of([
      { id: 'acc', name: 'SBDEMO2' } as TradingAccountDto,
      { id: 'acc-b', name: 'Other' } as TradingAccountDto,
    ]);

  function ranOnce(simulate: () => Observable<FtmoGroupSimulationDto>): Harness {
    const h = create(simulate, TWO_ACCOUNTS);
    select(h, 'a', 'b');
    typeRisk(h.fixture, '25');
    activateRun(h.fixture);
    return h;
  }

  it('switchingTheAccountCancelsAnInFlightRun_SoItsLateResponseIsNeverShown', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = ranOnce(() => pending);
    expect(pending.observed).toBe(true);

    h.fixture.componentInstance.selectAccount('acc-b');
    h.fixture.detectChanges();
    expect(pending.observed).toBe(false);
    expect(h.fixture.componentInstance.running()).toBe(false);

    pending.next(result(0.05, 3));
    h.fixture.detectChanges();
    expect(h.fixture.componentInstance.result()).toBeNull();
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).toBeNull();
  });

  it('switchingTheAccountAfterARunClearsTheResultAndTheRunError', () => {
    const h = ranOnce(() => of(result(0.05, 3)));
    expect(h.fixture.componentInstance.result()).not.toBeNull();
    h.fixture.componentInstance.selectAccount('acc-b');
    expect(h.fixture.componentInstance.result()).toBeNull();

    const error: FtmoRequestError = { key: 'ERRORS.INVALID_QUERY', detail: 'too many' };
    const failed = ranOnce(() => throwError(() => error));
    expect(failed.fixture.componentInstance.runError()).not.toBeNull();
    failed.fixture.componentInstance.selectAccount('acc-b');
    failed.fixture.detectChanges();
    expect(failed.fixture.componentInstance.runError()).toBeNull();
    expect(el(failed.fixture).querySelector('.ftmo-group-page__run-error')).toBeNull();
  });

  it('changingTheSelectionAfterARunClearsTheResult_BecauseItNoLongerDescribesTheGroup', () => {
    const h = ranOnce(() => of(result(0.05, 3)));
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).not.toBeNull();
    select(h, 'a');
    expect(h.fixture.componentInstance.result()).toBeNull();
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).toBeNull();
    expect(h.simulateGroup).toHaveBeenCalledTimes(1);
  });

  it('changingTheSelectionDuringARunCancelsIt', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = ranOnce(() => pending);
    select(h, 'a');
    expect(pending.observed).toBe(false);
    expect(runButton(h.fixture).disabled).toBe(false);
  });

  it('editingTheRiskAfterARunClearsTheResult_SoNoStalePeakIsMultipliedByTheNewRisk', () => {
    const h = ranOnce(() => of(result(0.05, 3)));
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).not.toBeNull();
    typeRisk(h.fixture, '30');
    expect(h.fixture.componentInstance.result()).toBeNull();
    expect(el(h.fixture).querySelector('.ftmo-group-form__observed')).toBeNull();
    // The client-side k x risk line still follows the edit.
    expect(el(h.fixture).querySelector('.ftmo-group-form__worst')!.textContent).toContain('60');
    expect(h.simulateGroup).toHaveBeenCalledTimes(1);
  });

  it('editingTheCapitalDuringARunCancelsIt', () => {
    const pending = new Subject<FtmoGroupSimulationDto>();
    const h = ranOnce(() => pending);
    const capital = el(h.fixture).querySelector<HTMLInputElement>('input[name="initialCapital"]')!;
    capital.value = '20000';
    capital.dispatchEvent(new Event('input'));
    h.fixture.detectChanges();
    expect(pending.observed).toBe(false);
    expect(h.fixture.componentInstance.running()).toBe(false);
  });
});

describe('FtmoGroupSimulationPageComponent account load failure (F2 RELIABILITY-001)', () => {
  it('showsATranslatedErrorStateInsteadOfABlankPage_InEnglishAndSpanish', () => {
    for (const lang of ['en', 'es'] as const) {
      const h = create(undefined, () => throwError(() => new Error('boom')), lang);
      const alert = el(h.fixture).querySelector('.ftmo-group-page__accounts-error')!;
      const dict = (lang === 'en' ? en : es) as JsonTree;
      expect(alert.textContent).toContain(
        dict['SIMULATOR']['FTMO_GROUP']['PICKER']['ACCOUNTS_ERROR'],
      );
      expect(alert.textContent).not.toContain('{{');
      expect(alert.textContent).not.toContain('SIMULATOR.');
      expect(el(h.fixture).textContent).not.toContain(
        dict['SIMULATOR']['FTMO_GROUP']['PICKER']['NO_ACCOUNTS'],
      );
    }
  });
});
