import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FtmoRunPanelComponent } from './ftmo-run-panel.component';
import { FtmoRunPanelVm, toRunPanelVm } from '../ftmo-simulation.mappers';
import { BacktestRunKind } from '../../../../core/services/backtest.service';
import {
  FtmoChainOutcome,
  FtmoChallengeRaceRefusal,
  FtmoMultiStartRunDto,
  FtmoSimulationRefusal,
  FtmoSimulationStatus,
} from '../../../../core/models/ftmo-simulation.model';
import en from '../../../../../../public/assets/i18n/en.json';
import es from '../../../../../../public/assets/i18n/es.json';

/** Collapses template whitespace so the assertion reads the text a user sees. */
function visibleText(el: HTMLElement): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function baseVm(overrides: Partial<FtmoRunPanelVm> = {}): FtmoRunPanelVm {
  return {
    kind: BacktestRunKind.Deploy,
    // Legacy fields no longer on the VM: kept here to prove the panel never renders them.
    disclosure: 'Server disclosure text',
    notModelled: ['Slippage', 'Spread widening'],
    monthsWithoutStart: [],
    monthsWithoutStartCount: 0,
    start1Differs: false,
    fxLow: null,
    fxHigh: null,
    unscalableCount: 0,
    state: 'noStarts',
    ...overrides,
  } as unknown as FtmoRunPanelVm;
}

/** Only the fields `toRunPanelVm` reads for the refused / race-refused branches. */
function refusedRunDto(overrides: Partial<FtmoMultiStartRunDto>): FtmoMultiStartRunDto {
  return {
    kind: BacktestRunKind.Deploy,
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    raceRefusal: null,
    storedProfitTargetPct: null,
    summary: null,
    monthsWithoutStart: [],
    start1DiffersFromSingleStartAnchor: false,
    fxLow: null,
    fxHigh: null,
    unscalableCount: 0,
    notModelled: [],
    disclosure: 'Server disclosure text',
    ...overrides,
  } as unknown as FtmoMultiStartRunDto;
}

