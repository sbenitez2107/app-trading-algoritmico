import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { EMPTY, Subject, Subscription, catchError, map, switchMap, tap } from 'rxjs';
import {
  FtmoGroupCandidatesDto,
  FtmoGroupSimulationDto,
} from '../../../core/models/ftmo-group-simulation.model';
import {
  FtmoRequestError,
  FtmoSimulationService,
} from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { FtmoGroupFormComponent } from '../ftmo-group-form/ftmo-group-form.component';
import { FtmoGroupPickerComponent } from '../ftmo-group-picker/ftmo-group-picker.component';
import { FtmoGroupResultComponent } from '../ftmo-group-result/ftmo-group-result.component';
import {
  DEFAULT_GROUP_FORM,
  GroupFormValue,
  canRunGroup,
  needsFxInputs,
  pickDefaultAccountId,
  toGroupRequest,
  toWorstCaseReadout,
} from '../ftmo-group-simulation.mappers';
import {
  DeepLinkNotice,
  DeepLinkParams,
  applyDeepLink,
  readDeepLink,
} from '../ftmo-group-search.deeplink.mappers';
import { toGroupResultVm } from '../ftmo-group-simulation.result.mappers';

/**
 * Container of the FTMO group simulation screen: title, always-visible disclosure, the account select
 * the strategy picker, the parameter form and the result area (reused run panels, refusals, window). A run is explicit: only the form's Run starts a request, a
 * second one is blocked while it is in flight, and leaving the screen cancels it. Candidates reload through `switchMap` when the account changes, which also
 * drops a stale in-flight response and clears the selection. Selecting never issues a run request.
 *
 * A result describes exactly the inputs it was run with (F3a RELIABILITY-002). Any change to them (account,
 * member selection or any form value) cancels an in-flight run and clears `result` and `runError`. The DTO
 * does not echo the risk or capital, so the readout cannot be rebuilt from the result alone; clearing it is
 * the only way the observed `peak x risk` never mixes a stale peak with a new input.
 */
interface PendingLink {
  link: DeepLinkParams;
  accountKnown: boolean;
}

