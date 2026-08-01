import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  computed,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import {
  ColDef,
  ValueFormatterParams,
  ValueGetterParams,
  CellClickedEvent,
  ICellRendererParams,
  themeQuartz,
} from 'ag-grid-community';
import {
  PortfolioService,
  PortfolioSummaryDto,
  PortfolioMonthlyReturnsDto,
  MonthlyReturnDto,
  AccountType,
} from '../../../core/services/portfolio.service';
import { formatCurrency } from '../../../shared/utils/format';
import { PortfolioMonthlyReturnsComponent } from '../portfolio-monthly-returns/portfolio-monthly-returns.component';
import {
  PortfolioMonthlySource,
  PortfolioMonthlyTooltipComponent,
} from './portfolio-monthly-tooltip.component';

/** Formats a fraction (0..1) as a percentage string with `decimals` decimal places. */
function formatPct(value: number | null | undefined, decimals = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return `${(value * 100).toFixed(decimals)}%`;
}

/** Formats a number with a fixed number of decimal places. */
function formatDec(value: number | null | undefined, decimals = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return value.toFixed(decimals);
}

/** Action columns must not trigger the row's navigate-to-detail click. */
const MONTHLY_COL_ID = 'monthly';
const ACTIONS_COL_ID = 'actions';
const NON_NAVIGABLE_COLS: ReadonlySet<string> = new Set([MONTHLY_COL_ID, ACTIONS_COL_ID]);

