import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../../app.config';
import { FtmoGroupCandidatesDto } from '../models/ftmo-group-simulation.model';
import { FtmoMultiStartDto, FtmoSimulationQuery } from '../models/ftmo-simulation.model';

/** A mapped Run-request failure, ready for the `translate` pipe (design.md AD10). */
export interface FtmoRequestError {
  key: string;
  detail: string | null;
}

/**
 * Every required field via `.set(k, String(v))`; `fxLow`/`fxHigh` are appended only when
 * `!== null` and `!== undefined` — `sizeDecimals=0` is a legal value and must never be dropped by
 * a truthy check (hard rule 2, mirrors `StrategyBacktestsController.TryValidateFtmoBreachQuery`'s
 * required-parameter set).
 */
function buildFtmoQueryParams(query: FtmoSimulationQuery): HttpParams {
  let params = new HttpParams()
    .set('broker', query.broker)
    .set('sqxSymbol', query.sqxSymbol)
    .set('initialCapital', String(query.initialCapital))
    .set('targetRiskPerTrade', String(query.targetRiskPerTrade))
    .set('sizeDecimals', String(query.sizeDecimals))
    .set('step', String(query.step))
    .set('minLot', String(query.minLot))
    .set('maxLots', String(query.maxLots));

  if (query.fxLow !== null && query.fxLow !== undefined) {
    params = params.set('fxLow', String(query.fxLow));
  }
  if (query.fxHigh !== null && query.fxHigh !== undefined) {
    params = params.set('fxHigh', String(query.fxHigh));
  }

  return params;
}

/**
 * Maps an HTTP failure into a stable, translatable `FtmoRequestError` (design.md AD10). A 400 is the
 * backend's own query-validation refusal (`StrategyBacktestsController.TryValidateFtmoBreachQuery`,
 * body `{ message }`); everything else (network failure, 5xx, request aborted) becomes a generic
 * request-failed error with no detail to show.
 */
function mapFtmoRequestError(err: HttpErrorResponse): Observable<never> {
  if (err.status === 400) {
    const body = err.error as { message?: string } | null;
    const detail = body && typeof body.message === 'string' ? body.message : null;
    return throwError(() => ({ key: 'ERRORS.INVALID_QUERY', detail }) satisfies FtmoRequestError);
  }
  return throwError(
    () => ({ key: 'ERRORS.REQUEST_FAILED', detail: null }) satisfies FtmoRequestError,
  );
}

/**
 * `FtmoSimulationModalComponent`'s HTTP client (design.md AD2). Not yet imported by any component —
 * consumed starting in PR1b's mappers and PR1d's container.
 */
@Injectable({ providedIn: 'root' })
export class FtmoSimulationService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  /** The FTMO multi-start replay for every held run of one strategy (design.md Data Flow). */
  getMultiStart(strategyId: string, query: FtmoSimulationQuery): Observable<FtmoMultiStartDto> {
    const params = buildFtmoQueryParams(query);
    return this.http
      .get<FtmoMultiStartDto>(
        `${this.apiUrl}/api/strategies/${strategyId}/ftmo-breach/multi-start`,
        {
          params,
        },
      )
      .pipe(catchError(mapFtmoRequestError));
  }

  /** The picker's candidates for ONE account, with the server's member cap (design D8). */
  getGroupCandidates(tradingAccountId: string): Observable<FtmoGroupCandidatesDto> {
    const params = new HttpParams().set('tradingAccountId', tradingAccountId);
    return this.http
      .get<FtmoGroupCandidatesDto>(`${this.apiUrl}/api/ftmo-simulations/candidates`, { params })
      .pipe(catchError(mapFtmoRequestError));
  }
}