@Component({
  selector: 'app-ftmo-group-simulation-page',
  standalone: true,
  imports: [
    TranslateModule,
    FtmoGroupPickerComponent,
    FtmoGroupFormComponent,
    FtmoGroupResultComponent,
  ],
  templateUrl: './ftmo-group-simulation-page.component.html',
  styleUrl: './ftmo-group-simulation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupSimulationPageComponent {
  private readonly accountService = inject(TradingAccountService);
  private readonly simulationService = inject(FtmoSimulationService);
  private readonly destroyRef = inject(DestroyRef);
  /** Optional: the specs of this page provide no router. */
  private readonly route = inject(ActivatedRoute, { optional: true });
  /** The deep link, applied exactly once (on the first candidates load); never auto-runs. */
  private pendingLink: PendingLink | null = null;
  private readonly accountChanges = new Subject<string>();
  private runSubscription: Subscription | null = null;

  readonly accounts = signal<{ id: string; name: string }[]>([]);
  readonly accountsLoaded = signal(false);
  readonly accountId = signal<string | null>(null);
  readonly candidates = signal<FtmoGroupCandidatesDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<FtmoRequestError | null>(null);
  readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  readonly accountsError = signal(false);
  readonly form = signal<GroupFormValue>(DEFAULT_GROUP_FORM);
  readonly running = signal(false);
  readonly result = signal<FtmoGroupSimulationDto | null>(null);
  readonly runError = signal<FtmoRequestError | null>(null);
  readonly deepLinkNotices = signal<DeepLinkNotice[]>([]);

  /** The result area's view model; `null` before the first run and after any invalidation. */
  readonly resultVm = computed(() => {
    const result = this.result();
    return result === null ? null : toGroupResultVm(result);
  });
  readonly showFx = computed(() =>
    needsFxInputs(this.candidates()?.candidates ?? [], this.selectedIds()),
  );
  readonly canRun = computed(() =>
    canRunGroup(this.form(), this.selectedIds().size, this.running()),
  );
  /** Peak per SUCCESSFUL kind (diagnostics present), each labelled with its own kind. */
  readonly readout = computed(() => {
    const result = this.result();
    const peaks = (result?.kinds ?? [])
      .filter((k) => k.diagnostics !== null)
      .map((k) => ({ kind: k.kind, peak: k.diagnostics!.peak.peakConcurrentOpen }));
    const form = this.form();
    return toWorstCaseReadout(
      this.selectedIds().size,
      form.targetRiskPerTrade,
      form.initialCapital,
      result?.dailyLossLimitPct ?? null,
      peaks,
    );
  });

  constructor() {
    this.accountChanges
      .pipe(
        tap(() => {
          this.selectedIds.set(new Set());
          this.candidates.set(null);
          this.error.set(null);
          this.loading.set(true);
        }),
        switchMap((id) => {
          // The link belongs to the first load attempt only; a failure must not defer it.
          const link = this.pendingLink;
          this.pendingLink = null;
          return this.simulationService.getGroupCandidates(id).pipe(
            map((response) => ({ response, link })),
            catchError((err: FtmoRequestError) => {
              this.error.set(err);
              this.loading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ response, link }) => {
        this.candidates.set(response);
        this.loading.set(false);
        this.applyLink(link, response);
      });

    this.accountService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (accounts) => {
          this.accounts.set(accounts.map((a) => ({ id: a.id, name: a.name })));
          this.accountsLoaded.set(true);
          const link = readDeepLink(this.route?.snapshot?.queryParamMap ?? null);
          const accountKnown =
            link?.account != null && this.accounts().some((a) => a.id === link.account);
          this.pendingLink = link === null ? null : { link, accountKnown };
          const id = accountKnown ? link!.account! : pickDefaultAccountId(this.accounts());
          if (id !== null) this.selectAccount(id);
        },
        // A failed load must not leave a blank page (F2 RELIABILITY-001).
        error: () => this.accountsError.set(true),
      });
  }

  /** Preselects from the link once; later edits and account changes are never overwritten. */
  private applyLink(pending: PendingLink | null, response: FtmoGroupCandidatesDto): void {
    if (pending === null) return;
    const applied = applyDeepLink(
      pending.link,
      response.candidates.map((c) => c.strategyId),
      response.maxMembers,
      pending.accountKnown,
      this.form(),
    );
    this.form.set(applied.form);
    this.selectedIds.set(new Set(applied.selected));
    this.deepLinkNotices.set(applied.notices);
  }

  selectAccount(id: string): void {
    this.invalidateRun();
    this.accountId.set(id);
    this.accountChanges.next(id);
  }

  onSelectionChange(selected: ReadonlySet<string>): void {
    this.invalidateRun();
    this.selectedIds.set(selected);
  }

  onFormChange(value: GroupFormValue): void {
    this.invalidateRun();
    this.form.set(value);
  }

  /** Explicit Run only: `running()` blocks a second request, destroy cancels, an error shows no result. */
  run(): void {
    const request = toGroupRequest(this.form(), [...this.selectedIds()], this.showFx());
    if (this.running() || request === null) return;
    this.invalidateRun();
    this.running.set(true);
    this.runSubscription = this.simulationService
      .simulateGroup(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (dto) => {
          this.result.set(dto);
          this.running.set(false);
        },
        error: (err: FtmoRequestError) => {
          this.runError.set(err);
          this.running.set(false);
        },
      });
  }

  /** Cancels an in-flight run and drops the result and error, which no longer describe the inputs. */
  private invalidateRun(): void {
    this.runSubscription?.unsubscribe();
    this.runSubscription = null;
    this.running.set(false);
    this.result.set(null);
    this.runError.set(null);
  }

  /** Same mapping as the modal: the translation key lives under `FTMO_SIMULATION`. */
  errorKey(e: FtmoRequestError): string {
    return `FTMO_SIMULATION.${e.key}`;
  }
}
