import {
  ColDef,
  RowClassParams,
  RowStyle,
  ValueFormatterParams,
  ValueGetterParams,
} from 'ag-grid-community';
import { formatCurrency, formatDateTime } from '../utils/format';

/**
 * Minimal structural shape shared by every trade row rendered in a trades grid.
 * Both StrategyTradeDto and PortfolioTradeDto are structural supersets of this —
 * the helpers below only depend on these fields, so column defs / row styling /
 * totals are defined once and reused by both grids.
 *
 * The factories are generic over `T extends TradeRow` so each component keeps
 * full typing at its own DTO. A couple of internal `as unknown as` casts bridge
 * ag-grid's invariant generics (e.g. `ColDefField<T>`) — they are sound because
 * every field/accessor referenced here exists on any `T extends TradeRow`.
 */
export interface TradeRow {
  ticket: number;
  openTime: string;
  closeTime: string | null;
  type: string;
  size: number;
  item: string;
  openPrice: number;
  closePrice: number | null;
  stopLoss: number;
  takeProfit: number;
  commission: number;
  taxes: number;
  swap: number;
  profit: number;
  closeReason: string | null;
  isOpen: boolean;
}

/** Display-friendly label for a raw MT4 close-reason suffix. */
export function formatCloseReason(value: string | null | undefined): string {
  if (!value) return '—';
  if (value === 'TS') return 'Trailing';
  return value;
}

/** Cell class used to color-code the close reason. */
export function closeReasonClass(value: string | null | undefined): string {
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
export function computeNetProfit(t: TradeRow | undefined | null): number | null {
  if (!t) return null;
  return t.profit + t.commission + t.swap + t.taxes;
}

/**
 * Pinned bottom row data: column totals across the loaded trades. Only summable
 * money fields are aggregated. The Net Profit valueGetter runs over this row too,
 * so its total is computed automatically from the summed components.
 */
export function computePinnedTotals<T extends TradeRow>(trades: readonly T[]): Partial<T>[] {
  if (trades.length === 0) return [];
  const totals = {
    commission: trades.reduce((s, x) => s + x.commission, 0),
    swap: trades.reduce((s, x) => s + x.swap, 0),
    taxes: trades.reduce((s, x) => s + x.taxes, 0),
    profit: trades.reduce((s, x) => s + x.profit, 0),
  };
  return [totals as unknown as Partial<T>];
}

/**
 * Inline row styling — applied as `style="background-color: ..."` on the row
 * element. Inline beats ag-grid's own `.ag-row-odd/even` rules without needing
 * `!important`, which CSS classes can't reliably do under encapsulation.
 */
export function tradeRowStyle<T extends TradeRow>(params: RowClassParams<T>): RowStyle | undefined {
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
}

/** Shared default column definition for trades grids. */
export function tradesDefaultColDef<T extends TradeRow>(): ColDef<T> {
  return {
    sortable: true,
    filter: true,
    resizable: true,
  };
}

/**
 * Builds the shared trade column definitions (Ticket → Status). The `totalLabelField`
 * names the column that should render the "TOTAL" label on the pinned bottom row —
 * defaults to `ticket`, but the portfolio grid uses its leading Strategy column instead
 * and so passes `'none'`.
 *
 * Field names are cast via `as keyof T` so the factory stays generic; every name
 * used here is a key of TradeRow and therefore of any `T extends TradeRow`.
 */
export function buildTradeColumnDefs<T extends TradeRow>(
  totalLabelField: 'ticket' | 'none' = 'ticket',
): ColDef<T>[] {
  const field = (name: keyof TradeRow) => name as unknown as ColDef<T>['field'];
  return [
    {
      field: field('ticket'),
      headerName: 'Ticket',
      width: 100,
      valueFormatter:
        totalLabelField === 'ticket'
          ? (p: ValueFormatterParams<T>) =>
              p.node?.rowPinned ? 'TOTAL' : (p.value?.toString() ?? '')
          : (p: ValueFormatterParams<T>) => p.value?.toString() ?? '',
    },
    {
      field: field('openTime'),
      headerName: 'Open Time',
      width: 170,
      valueFormatter: (p: { value: string | null }) => formatDateTime(p.value),
    },
    {
      field: field('closeTime'),
      headerName: 'Close Time',
      width: 170,
      valueFormatter: (p: { value: string | null }) => formatDateTime(p.value),
    },
    { field: field('type'), headerName: 'Type', width: 80 },
    { field: field('size'), headerName: 'Size', width: 80 },
    { field: field('item'), headerName: 'Item', width: 100 },
    { field: field('openPrice'), headerName: 'Open Price', width: 110 },
    { field: field('closePrice'), headerName: 'Close Price', width: 110 },
    { field: field('stopLoss'), headerName: 'SL', width: 100 },
    { field: field('takeProfit'), headerName: 'TP', width: 100 },
    {
      field: field('commission'),
      headerName: 'Commission',
      width: 120,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      field: field('swap'),
      headerName: 'Swap',
      width: 100,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      field: field('profit'),
      headerName: 'Profit',
      width: 110,
      valueFormatter: (p: { value: number | null }) => formatCurrency(p.value),
    },
    {
      headerName: 'Net Profit',
      colId: 'netProfit',
      width: 120,
      valueGetter: (p: ValueGetterParams<T>) => computeNetProfit(p.data),
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
      field: field('closeReason'),
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
      valueGetter: (p: ValueGetterParams<T>) => (p.data?.isOpen ? 'Open' : 'Closed'),
      cellClass: (p: { value: string }) =>
        p.value === 'Open' ? 'trade-status--open' : 'trade-status--closed',
    },
  ];
}