describe('FtmoRunPanelComponent', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoRunPanelComponent, TranslateModule.forRoot()],
    });
  });

  function create(vm: FtmoRunPanelVm): ComponentFixture<FtmoRunPanelComponent> {
    const fixture = TestBed.createComponent(FtmoRunPanelComponent);
    fixture.componentRef.setInput('vm', vm);
    fixture.detectChanges();
    return fixture;
  }

  it('stateRefused_RendersTheRefusalReason_AndNoOutcomeBarsOrOrderStatsTable', () => {
    const vm = baseVm({
      state: 'refused',
      refusalKey: 'FTMO_SIMULATION.REFUSAL.INSTRUMENT_SPEC_MISSING',
    } as any);
    const fixture = create(vm);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('FTMO_SIMULATION.REFUSAL.INSTRUMENT_SPEC_MISSING');
    expect(host.querySelector('app-ftmo-outcome-bars')).toBeNull();
    expect(host.querySelector('app-ftmo-order-stats-table')).toBeNull();
  });

  it('instrumentSpecMissing_FxRateNotDeclared_AndInvalidFxBand_EachRenderDistinctText', () => {
    const keys = [
      'FTMO_SIMULATION.REFUSAL.INSTRUMENT_SPEC_MISSING',
      'FTMO_SIMULATION.REFUSAL.FX_RATE_NOT_DECLARED',
      'FTMO_SIMULATION.REFUSAL.INVALID_FX_BAND',
    ];
    const texts = keys.map((refusalKey) => {
      const fixture = create(baseVm({ state: 'refused', refusalKey } as any));
      return (fixture.nativeElement as HTMLElement).textContent;
    });
    expect(new Set(texts).size).toBe(3);
  });

  it('monthsWithoutStart_RendersTheMonthsAndTheirCount_WhenNonEmpty', () => {
    const vm = baseVm({
      monthsWithoutStart: ['2024-01', '2024-02', '2024-03'],
      monthsWithoutStartCount: 3,
    });
    const fixture = create(vm);
    const text = (fixture.nativeElement as HTMLElement).textContent as string;
    expect(text).toContain('2024-01');
    expect(text).toContain('2024-02');
    expect(text).toContain('2024-03');
    expect(text).toContain('3');
  });

  it('start1DiffersFromSingleStartAnchorTrue_RendersAnExplicitDisclosure', () => {
    const fixture = create(baseVm({ start1Differs: true }));
    const text = (fixture.nativeElement as HTMLElement).textContent as string;
    expect(text).toContain('FTMO_SIMULATION.START1_DIFFERS_DISCLOSURE');
  });

  it('thePanelNoLongerRendersTheServerDisclosureOrNotModelledText_TheModalShowsThemOnce', () => {
    const fixture = create(baseVm());
    const text = (fixture.nativeElement as HTMLElement).textContent as string;
    expect(text).not.toContain('Server disclosure text');
    expect(text).not.toContain('Slippage');
    expect(text).not.toContain('Spread widening');
  });

  it('stateEvaluated_ComposesOutcomeBarsAndOrderStatsTable_AsChildrenWithTheVmDataPassedThrough', () => {
    const vm = baseVm({
      state: 'evaluated',
      outcomeRows: [
        {
          outcome: FtmoChainOutcome.Phase1Breached,
          labelKey: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_BREACHED',
          count: 5,
          share: 0.5,
          isCensored: false,
        },
      ],
      orderStatRows: [
        {
          labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.PHASE1_TARGET',
          n: 5,
          min: 1,
          q1: 2,
          median: 3,
          q3: 4,
          max: 5,
        },
      ],
    } as any);
    const fixture = create(vm);
    const host = fixture.nativeElement as HTMLElement;
    const bars = host.querySelector('app-ftmo-outcome-bars');
    const table = host.querySelector('app-ftmo-order-stats-table');
    expect(bars).toBeTruthy();
    expect(table).toBeTruthy();
    expect(bars?.textContent).toContain('FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_BREACHED');
    expect(table?.textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ROW.PHASE1_TARGET');
  });

  it('twoPanels_RenderInTwoDistinctSectionElements_EachSeparatelyLabelled_AndStackWithoutMerging', () => {
    const deployVm = baseVm({ kind: BacktestRunKind.Deploy });
    const evalVm = baseVm({ kind: BacktestRunKind.Evaluation });

    const deployFixture = create(deployVm);
    const evalFixture = create(evalVm);

    const deploySection = deployFixture.nativeElement.querySelector('section');
    const evalSection = evalFixture.nativeElement.querySelector('section');

    expect(deploySection).toBeTruthy();
    expect(evalSection).toBeTruthy();
    expect(deploySection.getAttribute('aria-label')).not.toBe(
      evalSection.getAttribute('aria-label'),
    );
  });

  describe('with the real en/es dictionaries', () => {
    // The REAL dictionaries (Dual-Entry precedent: portfolio-detail.component.spec.ts), not
    // `forRoot()` with no loader — only a real dictionary exposes a `{{param}}` placeholder that the
    // template forgot to fill, because a missing key renders the key itself.
    beforeEach(() => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('en', en);
      translate.setTranslation('es', es);
      translate.use('en');
    });

    it('stateRaceRefused_RendersProfitTargetMismatch_AlongsideTheStoredValue0_08_WithNoPlaceholderLeak', () => {
      const vm = baseVm({
        state: 'raceRefused',
        raceRefusalKey: 'FTMO_SIMULATION.RACE_REFUSAL.PROFIT_TARGET_MISMATCH',
        storedProfitTargetPct: 0.08,
      } as any);
      const host = create(vm).nativeElement as HTMLElement;
      const text = visibleText(host);
      expect(text).toContain('The stored profit target does not match the configured target.');
      expect(visibleText(host.querySelector('.ftmo-run-panel__stored-value') as HTMLElement)).toBe(
        'Stored profit target: 0.08',
      );
      expect(text).not.toContain('{{');
    });

    it('stateRaceRefused_InSpanish_RendersTheStoredValueThroughTheParam', () => {
      TestBed.inject(TranslateService).use('es');
      const vm = baseVm({
        state: 'raceRefused',
        raceRefusalKey: 'FTMO_SIMULATION.RACE_REFUSAL.PROFIT_TARGET_MISMATCH',
        storedProfitTargetPct: 0.08,
      } as any);
      const host = create(vm).nativeElement as HTMLElement;
      expect(visibleText(host.querySelector('.ftmo-run-panel__stored-value') as HTMLElement)).toBe(
        'Objetivo de ganancia almacenado: 0.08',
      );
      expect(visibleText(host)).not.toContain('{{');
    });

    it('monthsWithoutStart_RendersTheCountThroughTheParam_WithNoPlaceholderLeak', () => {
      const vm = baseVm({
        monthsWithoutStart: ['2024-01', '2024-02', '2024-03'],
        monthsWithoutStartCount: 3,
      });
      const host = create(vm).nativeElement as HTMLElement;
      const text = visibleText(host);
      expect(text).toContain('Months without a start (3) 2024-01, 2024-02, 2024-03');
      expect(text).not.toContain('{{');
    });

    it('anUnknownRefusalValue999_RendersUnknownValue999_WithNoPlaceholderLeak', () => {
      const vm = toRunPanelVm(
        refusedRunDto({
          status: FtmoSimulationStatus.Refused,
          refusal: 999 as FtmoSimulationRefusal,
        }),
      );
      const host = create(vm).nativeElement as HTMLElement;
      expect(visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement)).toBe(
        'Unknown value (999)',
      );
      expect(visibleText(host)).not.toContain('{{');
    });

    it('anUnknownRaceRefusalValue999_RendersUnknownValue999_AlongsideTheStoredValue_WithNoPlaceholderLeak', () => {
      const vm = toRunPanelVm(
        refusedRunDto({
          raceRefusal: 999 as FtmoChallengeRaceRefusal,
          storedProfitTargetPct: 0.08,
        }),
      );
      const host = create(vm).nativeElement as HTMLElement;
      const text = visibleText(host.querySelector('.ftmo-run-panel__race-refusal') as HTMLElement);
      expect(text).toBe('Unknown value (999) Stored profit target: 0.08');
      expect(visibleText(host)).not.toContain('{{');
    });

    it('aRefusedRunWithANullRefusal_StillRendersNoPlaceholderLeak', () => {
      const vm = toRunPanelVm(
        refusedRunDto({ status: FtmoSimulationStatus.Refused, refusal: null }),
      );
      const host = create(vm).nativeElement as HTMLElement;
      expect(visibleText(host.querySelector('.ftmo-run-panel__refusal') as HTMLElement)).toBe(
        'Unknown value (Not reported)',
      );
      expect(visibleText(host)).not.toContain('{{');
    });

    it('stateEvaluated_RendersBothChildrenWithRealText_WithNoPlaceholderLeakAnywhere', () => {
      const vm = baseVm({
        state: 'evaluated',
        start1Differs: true,
        monthsWithoutStart: ['2024-01'],
        monthsWithoutStartCount: 1,
        outcomeRows: [
          {
            outcome: FtmoChainOutcome.Phase1Breached,
            labelKey: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_BREACHED',
            count: 5,
            share: 0.5,
            isCensored: false,
          },
        ],
        orderStatRows: [
          {
            labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.PHASE1_TARGET',
            n: 0,
            min: null,
            q1: null,
            median: null,
            q3: null,
            max: null,
          },
        ],
      } as any);
      const host = create(vm).nativeElement as HTMLElement;
      const text = visibleText(host);
      expect(text).toContain('Deploy');
      expect(text).toContain('Eliminated in Phase 1');
      expect(text).toContain('Days to Phase 1 target');
      expect(text).toContain('Start 1 differs from the single-start anchor.');
      expect(text).not.toContain('{{');
      expect(text).not.toContain('FTMO_SIMULATION.');
    });
  });
});
