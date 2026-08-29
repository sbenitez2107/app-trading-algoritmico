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
import { ColDef, ValueFormatterParams, themeQuartz } from 'ag-grid-community';
import { PortfolioService, PortfolioTradeDto } from '../../../core/services/portfolio.service';
import {
  buildTradeColumnDefs,
  computePinnedTotals,
  tradeRowStyle,
  tradesDefaultColDef,
} from '../../../shared/trades-grid/trades-grid-shared';

@Component({
  selector: 'app-portfolio-trades-grid',
  standalone: true,
  imports: [CommonModule, AgGridAngular],
  templateUrl: './portfolio-trades-grid.component.html',
  styleUrl: './portfolio-trades-grid.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfolioTradesGridComponent {
  // Signal input: refetches when the parent switches the active portfolio
  // without unmounting the component.
  readonly portfolioId = input.required<string>();

  private readonly portfolioService = inject(PortfolioService);

  readonly status = signal<'all' | 'open' | 'closed'>('all');
  readonly isLoading = signal(true);
  readonly trades = signal<PortfolioTradeDto[]>([]);
  readonly error = signal<string | null>(null);

  /**
   * Pinned bottom row showing column totals across the loaded trades. The
   * Net Profit valueGetter runs over this row too, so its total is computed
   * automatically from the summed profit + commission + swap + taxes.
   */
  readonly pinnedBottomRowData = computed<Partial<PortfolioTradeDto>[]>(() =>
    computePinnedTotals(this.trades()),
  );

  constructor() {
    effect(() => {
      // Track portfolioId — re-runs whenever the parent points us at another portfolio.
      this.portfolioId();
      this.loadTrades();
    });
  }

  readonly gridTheme = themeQuartz;

  /**
   * Same columns as the strategy trades grid, plus a leading "Estrategia" column.
   * The shared Ticket column does NOT render the TOTAL label here (`'none'`) — the
   * Strategy column carries the "TOTAL" label on the pinned bottom row instead.
   *
   * The shared defs are typed against the structural TradeRow shape; PortfolioTradeDto
   * is a superset, so casting them into a ColDef<PortfolioTradeDto>[] is safe (they only
   * read fields present on PortfolioTradeDto).
   */
  readonly columnDefs: ColDef<PortfolioTradeDto>[] = [
    {
      field: 'strategyName',
      headerName: 'Estrategia',
      minWidth: 180,
      flex: 1,
      pinned: 'left',
      valueFormatter: (p: ValueFormatterParams<PortfolioTradeDto>) =>
        p.node?.rowPinned ? 'TOTAL' : (p.value ?? ''),
    },
    ...buildTradeColumnDefs<PortfolioTradeDto>('none'),
  ];

  /** Inline row styling by trade state (open / win / loss) — shared with the strategy grid. */
  readonly getRowStyle = tradeRowStyle<PortfolioTradeDto>;

  readonly defaultColDef: ColDef<PortfolioTradeDto> = tradesDefaultColDef<PortfolioTradeDto>();

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

    const id = this.portfolioId();
    const status = this.status();
    const FIRST_PAGE = 50;

    this.portfolioService
      .getTradesByPortfolio(id, status, 1, FIRST_PAGE)
      .pipe(
        switchMap((first) =>
          first.totalCount > first.items.length
            ? this.portfolioService.getTradesByPortfolio(id, status, 1, first.totalCount)
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
