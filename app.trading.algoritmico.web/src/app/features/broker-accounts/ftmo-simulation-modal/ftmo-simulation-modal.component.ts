import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  input,
  output,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import {
  FtmoSimulationService,
  FtmoRequestError,
} from '../../../core/services/ftmo-simulation.service';
import {
  FtmoMultiStartDto,
  FtmoSimulationQuery,
  IMOX_RETESTER_LOT_GRID,
} from '../../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { toPanels, FtmoRunPanelVm } from './ftmo-simulation.mappers';
import { FtmoRunPanelComponent } from './ftmo-run-panel/ftmo-run-panel.component';

/** `!== null`/`!== undefined` and finite — `0` is a legal value (hard rule 2). */
function isPresentNumber(value: number | null): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

/**
 * Container modal (design.md AD1, AD5, AD6, AD9, AD10). Opened from `account-detail` via `@if`,
 * which destroys it on close and cancels any in-flight request through `takeUntilDestroyed`.
 */
@Component({
  selector: 'app-ftmo-simulation-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, FtmoRunPanelComponent],
  templateUrl: './ftmo-simulation-modal.component.html',
  styleUrl: './ftmo-simulation-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoSimulationModalComponent implements OnInit {
  readonly strategyId = input.required<string>();
  readonly strategyName = input.required<string>();
  readonly symbol = input.required<string | null>();
  readonly broker = input.required<string | null>();
  readonly closed = output<void>();

  private readonly ftmoService = inject(FtmoSimulationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly brokerValue = signal('');
  readonly sqxSymbol = signal('');
  readonly initialCapital = signal<number | null>(10000);
  readonly targetRiskPerTrade = signal<number | null>(null);
  readonly sizeDecimals = signal<number | null>(IMOX_RETESTER_LOT_GRID.sizeDecimals);
  readonly step = signal<number | null>(IMOX_RETESTER_LOT_GRID.step);
  readonly minLot = signal<number | null>(IMOX_RETESTER_LOT_GRID.minLot);
  readonly maxLots = signal<number | null>(IMOX_RETESTER_LOT_GRID.maxLots);
  readonly fxLow = signal<number | null>(null);
  readonly fxHigh = signal<number | null>(null);

  readonly running = signal(false);
  readonly result = signal<FtmoMultiStartDto | null>(null);
  readonly error = signal<FtmoRequestError | null>(null);
  private lastQuery: FtmoSimulationQuery | null = null;

  readonly runKinds: BacktestRunKind[] = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

  readonly panels = computed<FtmoRunPanelVm[]>(() => toPanels(this.result()));

  /** All 8 required fields per AD6, `!running()`. `sizeDecimals=0` counts as present (hard rule 2). */
  readonly canRun = computed(() => {
    if (this.running()) {
      return false;
    }
    return (
      this.brokerValue().trim() !== '' &&
      this.sqxSymbol().trim() !== '' &&
      isPresentNumber(this.initialCapital()) &&
      isPresentNumber(this.targetRiskPerTrade()) &&
      isPresentNumber(this.sizeDecimals()) &&
      isPresentNumber(this.step()) &&
      isPresentNumber(this.minLot()) &&
      isPresentNumber(this.maxLots())
    );
  });

  ngOnInit(): void {
    this.brokerValue.set(this.broker() ?? '');
    this.sqxSymbol.set(this.symbol() ?? '');
  }

  panelFor(kind: BacktestRunKind): FtmoRunPanelVm | null {
    return this.panels().find((p) => p.kind === kind) ?? null;
  }

  errorFullKey(err: FtmoRequestError): string {
    return `FTMO_SIMULATION.${err.key}`;
  }

  /**
   * A 400 without a `message` has `detail: null`, and ngx-translate formats `null` as the literal
   * `"null"`, so the caller passes the translated `NOT_REPORTED` fallback (PR1c 1c.5.5 precedent).
   */
  errorParams(err: FtmoRequestError, notReported: string): { detail: string } {
    return { detail: err.detail ?? notReported };
  }

  /** AD10: early return while `running()` blocks any second request. */
  run(): void {
    if (this.running() || !this.canRun()) {
      return;
    }

    const query: FtmoSimulationQuery = {
      broker: this.brokerValue(),
      sqxSymbol: this.sqxSymbol(),
      initialCapital: this.initialCapital()!,
      targetRiskPerTrade: this.targetRiskPerTrade()!,
      sizeDecimals: this.sizeDecimals()!,
      step: this.step()!,
      minLot: this.minLot()!,
      maxLots: this.maxLots()!,
      fxLow: this.fxLow(),
      fxHigh: this.fxHigh(),
    };
    this.lastQuery = query;
    this.error.set(null);
    this.result.set(null);
    this.running.set(true);

    this.ftmoService
      .getMultiStart(this.strategyId(), query)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (dto) => {
          this.result.set(dto);
          this.running.set(false);
        },
        error: (err: FtmoRequestError) => {
          this.error.set(err);
          this.result.set(null);
          this.running.set(false);
        },
      });
  }

  /** Snapshot of the query used by the last activated Run (design.md AD10/AD12; consumed from PR2). */
  getLastQuery(): FtmoSimulationQuery | null {
    return this.lastQuery;
  }

  close(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }
}
