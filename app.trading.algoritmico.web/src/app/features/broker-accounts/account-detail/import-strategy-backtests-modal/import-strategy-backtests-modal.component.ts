import {
  Component,
  ChangeDetectionStrategy,
  EventEmitter,
  Output,
  inject,
  input,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import {
  BacktestService,
  BacktestImportOutcome,
  BacktestImportResultDto,
  BACKTEST_OUTCOME_LABELS,
  WalkForwardImportResultDto,
} from '../../../../core/services/backtest.service';
import { PlatformType } from '../../../../core/models/platform-type.model';

/** The three artifacts a strategy can own. The slot the user picks IS the declaration. */
export type BacktestSlot = 'deploy' | 'evaluation' | 'walkForward';

type SlotMap<T> = Record<BacktestSlot, T>;

const EMPTY: SlotMap<null> = { deploy: null, evaluation: null, walkForward: null };

/**
 * Imports the three artifacts a strategy can own, from the strategy's own row.
 *
 * THREE LABELLED SLOTS, not one drop zone. A Deploy trade list and an Evaluation trade list are the
 * same 16-column document produced by the same tool from different parameter sets — nothing in the
 * file distinguishes them, so any single-input design would have to GUESS which one it received,
 * and a wrong guess silently turns in-sample results into an out-of-sample claim. Naming the slot
 * moves that decision to the only party who actually knows: the person who exported the file.
 *
 * What CAN be detected is the wrong KIND of document, because a walk-forward export has a
 * completely different header. That is validated server-side per slot, and the reason is shown
 * against the slot it belongs to.
 *
 * Each slot is independently optional and independently re-importable, so "just refresh the WF
 * export" and "I only have the deploy run so far" both fall out with no extra UI.
 */
@Component({
  selector: 'app-import-strategy-backtests-modal',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './import-strategy-backtests-modal.component.html',
  styleUrl: './import-strategy-backtests-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportStrategyBacktestsModalComponent {
  readonly strategyId = input.required<string>();
  readonly strategyName = input<string>('');

  /** Emits true when at least one slot actually landed, so the caller knows whether to refresh. */
  @Output() readonly closed = new EventEmitter<boolean>();

  private readonly backtestService = inject(BacktestService);

  readonly outcomeLabels = BACKTEST_OUTCOME_LABELS;
  readonly Outcome = BacktestImportOutcome;
  readonly pending = signal(0);

  private readonly files = signal<SlotMap<File | null>>({ ...EMPTY });
  private readonly results = signal<SlotMap<BacktestImportResultDto | null>>({ ...EMPTY });
  private readonly errors = signal<SlotMap<string | null>>({ ...EMPTY });

  readonly walkForwardResult = signal<WalkForwardImportResultDto | null>(null);

  /**
   * One selector for the whole submission, not one per slot (design D6 "Where"): both trade
   * lists come from one SQX build. `null` by default and rendered as a visible "Not declared"
   * option — never `0 as PlatformType`, the exact hazard at `account-form.component.ts:58` this
   * field must not repeat.
   */
  readonly sourcePlatform = signal<PlatformType | null>(null);
  readonly PlatformType = PlatformType;

  readonly slots: readonly {
    key: BacktestSlot;
    inputId: string;
    labelKey: string;
    hintKey: string;
    namingKey: string;
  }[] = [
    {
      key: 'deploy',
      inputId: 'slot-deploy',
      labelKey: 'SQX.BACKTESTS.SLOT_DEPLOY',
      hintKey: 'SQX.BACKTESTS.SLOT_DEPLOY_HINT',
      namingKey: 'SQX.BACKTESTS.SLOT_DEPLOY_NAMING',
    },
    {
      key: 'evaluation',
      inputId: 'slot-evaluation',
      labelKey: 'SQX.BACKTESTS.SLOT_EVALUATION',
      hintKey: 'SQX.BACKTESTS.SLOT_EVALUATION_HINT',
      namingKey: 'SQX.BACKTESTS.SLOT_EVALUATION_NAMING',
    },
    {
      key: 'walkForward',
      inputId: 'slot-walk-forward',
      labelKey: 'SQX.BACKTESTS.SLOT_WALK_FORWARD',
      hintKey: 'SQX.BACKTESTS.SLOT_WALK_FORWARD_HINT',
      namingKey: 'SQX.BACKTESTS.SLOT_WALK_FORWARD_NAMING',
    },
  ];

  selectedFile(slot: BacktestSlot): File | null {
    return this.files()[slot];
  }

  slotResult(slot: BacktestSlot): BacktestImportResultDto | null {
    return this.results()[slot];
  }

  slotError(slot: BacktestSlot): string | null {
    return this.errors()[slot];
  }

  hasAnyFile(): boolean {
    return Object.values(this.files()).some((f) => f !== null);
  }

  /** Applies to `deploy`/`evaluation` only (design D6 "Scope") — a walk-forward export has no such column. */
  hasQueuedTradeListFile(): boolean {
    const { deploy, evaluation } = this.files();
    return deploy !== null || evaluation !== null;
  }

  /** `''` (the "Not declared" option's value) maps back to `null`, never to `PlatformType.MT4`. */
  onSourcePlatformChange(rawValue: string): void {
    this.sourcePlatform.set(rawValue === '' ? null : (Number(rawValue) as PlatformType));
  }

  onFileSelected(slot: BacktestSlot, file: File | null): void {
    this.files.update((current) => ({ ...current, [slot]: file }));
    // A new choice invalidates whatever the previous one reported for THIS slot only.
    this.results.update((current) => ({ ...current, [slot]: null }));
    this.errors.update((current) => ({ ...current, [slot]: null }));
  }

  onFileInput(slot: BacktestSlot, event: Event): void {
    const input = event.target as HTMLInputElement;
    this.onFileSelected(slot, input.files?.[0] ?? null);
  }

  /**
   * Submits every filled slot, each as its own request. They are independent on purpose: one slot
   * being refused must not withhold the other two, exactly as the server keeps each import in its
   * own transaction.
   */
  submit(): void {
    const { deploy, evaluation, walkForward } = this.files();
    const strategyId = this.strategyId();
    // `?? undefined`, never `?? null`: an omitted platform must send NOTHING over the wire
    // (design D6/D7), which `BacktestService.postFile` only does for `undefined`.
    const sourcePlatform = this.sourcePlatform() ?? undefined;

    if (deploy) {
      this.track('deploy');
      this.backtestService.importDeploy(strategyId, deploy, sourcePlatform).subscribe({
        next: (result) => this.settle('deploy', result),
        error: (err: Error) => this.fail('deploy', err),
      });
    }

    if (evaluation) {
      this.track('evaluation');
      this.backtestService.importEvaluation(strategyId, evaluation, sourcePlatform).subscribe({
        next: (result) => this.settle('evaluation', result),
        error: (err: Error) => this.fail('evaluation', err),
      });
    }

    if (walkForward) {
      this.track('walkForward');
      this.backtestService.importWalkForward(strategyId, walkForward).subscribe({
        next: (result) => {
          this.walkForwardResult.set(result);
          this.settle('walkForward', {
            fileName: result.fileName,
            outcome: result.outcome,
            tradeCount: null,
            rejectedRowCount: null,
            reason: result.reason,
          });
        },
        error: (err: Error) => this.fail('walkForward', err),
      });
    }
  }

  private track(slot: BacktestSlot): void {
    this.pending.update((n) => n + 1);
    this.errors.update((current) => ({ ...current, [slot]: null }));
  }

  private settle(slot: BacktestSlot, result: BacktestImportResultDto): void {
    this.pending.update((n) => Math.max(0, n - 1));
    this.results.update((current) => ({ ...current, [slot]: result }));
  }

  private fail(slot: BacktestSlot, err: Error): void {
    this.pending.update((n) => Math.max(0, n - 1));
    this.errors.update((current) => ({ ...current, [slot]: err.message }));
  }

  /** True when at least one slot actually changed stored data — the caller refreshes only then. */
  private anythingLanded(): boolean {
    return Object.values(this.results()).some(
      (r) => r !== null && r.outcome !== BacktestImportOutcome.Rejected,
    );
  }

  onClose(): void {
    this.closed.emit(this.anythingLanded());
  }
}
