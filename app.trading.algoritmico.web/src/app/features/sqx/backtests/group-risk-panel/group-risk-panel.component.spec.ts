import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { GroupRiskPanelComponent } from './group-risk-panel.component';
import {
  BacktestSegment,
  BacktestService,
  GroupRiskAnalysisDto,
  GroupRiskAnalysisStatus,
  GroupRiskMemberStatus,
  BacktestRunKind,
  BacktestPortfolioRiskDto,
  SeriesDensityDto,
  VarWithholdReason,
} from '../../../../core/services/backtest.service';

function density(overrides: Partial<SeriesDensityDto> = {}): SeriesDensityDto {
  return {
    tradeCount: 329,
    excludedUnscalableCount: 0,
    denseDayCount: 3860,
    negativeDayCount: 164,
    nonZeroDayCount: 318,
    negativeWindowCount: 1148,
    ...overrides,
  };
}

/** The measured IST shape: daily VaR95 withheld, VaR99 and the monthly figure published. */
function istRisk(overrides: Partial<BacktestPortfolioRiskDto> = {}): BacktestPortfolioRiskDto {
  return {
    initialCapital: 10000,
    method: 'Historical',
    windowDays: 0,
    observationDays: 3860,
    segment: BacktestSegment.InSampleTest,
    dailyVar95: null,
    dailyVar95Percent: null,
    dailyVar95Withheld: VarWithholdReason.InsufficientNegativeObservations,
    dailyVar99: 199.4423,
    dailyVar99Percent: 0.0199,
    dailyVar99Withheld: VarWithholdReason.None,
    monthlyVar95: 400.19,
    monthlyVar95Percent: 0.04,
    monthlyVar95Withheld: VarWithholdReason.None,
    monthlyVarOverlappingWindows: 3831,
    monthlyVarIndependentWindows: 128,
    density: density(),
    byService: [],
    varTarget: null,
    ...overrides,
  };
}

function completed(overrides: Partial<GroupRiskAnalysisDto> = {}): GroupRiskAnalysisDto {
  return {
    status: GroupRiskAnalysisStatus.Completed,
    segment: BacktestSegment.InSampleTest,
    members: [
      {
        strategyId: 'strategy-1',
        label: 'Alpha',
        status: GroupRiskMemberStatus.Resolved,
        segment: BacktestSegment.InSampleTest,
        runKind: BacktestRunKind.Deploy,
        runId: 'run-1',
        detail: null,
      },
    ],
    risk: istRisk(),
    correlation: {
      labels: ['Alpha'],
      matrix: [[1]],
      coActiveDays: [[3860]],
      coActiveShare: [[1]],
      observationDays: 3860,
      averageCorrelation: null,
      withheldCellCount: 0,
      alignment: 'Intersection',
      segment: BacktestSegment.InSampleTest,
      density: density(),
    },
    refusal: null,
    ...overrides,
  };
}

function refused(status: GroupRiskAnalysisStatus, refusal: string): GroupRiskAnalysisDto {
  return {
    status,
    segment: null,
    members: [],
    risk: null,
    correlation: null,
    refusal,
  };
}

