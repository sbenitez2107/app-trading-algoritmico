import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
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
}
