import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import {
  EMPTY,
  Subject,
  Subscription,
  catchError,
  defer,
  repeat,
  switchMap,
  takeWhile,
  tap,
} from 'rxjs';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import { FtmoGroupCandidatesDto } from '../../../core/models/ftmo-group-simulation.model';
import {
  FtmoGroupSearchRequestError,
  FtmoGroupSearchService,
} from '../../../core/services/ftmo-group-search.service';
import {
  FtmoRequestError,
  FtmoSimulationService,
} from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { DeepLinkSource, buildDeepLink } from '../ftmo-group-search.deeplink.mappers';
import { pickDefaultAccountId } from '../ftmo-group-simulation.mappers';
import {
  DEFAULT_SEARCH_FORM,
  SearchFormValue,
  canStartSearch,
  fractionToPercent,
  isRunningStatus,
  isTerminalStatus,
  poolNeedsFx,
  searchErrorKey,
  toSearchRequest,
} from '../ftmo-group-search.mappers';
import { FtmoSearchFormComponent } from '../ftmo-search-form/ftmo-search-form.component';
import { FtmoSearchProgressComponent } from '../ftmo-search-progress/ftmo-search-progress.component';
import { FtmoSearchScatterComponent } from '../ftmo-search-scatter/ftmo-search-scatter.component';
import { FtmoSearchTableComponent } from '../ftmo-search-table/ftmo-search-table.component';
import { DEFAULT_CEILING_PERCENT } from '../ftmo-group-search.table.mappers';

/** Poll period of a running job (design D10). */
export const SEARCH_POLL_MS = 1000;

type SearchNotice = 'alreadyRunning' | 'lost';

/**
 * Container of the FTMO group search screen: title, always-visible disclosure, the form, the progress of
 * the job and the ranked results table (whose ceiling defaults to the one the search was started with).
 *
 * Polling is `defer(get)` -> `repeat({ delay: 1000 })` -> `takeWhile(!terminal, inclusive)` ->
 * `takeUntilDestroyed`: each poll starts 1 s after the previous response, so requests never overlap
 * and a slow GET is never cancelled. Leaving the screen stops polling ONLY: the job keeps running on the backend and
 * `GET current` re-attaches on init. The sole DELETE path is the explicit Cancel click.
 */
