import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  inject,
  input,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { of, switchMap } from 'rxjs';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, themeQuartz } from 'ag-grid-community';
import { StrategyService, StrategyTradeDto } from '../../../core/services/strategy.service';
import {
  buildTradeColumnDefs,
  computePinnedTotals,
  tradeRowStyle,
  tradesDefaultColDef,
} from '../../../shared/trades-grid/trades-grid-shared';

@Component({
  selector: 'app-strategy-trades-grid',
  standalone: true,
  imports: [CommonModule, AgGridAngular],
  templateUrl: './strategy-trades-grid.component.html',
  styleUrl: './strategy-trades-grid.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyTradesGridComponent {
  // Signal input: refetches when the parent switches the active strategy
  // without unmounting the component.
  readonly strategyId = input.required<string>();

  private readonly strategyService = inject(StrategyService);

  readonly status = signal<'all' | 'open' | 'closed'>('all');
  readonly isLoading = signal(true);
  readonly trades = signal<StrategyTradeDto[]>([]);
  readonly error = signal<string | null>(null);

  /**
   * Pinned bottom row showing column totals across the loaded trades.
   * Note: only summable money fields are aggregated. The Net Profit valueGetter
   * runs over this row too, so its total is computed automatically from the
   * profit + commission + swap + taxes we put here.
   */
  readonly pinnedBottomRowData = computed<Partial<StrategyTradeDto>[]>(() =>
    computePinnedTotals(this.trades()),
  );

  constructor() {
    effect(() => {
      // Track strategyId — re-runs whenever the parent points us at another strategy.
      this.strategyId();
      this.loadTrades();
    });
  }

  readonly gridTheme = themeQuartz;

  // Column defs / row styling come from the shared trades-grid factories,
  // instantiated at this component's DTO type.
  readonly columnDefs: ColDef<StrategyTradeDto>[] = buildTradeColumnDefs<StrategyTradeDto>();

  /** Inline row styling by trade state (open / win / loss) — shared with the portfolio grid. */
  readonly getRowStyle = tradeRowStyle<StrategyTradeDto>;

  readonly defaultColDef: ColDef<StrategyTradeDto> = tradesDefaultColDef<StrategyTradeDto>();

  setStatus(status: 'all' | 'open' | 'closed'): void {
    this.status.set(status);
    this.loadTrades();
  }

  // ag-grid paginates client-side over the rows we hand it, so we must load the
  // FULL trade set — not just the first server page. The first response reports the
  // real totalCount; if it exceeds the rows we got, refetch everything in one page.
  private loadTrades(): void {
    this.isLoading.set(true);
    this.error.set(null);

    const id = this.strategyId();
    const status = this.status();
    const FIRST_PAGE = 50;

    this.strategyService
      .getTradesByStrategy(id, status, 1, FIRST_PAGE)
      .pipe(
        switchMap((first) =>
          first.totalCount > first.items.length
            ? this.strategyService.getTradesByStrategy(id, status, 1, first.totalCount)
            : of(first),
        ),
      )
      .subscribe({
        next: (result) => {
          this.trades.set(result.items);
          this.isLoading.set(false);
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          this.error.set(err?.error?.message ?? err?.message ?? 'Failed to load trades.');
          this.isLoading.set(false);
        },
      });
  }
}
