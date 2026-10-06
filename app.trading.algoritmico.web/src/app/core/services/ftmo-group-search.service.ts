import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../app.config';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRequest,
  FtmoGroupSearchStartResponseDto,
} from '../models/ftmo-group-search.model';

/** A mapped failure, ready for the `translate` pipe (same shape as `FtmoRequestError`). */
export interface FtmoGroupSearchRequestError {
  key: string;
  detail: string | null;
}

/** The result of a start: a new job, or the id of the job already running (409). */
export type FtmoGroupSearchStartOutcome =
  | { outcome: 'started'; jobId: string; job: FtmoGroupSearchJobDto }
  | { outcome: 'alreadyRunning'; runningJobId: string };

const LOST_KEY = 'SIMULATOR.FTMO_SEARCH.ERRORS.LOST';

function failure(key: string, detail: string | null = null): Observable<never> {
  return throwError(() => ({ key, detail }) satisfies FtmoGroupSearchRequestError);
}

/**
 * Maps an HTTP failure into a stable, translatable error: 400 is the backend's own validation refusal
 * (body `{ message }`), 404 means the job is gone (the API restarted), anything else is a generic failure.
 */
function mapSearchError(err: HttpErrorResponse): Observable<never> {
  if (err.status === 400) {
    const body = err.error as { message?: string } | null;
    return failure(
      'ERRORS.INVALID_QUERY',
      body && typeof body.message === 'string' ? body.message : null,
    );
  }
  if (err.status === 404) return failure(LOST_KEY);
  return failure('ERRORS.REQUEST_FAILED');
}

/** HTTP client of the FTMO group search job (design D7): start, poll, re-attach and cancel. */
@Injectable({ providedIn: 'root' })
export class FtmoGroupSearchService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  private get baseUrl(): string {
    return `${this.apiUrl}/api/ftmo-simulations/group-search`;
  }

  /** 202 starts, 409 yields the running id (not an error), 400 carries the server message. */
  startGroupSearch(body: FtmoGroupSearchRequest): Observable<FtmoGroupSearchStartOutcome> {
    return this.http.post<FtmoGroupSearchStartResponseDto>(this.baseUrl, body).pipe(
      map(
        (response): FtmoGroupSearchStartOutcome => ({
          outcome: 'started',
          jobId: response.jobId,
          job: response.job,
        }),
      ),
      catchError((err: HttpErrorResponse) => {
        if (err.status === 409) {
          const running = (err.error as { runningJobId?: string } | null)?.runningJobId;
          if (typeof running === 'string') {
            return of<FtmoGroupSearchStartOutcome>({
              outcome: 'alreadyRunning',
              runningJobId: running,
            });
          }
        }
        return mapSearchError(err);
      }),
    );
  }

  /** One poll; a 404 (job lost on restart) maps to the lost-state error. */
  getGroupSearch(jobId: string): Observable<FtmoGroupSearchJobDto> {
    return this.http
      .get<FtmoGroupSearchJobDto>(`${this.baseUrl}/${jobId}`)
      .pipe(catchError(mapSearchError));
  }

  /** The retained job (running or last terminal): 200 gives it, 204 maps to `null`. */
  getCurrentGroupSearch(): Observable<FtmoGroupSearchJobDto | null> {
    return this.http.get<FtmoGroupSearchJobDto | null>(`${this.baseUrl}/current`).pipe(
      map((job) => job ?? null),
      catchError(mapSearchError),
    );
  }

  /** Explicit cancel: 204 completes; idempotent on a terminal job; 404 maps to the lost state. */
  cancelGroupSearch(jobId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${jobId}`).pipe(catchError(mapSearchError));
  }
}
