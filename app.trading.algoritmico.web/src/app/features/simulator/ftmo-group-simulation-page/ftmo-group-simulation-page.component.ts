import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { EMPTY, Subject, catchError, switchMap, tap } from 'rxjs';
import { FtmoGroupCandidatesDto } from '../../../core/models/ftmo-group-simulation.model';
import {
  FtmoRequestError,
  FtmoSimulationService,
} from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { FtmoGroupPickerComponent } from '../ftmo-group-picker/ftmo-group-picker.component';
import { pickDefaultAccountId } from '../ftmo-group-simulation.mappers';

/**
 * Container of the FTMO group simulation screen: title, always-visible disclosure, the account select
 * and the strategy picker. Candidates reload through `switchMap` when the account changes, which also
 * drops a stale in-flight response and clears the selection. Selecting never issues a run request.
 */
@Component({
  selector: 'app-ftmo-group-simulation-page',
  standalone: true,
  imports: [TranslateModule, FtmoGroupPickerComponent],
  templateUrl: './ftmo-group-simulation-page.component.html',
  styleUrl: './ftmo-group-simulation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupSimulationPageComponent {
  private readonly accountService = inject(TradingAccountService);
  private readonly simulationService = inject(FtmoSimulationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly accountChanges = new Subject<string>();

  readonly accounts = signal<{ id: string; name: string }[]>([]);
  readonly accountsLoaded = signal(false);
  readonly accountId = signal<string | null>(null);
  readonly candidates = signal<FtmoGroupCandidatesDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<FtmoRequestError | null>(null);
  readonly selectedIds = signal<ReadonlySet<string>>(new Set());

  constructor() {
    this.accountChanges
      .pipe(
        tap(() => {
          this.selectedIds.set(new Set());
          this.candidates.set(null);
          this.error.set(null);
          this.loading.set(true);
        }),
        switchMap((id) =>
          this.simulationService.getGroupCandidates(id).pipe(
            catchError((err: FtmoRequestError) => {
              this.error.set(err);
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((response) => {
        this.candidates.set(response);
        this.loading.set(false);
      });

    this.accountService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((accounts) => {
        this.accounts.set(accounts.map((a) => ({ id: a.id, name: a.name })));
        this.accountsLoaded.set(true);
        const id = pickDefaultAccountId(this.accounts());
        if (id !== null) this.selectAccount(id);
      });
  }

  selectAccount(id: string): void {
    this.accountId.set(id);
    this.accountChanges.next(id);
  }

  onSelectionChange(selected: ReadonlySet<string>): void {
    this.selectedIds.set(selected);
  }

  /** Same mapping as the modal: the translation key lives under `FTMO_SIMULATION`. */
  errorKey(e: FtmoRequestError): string {
    return `FTMO_SIMULATION.${e.key}`;
  }
}
