import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

/**
 * Mirrors AppTradingAlgoritmico.Application.DTOs.Backtests.BacktestImportOutcome (numeric, matches
 * the backend JSON). The previous revision also carried `Reattributed` and `Conflict`; both existed
 * only because a run's strategy was inferred from its filename and could therefore be wrong.
 */
export enum BacktestImportOutcome {
  Imported = 0,
  Unchanged = 1,
  Replaced = 2,
  Rejected = 3,
}

export enum BacktestRunKind {
  Deploy = 1,
  Evaluation = 2,
}

export enum BacktestReadiness {
  None = 0,
  SizingOnly = 1,
  Evaluable = 2,
}

export enum BacktestSegment {
  Unknown = 0,
  InSample = 1,
  OutOfSample = 2,
  InSampleTest = 3,
}

export enum CalibrationStatus {
  Calibrated = 0,
  InsufficientSamples = 1,
  Inconsistent = 2,
}

export interface BacktestImportResultDto {
  fileName: string;
  outcome: BacktestImportOutcome;
  tradeCount: number | null;
  rejectedRowCount: number | null;
  reason: string | null;
}

export interface WalkForwardImportResultDto {
  fileName: string;
  outcome: BacktestImportOutcome;
  windowCount: number | null;
  oosFromDate: string | null;
  reason: string | null;
}

export interface BacktestRunSummaryDto {
  id: string;
  sourceFileName: string;
  symbol: string | null;
  kind: BacktestRunKind;
  tradeCount: number;
  createdAt: string;
}

export interface WalkForwardExportSummaryDto {
  id: string;
  sourceFileName: string;
  oosFromDate: string;
  windowCount: number;
  deployParameters: string;
  evaluationParameters: string;
  createdAt: string;
}

export interface StrategyBacktestsDto {
  strategyId: string;
  deploy: BacktestRunSummaryDto | null;
  evaluation: BacktestRunSummaryDto | null;
  walkForwardExport: WalkForwardExportSummaryDto | null;
}

export interface BacktestRunDto {
  id: string;
  sourceFileName: string;
  symbol: string | null;
  strategyId: string;
  strategyName: string;
  kind: BacktestRunKind;
  tradeCount: number;
  createdAt: string;
}

export interface BacktestTradeDto {
  id: string;
  rowIndex: number;
  ticket: number;
  symbol: string;
  type: string;
  openTime: string;
  openPrice: number;
  size: number;
  closeTime: string;
  closePrice: number;
  profit: number;
  balance: number;
  sampleTypeRaw: string;
  segment: BacktestSegment;
  segmentIndex: number | null;
  closeType: string;
  realizedRisk: number | null;
  stopLoss: number | null;
  comment: string | null;
}