describe('GroupRiskPanelComponent', () => {
  let backtestServiceMock: Partial<BacktestService>;

  function configure(analysis: GroupRiskAnalysisDto) {
    backtestServiceMock = { getGroupRisk: vi.fn().mockReturnValue(of(analysis)) };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [GroupRiskPanelComponent, TranslateModule.forRoot()],
      providers: [{ provide: BacktestService, useValue: backtestServiceMock }],
    });
  }

  function create(segment: BacktestSegment | null = BacktestSegment.InSampleTest) {
    const fixture = TestBed.createComponent(GroupRiskPanelComponent);
    fixture.componentRef.setInput('strategyIds', ['strategy-1']);
    fixture.detectChanges();
    fixture.componentInstance.segment.set(segment);
    fixture.componentInstance.analyze();
    fixture.detectChanges();
    return fixture;
  }

  // =====================================================================
  // THE load-bearing assertion of the whole slice.
  // =====================================================================

  it('withheldDailyVar95_RendersItsStateLabelAndNeverAZero', () => {
    configure(completed());
    const fixture = create();

    const cell = fixture.nativeElement.querySelector('[data-testid="daily-var95"]') as HTMLElement;
    expect(cell).toBeTruthy();
    expect(cell.textContent).toContain(
      'SQX.BACKTESTS.GROUP_RISK.WITHHELD_INSUFFICIENT_NEGATIVE_OBSERVATIONS',
    );
    // A withheld figure must not render a digit AT ALL in its own cell. `0`, `0.00` and `-` are all
    // claims the data does not make; the only honest content is the reason it is absent.
    expect(cell.textContent).not.toMatch(/\d/);
  });

  it('publishedFigures_StillRenderTheirNumbersBesideTheWithheldOne', () => {
    configure(completed());
    const fixture = create();

    const var99 = fixture.nativeElement.querySelector('[data-testid="daily-var99"]') as HTMLElement;
    const monthly = fixture.nativeElement.querySelector(
      '[data-testid="monthly-var95"]',
    ) as HTMLElement;

    expect(var99.textContent).toContain('199.44');
    expect(monthly.textContent).toContain('400.19');
    expect(monthly.textContent).not.toContain('SQX.BACKTESTS.GROUP_RISK.WITHHELD');
  });

  it('everyWithholdReason_RendersItsOwnLabel', () => {
    for (const [reason, key] of [
      [VarWithholdReason.NoSeries, 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_NO_SERIES'],
      [
        VarWithholdReason.InsufficientHistory,
        'SQX.BACKTESTS.GROUP_RISK.WITHHELD_INSUFFICIENT_HISTORY',
      ],
    ] as const) {
      configure(
        completed({
          risk: istRisk({ monthlyVar95: null, monthlyVar95Withheld: reason }),
        }),
      );
      const fixture = create();

      const cell = fixture.nativeElement.querySelector(
        '[data-testid="monthly-var95"]',
      ) as HTMLElement;
      expect(cell.textContent).toContain(key);
      expect(cell.textContent).not.toMatch(/\d/);
    }
  });

  // =====================================================================
  // Disclosure that must accompany every figure.
  // =====================================================================

  it('completedAnalysis_AlwaysDisclosesDensitySegmentDenominatorAndBothQualifiers', () => {
    configure(completed());
    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('3860');
    expect(text).toContain('164');
    expect(text).toContain('318');
    expect(text).toContain('329');
    expect(text).toContain('SQX.BACKTESTS.GROUP_RISK.EXCLUDED_UNSCALABLE');
    expect(text).toContain('SQX.BACKTESTS.GROUP_RISK.SEGMENT_IN_SAMPLE_TEST');
    expect(text).toContain('SQX.BACKTESTS.GROUP_RISK.DENOMINATOR_LABEL');
    expect(text).toContain('SQX.BACKTESTS.GROUP_RISK.APPROXIMATION_DISCLAIMER');
    expect(text).toContain('SQX.BACKTESTS.GROUP_RISK.SIMULATED_CLOSES');
  });

  it('simulatedClosesQualifier_AccompaniesTheCorrelationMatrixToo', () => {
    configure(completed());
    const fixture = create();

    const matrix = fixture.nativeElement.querySelector(
      '[data-testid="correlation-panel"]',
    ) as HTMLElement;
    expect(matrix.textContent).toContain('SQX.BACKTESTS.GROUP_RISK.SIMULATED_CLOSES');
    expect(matrix.textContent).toContain('Intersection');
  });

  it('withheldCorrelationCell_RendersItsOwnMarkerAndNeverAZero', () => {
    configure(
      completed({
        correlation: {
          labels: ['Alpha', 'Beta'],
          matrix: [
            [1, null],
            [null, 1],
          ],
          coActiveDays: [
            [3860, 1],
            [1, 3804],
          ],
          coActiveShare: [
            [1, 0.001],
            [0.001, 1],
          ],
          observationDays: 3860,
          averageCorrelation: null,
          withheldCellCount: 1,
          alignment: 'Intersection',
          segment: BacktestSegment.InSampleTest,
          density: density(),
        },
      }),
    );
    const fixture = create();

    const cell = fixture.nativeElement.querySelector('[data-testid="cell-0-1"]') as HTMLElement;
    expect(cell.textContent).toContain('SQX.BACKTESTS.GROUP_RISK.CELL_WITHHELD');
    expect(cell.textContent).not.toContain('0.00');

    const average = fixture.nativeElement.querySelector(
      '[data-testid="average-correlation"]',
    ) as HTMLElement;
    expect(average.textContent).toContain('SQX.BACKTESTS.GROUP_RISK.CELL_WITHHELD');
  });

  // =====================================================================
  // Each refusal renders its OWN message, and no figure.
  // =====================================================================

  it('eachRefusalStatus_RendersItsOwnMessageAndNoFigures', () => {
    const cases: [GroupRiskAnalysisStatus, string][] = [
      [
        GroupRiskAnalysisStatus.SegmentNotSpecified,
        'SQX.BACKTESTS.GROUP_RISK.STATUS_SEGMENT_NOT_SPECIFIED',
      ],
      [
        GroupRiskAnalysisStatus.UnknownSegmentNotSelectable,
        'SQX.BACKTESTS.GROUP_RISK.STATUS_UNKNOWN_SEGMENT',
      ],
      [
        GroupRiskAnalysisStatus.RunSegmentsDisagree,
        'SQX.BACKTESTS.GROUP_RISK.STATUS_RUN_SEGMENTS_DISAGREE',
      ],
      [GroupRiskAnalysisStatus.NoEvidenceForSegment, 'SQX.BACKTESTS.GROUP_RISK.STATUS_NO_EVIDENCE'],
      [
        GroupRiskAnalysisStatus.AmbiguousRunSelection,
        'SQX.BACKTESTS.GROUP_RISK.STATUS_AMBIGUOUS_RUN',
      ],
      [GroupRiskAnalysisStatus.NonUnitWeight, 'SQX.BACKTESTS.GROUP_RISK.STATUS_NON_UNIT_WEIGHT'],
      [GroupRiskAnalysisStatus.HeterogeneousGroup, 'SQX.BACKTESTS.GROUP_RISK.STATUS_HETEROGENEOUS'],
    ];

    for (const [status, key] of cases) {
      configure(refused(status, 'server sentence'));
      const fixture = create();

      const refusal = fixture.nativeElement.querySelector('[data-testid="refusal"]') as HTMLElement;
      expect(refusal, `${status} must render a refusal`).toBeTruthy();
      expect(refusal.textContent).toContain(key);
      // The server's own sentence names the member/run, so it is shown verbatim beside the key.
      expect(refusal.textContent).toContain('server sentence');

      expect(fixture.nativeElement.querySelector('[data-testid="daily-var95"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="correlation-panel"]')).toBeNull();
    }
  });

  it('omittedSegment_IsNeverSentAsUnknown', () => {
    configure(refused(GroupRiskAnalysisStatus.SegmentNotSpecified, 'none chosen'));
    const fixture = create(null);

    const query = (backtestServiceMock.getGroupRisk as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(query.segment).toBeUndefined();
    expect(query.segment).not.toBe(BacktestSegment.Unknown);
    expect(fixture.nativeElement.querySelector('[data-testid="refusal"]')).toBeTruthy();
  });

  it('refusedMembers_AreNamedIndividually', () => {
    configure({
      ...refused(GroupRiskAnalysisStatus.AmbiguousRunSelection, 'group sentence'),
      members: [
        {
          strategyId: 'strategy-1',
          label: 'Alpha',
          status: GroupRiskMemberStatus.AmbiguousRunSelection,
          segment: null,
          runKind: null,
          runId: null,
          detail: 'Alpha has runs in BOTH slots',
        },
      ],
    });
    const fixture = create();

    const members = fixture.nativeElement.querySelector('[data-testid="members"]') as HTMLElement;
    expect(members.textContent).toContain('Alpha');
    expect(members.textContent).toContain('Alpha has runs in BOTH slots');
    expect(members.textContent).toContain('SQX.BACKTESTS.GROUP_RISK.STATUS_AMBIGUOUS_RUN');
  });
});
