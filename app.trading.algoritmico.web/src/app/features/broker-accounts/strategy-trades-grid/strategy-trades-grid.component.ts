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
import { AgGridAngular } from 'ag-grid-angular';
import {
  ColDef,
  RowClassParams,
  RowStyle,
  ValueFormatterParams,
  ValueGetterParams,
  themeQuartz,
} from 'ag-grid-community';
import { StrategyService, StrategyTradeDto } from '../../../core/services/strategy.service';
import { formatCurrency, formatDateTime } from '../../../shared/utils/format';

/** Display-friendly label for a raw MT4 close-reason suffix. */
function formatCloseReason(value: string | null | undefined): string {
  if (!value) return '—';
  if (value === 'TS') return 'Trailing';
  return value;
}

/** Cell class used to color-code the close reason. */
function closeReasonClass(value: string | null | undefined): string {
  switch (value) {
    case 'TP':
      return 'close-reason--tp';
    case 'SL':
      return 'close-reason--sl';
    case 'TS':
      return 'close-reason--ts';
    case null:
    case undefined:
      return '';
    default:
      return 'close-reason--other';
  }
}

/** Net profit per trade — broker-reported profit plus all costs (commission/swap/taxes). */
function computeNetProfit(t: StrategyTradeDto | undefined | null): number | null {
  if (!t) return null;
  return t.profit + t.commission + t.swap + t.taxes;
}

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
  readonly pinnedBottomRowData = computed<Partial<StrategyTradeDto>[]>(() => {
    const t = this.trades();
    if (t.length === 0) return [];
    return [
      {
        commission: t.reduce((s, x) => s + x.commission, 0),
        swap: t.reduce((s, x) => s + x.swap, 0),
        taxes: t.reduce((s, x) => s + x.taxes, 0),
        profit: t.reduce((s, x) => s + x.profit, 0),
      },
    ];
  });

  constructor() {
    effect(() => {
      // Track strategyId — re-runs whenever the parent points us at another strategy.
      this.strategyId();
      this.loadTrades();
    });
  }

  readonly gridTheme = themeQuartz;

  readonly columnDefs: ColDef<StrategyTradeDto>[] = [
    {
      field: 'ticket',
      headerName: 'Ticket',
      width: 100,
      // Pinned total row shows the "TOTAL" label here instead of a ticket number.
      valueFormatter: (p: ValueFormatterParams<StrategyTradeDto>) =>
        p.node?.rowPinned ? 'TOTAL' : (p.value?.toString() ?? ''),
    },
    {
      field: 'openTime',
      headerName: 'Open Time',
      width: 170,
      valueFormatter: (p: { value: string | null }) => formatDateTime(p.value),
    },
    {
      field: 'closeTime',
      headerName: 'Close Time',
      width: 170,
      valueFormatter: (p: { value: string | null }) => formatDateTime(p.value),
    },
    { field: 'type', headerName: 'Type', width: 80 },
    { field: 'size', headerName: 'Size', width: 80 },
    { field: 'item', headerName: 'Item', width: 100 },
    { field: 'openPrice', headerName: 'Open Price', width: 110 },
    { field: 'closePrice', headerName: 'Close Price', width: 110 },
    { field: 'stopLoss', headerName: 'SL', width: 100 },
    { field: 'takeProfit', headerName: 'TP', width: 100 },
    {
      field: 'commission',
      headerName: 'Commission',
      width: 120,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      field: 'swap',
      headerName: 'Swap',
      width: 100,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      field: 'profit',
      headerName: 'Profit',
      width: 110,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      headerName: 'Net Profit',
      colId: 'netProfit',
      width: 120,
      valueGetter: (p: ValueGetterParams<StrategyTradeDto>) => computeNetProfit(p.data),
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
      cellClass: (p: { value: number | null }) =>
        p.value === null || p.value === undefined
          ? ''
          : p.value > 0
            ? 'profit--positive'
            : p.value < 0
              ? 'profit--negative'
              : '',
    },
    {
      field: 'closeReason',
      headerName: 'Close Reason',
      width: 130,
      valueFormatter: (p: { value: string | null }) => formatCloseReason(p.value),
      cellClass: (p: { value: string | null }) => closeReasonClass(p.value),
    },
    {
      headerName: 'Status',
      colId: 'status',
      width: 100,
      // Use valueGetter (not field+valueFormatter): ag-grid 35 auto-renders boolean
      // fields with a checkbox cell renderer that ignores valueFormatter.
      valueGetter: (p: ValueGetterParams<StrategyTradeDto>) => (p.data?.isOpen ? 'Open' : 'Closed'),
      cellClass: (p: { value: string }) =>
        p.value === 'Open' ? 'trade-status--open' : 'trade-status--closed',
    },
  ];

  /**
   * Inline row styling — applied as `style="background-color: ..."` on the row
   * element. Inline beats ag-grid's own `.ag-row-odd/even` rules without needing
   * `!important`, which CSS classes can't reliably do under encapsulation.
   */
  readonly getRowStyle = (params: RowClassParams<StrategyTradeDto>): RowStyle | undefined => {
    // Pinned total row — bold, neutral background, top border to separate from data.
    if (params.node?.rowPinned) {
      return {
        fontWeight: '600',
        backgroundColor: 'rgba(255, 255, 255, 0.04)',
        borderTop: '2px solid var(--color-border, #313244)',
      };
    }
    if (!params.data) return undefined;
    if (params.data.isOpen) return { backgroundColor: 'rgba(137, 180, 250, 0.12)' };
    const net = computeNetProfit(params.data);
    if (net !== null && net > 0) return { backgroundColor: 'rgba(34, 197, 94, 0.12)' };
    if (net !== null && net < 0) return { backgroundColor: 'rgba(239, 68, 68, 0.12)' };
    return undefined;
  };

  readonly defaultColDef: ColDef<StrategyTradeDto> = {
    sortable: true,
    filter: true,
    resizable: true,
  };

  setStatus(status: 'all' | 'open' | 'closed'): void {
    this.status.set(status);
    this.loadTrades();
  }

  private loadTrades(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.strategyService.getTradesByStrategy(this.strategyId(), this.status(), 1, 50).subscribe({
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