export interface SymbolCalibrationDto {
  symbol: string;
  pointValue: number | null;
  sampleCount: number;
  minObserved: number | null;
  maxObserved: number | null;
  status: CalibrationStatus;
  calibratedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * Mirrors AppTradingAlgoritmico.Domain.Enums.VarWithholdReason.
 *
 * A withheld figure arrives as `null` with one of these beside it — NEVER as `0`. A numeric zero
 * would read as "this group loses nothing at the 5th percentile", which is a claim the backtest
 * data does not make, and the template must never substitute one.
 */
export enum VarWithholdReason {
  None = 0,
  NoSeries = 1,
  InsufficientHistory = 2,
  InsufficientNegativeObservations = 3,
}

/** Mirrors AppTradingAlgoritmico.Domain.Enums.GroupRiskMemberStatus. */
export enum GroupRiskMemberStatus {
  Resolved = 0,
  RunSegmentsDisagree = 1,
  NoEvidenceForSegment = 2,
  AmbiguousRunSelection = 3,
  RiskNotEstimable = 4,
  NonUnitWeight = 5,
}

/** Mirrors AppTradingAlgoritmico.Domain.Enums.GroupRiskAnalysisStatus. */
export enum GroupRiskAnalysisStatus {
  Completed = 0,
  SegmentNotSpecified = 1,
  UnknownSegmentNotSelectable = 2,
  NoStrategiesRequested = 3,
  StrategyNotFound = 4,
  InvalidLotGrid = 5,
  RunSegmentsDisagree = 6,
  NoEvidenceForSegment = 7,
  AmbiguousRunSelection = 8,
  RiskNotEstimable = 9,
  NonUnitWeight = 10,
  HeterogeneousGroup = 11,
}

/**
 * Mixed provenance, deliberately: the four DAY-level counts are what the density gates consumed,
 * while `tradeCount`/`excludedUnscalableCount` come from the bridge. `nonZeroDayCount` is
 * disclosure only and never gates — a non-zero-day share threshold clears on both committed
 * fixtures and would publish a daily VaR measured to be exactly 0.00.
 */
export interface SeriesDensityDto {
  tradeCount: number;
  excludedUnscalableCount: number;
  denseDayCount: number;
  negativeDayCount: number;
  nonZeroDayCount: number;
  negativeWindowCount: number;
}

export interface VarTargetReadoutDto {
  targetVarPct: number | null;
  varFloorPct: number | null;
  horizonDays: number;
  insufficientHistory: boolean;
  observationDays: number;
  overlappingWindows: number;
  independentWindows: number;
  monthlyVar95: number | null;
  monthlyVar95Percent: number | null;
  impliedMultiplier: number | null;
}

export interface BacktestServiceRiskDto {
  service: string;
  strategyCount: number;
  netProfit: number;
  dailyVar95: number | null;
  dailyVar95Percent: number | null;
  dailyVar95Withheld: VarWithholdReason;
  monthlyVar95: number | null;
  monthlyVar95Percent: number | null;
  monthlyVar95Withheld: VarWithholdReason;
  monthlyVarOverlappingWindows: number;
  monthlyVarIndependentWindows: number;
  density: SeriesDensityDto;
}

/** Every VaR field is nullable and paired with its reason. `windowDays` is always 0 — no trim. */
export interface BacktestPortfolioRiskDto {
  initialCapital: number;
  method: string;
  windowDays: number;
  observationDays: number;
  segment: BacktestSegment;
  dailyVar95: number | null;
  dailyVar95Percent: number | null;
  dailyVar95Withheld: VarWithholdReason;
  dailyVar99: number | null;
  dailyVar99Percent: number | null;
  dailyVar99Withheld: VarWithholdReason;
  monthlyVar95: number | null;
  monthlyVar95Percent: number | null;
  monthlyVar95Withheld: VarWithholdReason;
  monthlyVarOverlappingWindows: number;
  monthlyVarIndependentWindows: number;
  density: SeriesDensityDto;
  byService: BacktestServiceRiskDto[];
  varTarget: VarTargetReadoutDto | null;
}

export interface BacktestCorrelationDto {
  labels: string[];
  matrix: (number | null)[][];
  coActiveDays: number[][];
  coActiveShare: number[][];
  observationDays: number;
  averageCorrelation: number | null;
  withheldCellCount: number;
  alignment: string;
  segment: BacktestSegment;
  density: SeriesDensityDto;
}

export interface GroupRiskMemberDto {
  strategyId: string;
  label: string;
  status: GroupRiskMemberStatus;
  segment: BacktestSegment | null;
  runKind: BacktestRunKind | null;
  runId: string | null;
  detail: string | null;
}

export interface GroupRiskAnalysisDto {
  status: GroupRiskAnalysisStatus;
  segment: BacktestSegment | null;
  members: GroupRiskMemberDto[];
  risk: BacktestPortfolioRiskDto | null;
  correlation: BacktestCorrelationDto | null;
  refusal: string | null;
}

/** The query the panel sends. `segment` is optional so "not specified" stays expressible. */
export interface GroupRiskAnalysisQuery {
  strategyIds: string[];
  initialCapital: number;
  targetRiskPerTrade: number;
  segment?: BacktestSegment;
  runKind?: BacktestRunKind;
  fundingService?: string;
}

export const BACKTEST_OUTCOME_LABELS: Record<BacktestImportOutcome, string> = {
  [BacktestImportOutcome.Imported]: 'SQX.BACKTESTS.OUTCOME_IMPORTED',
  [BacktestImportOutcome.Unchanged]: 'SQX.BACKTESTS.OUTCOME_UNCHANGED',
  [BacktestImportOutcome.Replaced]: 'SQX.BACKTESTS.OUTCOME_REPLACED',
  [BacktestImportOutcome.Rejected]: 'SQX.BACKTESTS.OUTCOME_REJECTED',
};

export const BACKTEST_KIND_LABELS: Record<BacktestRunKind, string> = {
  [BacktestRunKind.Deploy]: 'SQX.BACKTESTS.KIND_DEPLOY',
  [BacktestRunKind.Evaluation]: 'SQX.BACKTESTS.KIND_EVALUATION',
};

export const BACKTEST_SEGMENT_LABELS: Record<BacktestSegment, string> = {
  [BacktestSegment.Unknown]: 'SQX.BACKTESTS.GROUP_RISK.SEGMENT_UNKNOWN',
  [BacktestSegment.InSample]: 'SQX.BACKTESTS.GROUP_RISK.SEGMENT_IN_SAMPLE',
  [BacktestSegment.OutOfSample]: 'SQX.BACKTESTS.GROUP_RISK.SEGMENT_OUT_OF_SAMPLE',
  [BacktestSegment.InSampleTest]: 'SQX.BACKTESTS.GROUP_RISK.SEGMENT_IN_SAMPLE_TEST',
};

/**
 * The label a WITHHELD figure renders instead of a number. There is no entry that renders as a
 * digit, and `None` is never reached by the withheld branch — it accompanies a figure that is
 * present.
 */
export const VAR_WITHHOLD_LABELS: Record<VarWithholdReason, string> = {
  [VarWithholdReason.None]: 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_NONE',
  [VarWithholdReason.NoSeries]: 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_NO_SERIES',
  [VarWithholdReason.InsufficientHistory]: 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_INSUFFICIENT_HISTORY',
  [VarWithholdReason.InsufficientNegativeObservations]:
    'SQX.BACKTESTS.GROUP_RISK.WITHHELD_INSUFFICIENT_NEGATIVE_OBSERVATIONS',
};

/** Each refusal keeps its own sentence — collapsing them would hide what the operator must fix. */
export const GROUP_RISK_STATUS_LABELS: Record<GroupRiskAnalysisStatus, string> = {
  [GroupRiskAnalysisStatus.Completed]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_COMPLETED',
  [GroupRiskAnalysisStatus.SegmentNotSpecified]:
    'SQX.BACKTESTS.GROUP_RISK.STATUS_SEGMENT_NOT_SPECIFIED',
  [GroupRiskAnalysisStatus.UnknownSegmentNotSelectable]:
    'SQX.BACKTESTS.GROUP_RISK.STATUS_UNKNOWN_SEGMENT',
  [GroupRiskAnalysisStatus.NoStrategiesRequested]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_NO_STRATEGIES',
  [GroupRiskAnalysisStatus.StrategyNotFound]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_STRATEGY_NOT_FOUND',
  [GroupRiskAnalysisStatus.InvalidLotGrid]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_INVALID_LOT_GRID',
  [GroupRiskAnalysisStatus.RunSegmentsDisagree]:
    'SQX.BACKTESTS.GROUP_RISK.STATUS_RUN_SEGMENTS_DISAGREE',
  [GroupRiskAnalysisStatus.NoEvidenceForSegment]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_NO_EVIDENCE',
  [GroupRiskAnalysisStatus.AmbiguousRunSelection]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_AMBIGUOUS_RUN',
  [GroupRiskAnalysisStatus.RiskNotEstimable]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_RISK_NOT_ESTIMABLE',
  [GroupRiskAnalysisStatus.NonUnitWeight]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_NON_UNIT_WEIGHT',
  [GroupRiskAnalysisStatus.HeterogeneousGroup]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_HETEROGENEOUS',
};

export const GROUP_RISK_MEMBER_STATUS_LABELS: Record<GroupRiskMemberStatus, string> = {
  [GroupRiskMemberStatus.Resolved]: 'SQX.BACKTESTS.GROUP_RISK.MEMBER_RESOLVED',
  [GroupRiskMemberStatus.RunSegmentsDisagree]:
    'SQX.BACKTESTS.GROUP_RISK.STATUS_RUN_SEGMENTS_DISAGREE',
  [GroupRiskMemberStatus.NoEvidenceForSegment]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_NO_EVIDENCE',
  [GroupRiskMemberStatus.AmbiguousRunSelection]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_AMBIGUOUS_RUN',
  [GroupRiskMemberStatus.RiskNotEstimable]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_RISK_NOT_ESTIMABLE',
  [GroupRiskMemberStatus.NonUnitWeight]: 'SQX.BACKTESTS.GROUP_RISK.STATUS_NON_UNIT_WEIGHT',
};

export const CALIBRATION_STATUS_LABELS: Record<CalibrationStatus, string> = {
  [CalibrationStatus.Calibrated]: 'SQX.BACKTESTS.CALIBRATION_STATUS_CALIBRATED',
  [CalibrationStatus.InsufficientSamples]: 'SQX.BACKTESTS.CALIBRATION_STATUS_INSUFFICIENT_SAMPLES',
  [CalibrationStatus.Inconsistent]: 'SQX.BACKTESTS.CALIBRATION_STATUS_INCONSISTENT',
};

@Injectable({ providedIn: 'root' })
export class BacktestService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly base = `${inject(API_BASE_URL)}/api/backtests`;