@Component({
  selector: 'app-ftmo-group-search-page',
  imports: [
    TranslateModule,
    FtmoSearchFormComponent,
    FtmoSearchProgressComponent,
    FtmoSearchScatterComponent,
    FtmoSearchTableComponent,
  ],
  templateUrl: './ftmo-group-search-page.component.html',
  styleUrl: './ftmo-group-search-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupSearchPageComponent {
  private readonly accountService = inject(TradingAccountService);
  private readonly simulationService = inject(FtmoSimulationService);
  private readonly searchService = inject(FtmoGroupSearchService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly accountChanges = new Subject<string>();
  private pollSubscription: Subscription | null = null;

  readonly accounts = signal<{ id: string; name: string }[]>([]);
  readonly accountsLoaded = signal(false);
  readonly accountsError = signal(false);
  readonly accountId = signal<string | null>(null);
  readonly candidates = signal<FtmoGroupCandidatesDto | null>(null);
  readonly candidatesError = signal<FtmoRequestError | null>(null);
  readonly form = signal<SearchFormValue>(DEFAULT_SEARCH_FORM);
  readonly job = signal<FtmoGroupSearchJobDto | null>(null);
  readonly starting = signal(false);
  readonly notice = signal<SearchNotice | null>(null);
  readonly error = signal<FtmoGroupSearchRequestError | null>(null);
  /** The ceiling (percent) of the shown job's own request; the default when the request carries none. */
  readonly requestedCeilingPercent = computed(() => {
    const ceiling = this.job()?.request.eliminationCeiling ?? null;
    return ceiling === null ? DEFAULT_CEILING_PERCENT : fractionToPercent(ceiling);
  });

  readonly running = computed(() => {
    const job = this.job();
    return this.starting() || (job !== null && isRunningStatus(job.status));
  });
  readonly maxMembers = computed(() => this.candidates()?.maxMembers ?? 0);
  readonly showFx = computed(() => poolNeedsFx(this.candidates()?.candidates ?? []));
  readonly canStart = computed(
    () =>
      this.accountId() !== null &&
      canStartSearch(this.form(), this.maxMembers(), this.running(), this.showFx()),
  );

  constructor() {
    this.accountChanges
      .pipe(
        tap(() => {
          this.candidates.set(null);
          this.candidatesError.set(null);
        }),
        switchMap((id) =>
          this.simulationService.getGroupCandidates(id).pipe(
            catchError((err: FtmoRequestError) => {
              this.candidatesError.set(err);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((response) => this.candidates.set(response));

    this.accountService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (accounts) => {
          this.accounts.set(accounts.map((a) => ({ id: a.id, name: a.name })));
          this.accountsLoaded.set(true);
          const id = pickDefaultAccountId(this.accounts());
          if (id !== null) this.selectAccount(id);
        },
        error: () => this.accountsError.set(true),
      });

    // Re-attach: a job left running (or the last terminal one) is shown again; 204 leaves the empty form.
    this.searchService
      .getCurrentGroupSearch()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (job) => {
          if (job === null) return;
          this.job.set(job);
          if (isRunningStatus(job.status)) this.startPolling(job.jobId);
        },
        error: (err: FtmoGroupSearchRequestError) => this.error.set(err),
      });
  }

  selectAccount(id: string): void {
    this.accountId.set(id);
    this.accountChanges.next(id);
  }

  onFormChange(value: SearchFormValue): void {
    this.form.set(value);
  }

  /** Explicit Start: 202 attaches, 409 attaches to the running id with a notice, 400 shows the error. */
  start(): void {
    const request = toSearchRequest(
      this.accountId(),
      this.form(),
      this.maxMembers(),
      this.showFx(),
    );
    if (this.running() || request === null) return;
    this.error.set(null);
    this.notice.set(null);
    this.starting.set(true);
    this.searchService
      .startGroupSearch(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (outcome) => {
          this.starting.set(false);
          if (outcome.outcome === 'started') {
            this.job.set(outcome.job);
            if (isRunningStatus(outcome.job.status)) this.startPolling(outcome.jobId);
          } else {
            this.notice.set('alreadyRunning');
            this.startPolling(outcome.runningJobId);
          }
        },
        error: (err: FtmoGroupSearchRequestError) => {
          this.starting.set(false);
          this.error.set(err);
        },
      });
  }

  /**
   * The only DELETE path: stops polling and marks the job cancelled locally, then fetches the final
   * snapshot ONCE so the partial rows and `notComputed` come from the server. If that fetch fails, the
   * local Cancelled state (with the last polled rows) is kept and no error is shown.
   */
  cancel(): void {
    const job = this.job();
    if (job === null) return;
    const jobId = job.jobId;
    this.searchService
      .cancelGroupSearch(jobId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.stopPolling();
          this.job.update((current) =>
            current === null ? null : { ...current, status: FtmoGroupSearchStatus.Cancelled },
          );
          this.searchService
            .getGroupSearch(jobId)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              // Cancellation is cooperative: a snapshot still reporting Running is shown as Cancelled.
              next: (snapshot) =>
                this.job.set(
                  isRunningStatus(snapshot.status)
                    ? { ...snapshot, status: FtmoGroupSearchStatus.Cancelled }
                    : snapshot,
                ),
              error: () => undefined,
            });
        },
        error: (err: FtmoGroupSearchRequestError) => this.onPollError(err),
      });
  }

  /** Opens a ranked group in the group simulator. Navigation only: the group page never auto-runs. */
  openInGroup(row: FtmoGroupSearchRowDto): void {
    const request = this.job()?.request;
    if (request === undefined) return;
    const source: DeepLinkSource = {
      accountId: request.tradingAccountId,
      initialCapital: request.initialCapital,
      targetRiskPerTrade: request.targetRiskPerTrade,
      fxLow: request.fxLow,
      fxHigh: request.fxHigh,
    };
    const target = buildDeepLink(row, source);
    void this.router.navigate(target.path, { queryParams: target.queryParams });
  }

  errorKey(e: FtmoGroupSearchRequestError): string {
    return searchErrorKey(e.key);
  }

  private startPolling(jobId: string): void {
    this.stopPolling();
    this.pollSubscription = defer(() => this.searchService.getGroupSearch(jobId))
      .pipe(
        repeat({ delay: SEARCH_POLL_MS }),
        takeWhile((job) => !isTerminalStatus(job.status), true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (job) => this.job.set(job),
        error: (err: FtmoGroupSearchRequestError) => this.onPollError(err),
      });
  }

  private stopPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = null;
  }

  /** A lost job (404) clears the display and enables Start; any other failure is shown as an error. */
  private onPollError(err: FtmoGroupSearchRequestError): void {
    this.stopPolling();
    if (searchErrorKey(err.key) === 'SIMULATOR.FTMO_SEARCH.ERRORS.LOST') {
      this.job.set(null);
      this.notice.set('lost');
    } else {
      this.error.set(err);
    }
  }
}