@Component({
  selector: 'app-portfolios-list',
  standalone: true,
  imports: [CommonModule, AgGridAngular, PortfolioMonthlyReturnsComponent],
  templateUrl: './portfolios-list.component.html',
  styleUrl: './portfolios-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfoliosListComponent implements OnInit, PortfolioMonthlySource {
  private readonly service = inject(PortfolioService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  broker = '';
  private portfoliosBase = '/portfolios';

  readonly portfolios = signal<PortfolioSummaryDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  /** 'grid' = KPI grid (default); 'monthly' = portfolios × months matrix. */
  readonly viewMode = signal<'grid' | 'monthly'>('grid');

  // --- Monthly returns: one broker-wide request feeds both the matrix view and the row tooltips.
  readonly monthlyRows = signal<PortfolioMonthlyReturnsDto[]>([]);
  readonly monthlyLoading = signal(true);
  readonly monthlyError = signal<string | null>(null);

  readonly monthlyById = computed<Map<string, MonthlyReturnDto[]>>(
    () => new Map(this.monthlyRows().map((r) => [r.portfolioId, r.returns])),
  );

  // --- Delete confirmation
  readonly pendingDelete = signal<PortfolioSummaryDto | null>(null);
  readonly isDeleting = signal(false);

  readonly gridTheme = themeQuartz;

  readonly defaultColDef: ColDef<PortfolioSummaryDto> = {
    sortable: true,
    resizable: true,
    minWidth: 80,
  };

  readonly columnDefs: ColDef<PortfolioSummaryDto>[] = [
    // --- Identity ---
    {
      field: 'name',
      headerName: 'Portfolio',
      pinned: 'left',
      flex: 1,
      minWidth: 180,
    },
    {
      field: 'memberCount',
      headerName: 'Estrategias',
      width: 110,
    },
    {
      field: 'accountType',
      headerName: 'Tipo',
      width: 80,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        p.value === AccountType.Live ? 'Live' : 'Demo',
      cellClass: (p: { value: AccountType }) =>
        p.value === AccountType.Live ? 'account-type--live' : 'account-type--demo',
    },
    {
      field: 'broker',
      headerName: 'Broker',
      width: 110,
    },
    // --- Capital ---
    {
      field: 'initialCapital',
      headerName: 'Capital',
      width: 120,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        formatCurrency(p.value, p.data?.baseCurrency ?? 'USD'),
    },
    {
      field: 'finalEquity',
      headerName: 'Equity Final',
      width: 130,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        formatCurrency(p.value, p.data?.baseCurrency ?? 'USD'),
    },
    // --- Profit / Return ---
    {
      field: 'netProfit',
      headerName: 'Net Profit',
      width: 130,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        formatCurrency(p.value, p.data?.baseCurrency ?? 'USD'),
      cellClass: (p: { value: number }) =>
        p.value > 0 ? 'profit--positive' : p.value < 0 ? 'profit--negative' : '',
    },
    {
      field: 'totalReturn',
      headerName: 'Return',
      width: 100,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatPct(p.value),
      cellClass: (p: { value: number }) =>
        p.value > 0 ? 'profit--positive' : p.value < 0 ? 'profit--negative' : '',
    },
    {
      field: 'cagr',
      headerName: 'CAGR',
      width: 100,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatPct(p.value),
      cellClass: (p: { value: number }) =>
        p.value > 0 ? 'profit--positive' : p.value < 0 ? 'profit--negative' : '',
    },
    // --- Risk ratios ---
    {
      field: 'returnDrawdownRatio',
      headerName: 'R/DD',
      width: 90,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatDec(p.value),
    },
    {
      field: 'profitFactor',
      headerName: 'PF',
      width: 80,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatDec(p.value),
    },
    {
      field: 'sharpeRatio',
      headerName: 'Sharpe',
      width: 90,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatDec(p.value),
    },
    {
      field: 'sqn',
      headerName: 'SQN',
      width: 80,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatDec(p.value),
    },
    {
      field: 'maxDrawdownPercent',
      headerName: 'Max DD',
      width: 100,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatPct(p.value),
      cellClass: () => 'profit--negative',
    },
    {
      field: 'exposure',
      headerName: 'Exposure',
      width: 100,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatPct(p.value, 1),
    },
    // --- Trade stats ---
    {
      field: 'tradeCount',
      headerName: 'Trades',
      width: 90,
    },
    {
      headerName: 'W / L',
      colId: 'wl',
      width: 90,
      valueGetter: (p: ValueGetterParams<PortfolioSummaryDto>) =>
        p.data ? `${p.data.winCount} / ${p.data.lossCount}` : '—',
    },
    {
      field: 'winRate',
      headerName: 'Win Rate',
      width: 100,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) => formatPct(p.value),
    },
    // --- Avg profits ---
    {
      field: 'monthlyAvgProfit',
      headerName: 'Avg/Mes',
      width: 120,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        formatCurrency(p.value, p.data?.baseCurrency ?? 'USD'),
    },
    {
      field: 'dailyAvgProfit',
      headerName: 'Avg/Día',
      width: 110,
      valueFormatter: (p: ValueFormatterParams<PortfolioSummaryDto>) =>
        formatCurrency(p.value, p.data?.baseCurrency ?? 'USD'),
    },
    // --- Actions (pinned right) ---
    {
      headerName: 'Mensual',
      colId: MONTHLY_COL_ID,
      width: 90,
      sortable: false,
      filter: false,
      resizable: false,
      pinned: 'right',
      cellClass: 'monthly-cell',
      // A non-empty tooltip value is what makes AG Grid render the custom component at all.
      tooltipValueGetter: (p: { data?: PortfolioSummaryDto }) => p.data?.id ?? null,
      tooltipComponent: PortfolioMonthlyTooltipComponent,
      tooltipComponentParams: { source: this },
      cellRenderer: () => {
        const span = document.createElement('span');
        span.className = 'monthly-cell__icon';
        span.title = 'Ver retorno mensual';
        span.innerHTML = '&#x1F4C5;';
        return span;
      },
    },
    {
      headerName: 'Acciones',
      colId: ACTIONS_COL_ID,
      width: 100,
      sortable: false,
      filter: false,
      resizable: false,
      pinned: 'right',
      cellRenderer: (params: ICellRendererParams<PortfolioSummaryDto>) => {
        const container = document.createElement('div');
        container.className = 'grid-actions';

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'grid-delete-btn';
        deleteBtn.title = 'Eliminar portfolio';
        deleteBtn.innerHTML = '&#x1F5D1;';
        deleteBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          if (params.data) this.requestDelete(params.data);
        });

        container.appendChild(deleteBtn);
        return container;
      },
    },
  ];

  ngOnInit(): void {
    this.broker = this.route.snapshot.data['broker'] ?? '';
    this.portfoliosBase = this.route.snapshot.data['portfoliosBase'] ?? '/portfolios';
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.getSummaries(this.broker).subscribe({
      next: (rows) => {
        this.portfolios.set(rows);
        this.isLoading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar los portfolios');
        this.isLoading.set(false);
      },
    });

    this.loadMonthly();
  }

  /**
   * Loads the whole monthly matrix in the background, in parallel with the summaries.
   * The grid never waits on it: it only backs the tooltips and the monthly view.
   */
  private loadMonthly(): void {
    this.monthlyLoading.set(true);
    this.monthlyError.set(null);
    this.service.getMonthlyReturnsByBroker(this.broker).subscribe({
      next: (rows) => {
        this.monthlyRows.set(rows);
        this.monthlyLoading.set(false);
      },
      error: () => {
        this.monthlyError.set('Error al cargar el retorno mensual');
        this.monthlyLoading.set(false);
      },
    });
  }

  openCreate(): void {
    this.router.navigate([this.portfoliosBase, 'new']);
  }

  toggleMonthlyView(): void {
    this.viewMode.update((mode) => (mode === 'grid' ? 'monthly' : 'grid'));
  }

  openPortfolio(id: string): void {
    this.router.navigate([this.portfoliosBase, id]);
  }

  onCellClicked(event: CellClickedEvent<PortfolioSummaryDto>): void {
    if (!event.data) return;
    if (NON_NAVIGABLE_COLS.has(event.column.getColId())) return;
    this.openPortfolio(event.data.id);
  }

  // -------------------------------------------------------------------------
  // Delete
  // -------------------------------------------------------------------------

  requestDelete(portfolio: PortfolioSummaryDto): void {
    this.pendingDelete.set(portfolio);
  }

  cancelDelete(): void {
    if (this.isDeleting()) return;
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const target = this.pendingDelete();
    if (!target || this.isDeleting()) return;

    this.isDeleting.set(true);
    this.error.set(null);
    this.service.delete(target.id).subscribe({
      next: () => {
        // Drop the row locally so the grid updates without a full refetch,
        // and keep the monthly cache in sync for the tooltip/matrix views.
        this.portfolios.update((rows) => rows.filter((r) => r.id !== target.id));
        this.monthlyRows.update((rows) => rows.filter((r) => r.portfolioId !== target.id));
        this.isDeleting.set(false);
        this.pendingDelete.set(null);
      },
      error: () => {
        this.error.set(`No se pudo eliminar el portfolio "${target.name}"`);
        this.isDeleting.set(false);
        this.pendingDelete.set(null);
      },
    });
  }

  /** Kept for any remaining template references or child components that call it. */
  formatCurrency(amount: number, currency = 'USD'): string {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount);
  }
}