  /**
   * Imports the strategy's Deploy run: the trade list produced from the parameters actually
   * running. One file, one slot, one request — the strategy is in the URL, so nothing about the
   * file's name or contents decides where it lands.
   */
  importDeploy(strategyId: string, file: File): Observable<BacktestImportResultDto> {
    return this.postFile(`${this.strategyBase(strategyId)}/backtests/deploy`, file);
  }

  /** Imports the strategy's Evaluation run: the trade list produced from the PREVIOUS walk-forward window's parameters. */
  importEvaluation(strategyId: string, file: File): Observable<BacktestImportResultDto> {
    return this.postFile(`${this.strategyBase(strategyId)}/backtests/evaluation`, file);
  }

  /** Imports the strategy's walk-forward export, which owns the out-of-sample boundary date. */
  importWalkForward(strategyId: string, file: File): Observable<WalkForwardImportResultDto> {
    return this.postFile<WalkForwardImportResultDto>(
      `${this.strategyBase(strategyId)}/walk-forward`,
      file,
    );
  }

  /** Both run slots and the walk-forward export currently held by one strategy. */
  getStrategyBacktests(strategyId: string): Observable<StrategyBacktestsDto> {
    return this.http.get<StrategyBacktestsDto>(`${this.strategyBase(strategyId)}/backtests`);
  }

  private strategyBase(strategyId: string): string {
    return `${this.apiBase}/api/strategies/${strategyId}`;
  }

  /**
   * A file-level REJECTION is a 200 carrying a reason, not an HTTP failure — the server parsed the
   * file and has something specific to say about it, and that sentence is what the user needs. Only
   * a genuine transport or server fault becomes an error, and it is re-thrown as a stable i18n key
   * so the caller can render it through the `translate` pipe without further mapping.
   */
  private postFile<T = BacktestImportResultDto>(url: string, file: File): Observable<T> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http
      .post<T>(url, formData)
      .pipe(
        catchError((err: HttpErrorResponse) =>
          throwError(() => new Error('SQX.BACKTESTS.IMPORT_ERROR', { cause: err })),
        ),
      );
  }

  getRuns(page = 1, pageSize = 20): Observable<PagedResult<BacktestRunDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<BacktestRunDto>>(`${this.base}/runs`, { params });
  }

  getTradesByRun(
    runId: string,
    segment?: BacktestSegment,
    page = 1,
    pageSize = 50,
  ): Observable<PagedResult<BacktestTradeDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (segment !== undefined) params = params.set('segment', segment);
    return this.http.get<PagedResult<BacktestTradeDto>>(`${this.base}/runs/${runId}/trades`, {
      params,
    });
  }

  getCalibrations(): Observable<SymbolCalibrationDto[]> {
    return this.http.get<SymbolCalibrationDto[]>(`${this.base}/calibrations`);
  }

  /**
   * Correlation and VaR for ONE named group of strategies over ONE named sample.
   *
   * A REFUSAL is not a transport failure. The server answers 400/404/422 with the same
   * `GroupRiskAnalysisDto`, carrying the per-member evidence the operator has to act on, so those
   * bodies are unwrapped and passed through rather than swallowed into a generic error. Only a
   * genuine fault — no body, or one that is not an analysis — becomes an error, and it is re-thrown
   * as a stable i18n key.
   *
   * `segment` is only appended when it is defined. Sending `segment=0` would be asking for
   * `Unknown`, which is a DIFFERENT request from not having chosen one, and the server refuses each
   * for its own reason.
   */
  getGroupRisk(query: GroupRiskAnalysisQuery): Observable<GroupRiskAnalysisDto> {
    let params = new HttpParams()
      .set('initialCapital', query.initialCapital)
      .set('targetRiskPerTrade', query.targetRiskPerTrade);

    for (const id of query.strategyIds) params = params.append('strategyIds', id);
    if (query.segment !== undefined) params = params.set('segment', query.segment);
    if (query.runKind !== undefined) params = params.set('runKind', query.runKind);
    if (query.fundingService) params = params.set('fundingService', query.fundingService);

    return this.http.get<GroupRiskAnalysisDto>(`${this.base}/portfolio-risk`, { params }).pipe(
      catchError((err: HttpErrorResponse) => {
        const body = err.error as GroupRiskAnalysisDto | null;
        if (body && typeof body === 'object' && typeof body.status === 'number') return of(body);
        return throwError(
          () => new Error('SQX.BACKTESTS.GROUP_RISK.REQUEST_ERROR', { cause: err }),
        );
      }),
    );
  }
}
