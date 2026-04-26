import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  effect,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import {
  ColDef,
  ColGroupDef,
  GridApi,
  GridReadyEvent,
  RowClickedEvent,
  themeQuartz,
} from 'ag-grid-community';
import {
  StrategyService,
  StrategyDto,
  StrategyTradeSummaryDto,
} from '../../../core/services/strategy.service';
import {
  TradingAccountService,
  TradingAccountDto,
} from '../../../core/services/trading-account.service';
import { GridPresetService, GridPresetDto } from '../../../core/services/grid-preset.service';
import { AddStrategyModalComponent } from '../add-strategy-modal/add-strategy-modal.component';
import { SavePresetModalComponent } from '../save-preset-modal/save-preset-modal.component';
import { StrategyCommentsModalComponent } from '../strategy-comments-modal/strategy-comments-modal.component';
import { ImportTradesModalComponent } from '../import-trades-modal/import-trades-modal.component';
import { StrategyTradesGridComponent } from '../strategy-trades-grid/strategy-trades-grid.component';
import { StrategyAnalyticsModalComponent } from '../strategy-analytics-modal/strategy-analytics-modal.component';
import { TradeImportResultDto } from '../../../core/services/trading-account.service';
import { symbolToColor } from '../../../shared/utils/symbol-color';
import { formatCurrency } from '../../../shared/utils/format';

export interface KpiColDef {
  field: keyof StrategyDto;
  headerName: string;
  /** When true, skip the numeric toFixed(2) formatter — the value is a plain string. */
  text?: boolean;
  /** When true, render as a currency amount ($ + thousands + 2 decimals). */
  currency?: boolean;
  /** When true, render as a percentage (value × 100, fixed to 2 decimals). */
  percent?: boolean;
}

/** MT4 live KPI columns shown under the "MT4 (Live)" group. */
export const MT4_KPI_COLS: KpiColDef[] = [
  { field: 'liveNetProfit', headerName: 'Net Profit', currency: true },
  { field: 'liveTotalReturn', headerName: 'Total Return %', percent: true },
  { field: 'liveWinRate', headerName: 'Win %', percent: true },
  { field: 'liveProfitFactor', headerName: 'Profit Factor' },
  { field: 'liveReturnDrawdownRatio', headerName: 'Return / DD' },
  { field: 'liveMaxDrawdownPercent', headerName: 'Max DD %', percent: true },
  { field: 'liveSharpeRatio', headerName: 'Sharpe' },
];

export const ALL_KPI_COLS: KpiColDef[] = [
  { field: 'totalProfit', headerName: 'Total Profit', currency: true },
  { field: 'profitInPips', headerName: 'Profit (pips)' },
  { field: 'yearlyAvgProfit', headerName: 'Yearly Avg Profit', currency: true },
  { field: 'yearlyAvgReturn', headerName: 'Yearly Avg Return' },
  { field: 'cagr', headerName: 'CAGR' },
  { field: 'numberOfTrades', headerName: 'Trades' },
  { field: 'sharpeRatio', headerName: 'Sharpe Ratio' },
  { field: 'profitFactor', headerName: 'Profit Factor' },
  { field: 'returnDrawdownRatio', headerName: 'Return/DD Ratio' },
  { field: 'winningPercentage', headerName: 'Win %' },
  { field: 'drawdown', headerName: 'Drawdown', currency: true },
  { field: 'drawdownPercent', headerName: 'Drawdown %' },
  { field: 'dailyAvgProfit', headerName: 'Daily Avg Profit', currency: true },
  { field: 'monthlyAvgProfit', headerName: 'Monthly Avg Profit', currency: true },
  { field: 'averageTrade', headerName: 'Avg Trade', currency: true },
  { field: 'annualReturnMaxDdRatio', headerName: 'Ann. Return / Max DD' },
  { field: 'rExpectancy', headerName: 'R-Expectancy' },
  { field: 'rExpectancyScore', headerName: 'R-Expectancy Score' },
  { field: 'strQualityNumber', headerName: 'SQN' },
  { field: 'sqnScore', headerName: 'SQN Score' },
  { field: 'winsLossesRatio', headerName: 'Win/Loss Ratio' },
  { field: 'payoutRatio', headerName: 'Payout Ratio' },
  { field: 'averageBarsInTrade', headerName: 'Avg Bars in Trade' },
  { field: 'ahpr', headerName: 'AHPR' },
  { field: 'zScore', headerName: 'Z-Score' },
  { field: 'zProbability', headerName: 'Z-Probability' },
  { field: 'expectancy', headerName: 'Expectancy' },
  { field: 'deviation', headerName: 'Deviation' },
  { field: 'exposure', headerName: 'Exposure' },
  { field: 'stagnationInDays', headerName: 'Stagnation (days)' },
  { field: 'stagnationPercent', headerName: 'Stagnation %' },
  { field: 'numberOfWins', headerName: 'Wins' },
  { field: 'numberOfLosses', headerName: 'Losses' },
  { field: 'numberOfCancelled', headerName: 'Cancelled' },
  { field: 'grossProfit', headerName: 'Gross Profit', currency: true },
  { field: 'grossLoss', headerName: 'Gross Loss', currency: true },
  { field: 'averageWin', headerName: 'Avg Win', currency: true },
  { field: 'averageLoss', headerName: 'Avg Loss', currency: true },
  { field: 'largestWin', headerName: 'Largest Win', currency: true },
  { field: 'largestLoss', headerName: 'Largest Loss', currency: true },
  { field: 'maxConsecutiveWins', headerName: 'Max Consec. Wins' },
  { field: 'maxConsecutiveLosses', headerName: 'Max Consec. Losses' },
  { field: 'averageConsecutiveWins', headerName: 'Avg Consec. Wins' },
  { field: 'averageConsecutiveLosses', headerName: 'Avg Consec. Losses' },
  { field: 'averageBarsInWins', headerName: 'Avg Bars in Wins' },
  { field: 'averageBarsInLosses', headerName: 'Avg Bars in Losses' },
  { field: 'entryIndicators', headerName: 'Entry Indicators', text: true },
  { field: 'priceIndicators', headerName: 'Price Indicators', text: true },
  { field: 'indicatorParameters', headerName: 'Indicator Params', text: true },
];

export const DEFAULT_VISIBLE_COLS: (keyof StrategyDto)[] = [
  // SQX
  'totalProfit',
  'winningPercentage',
  'profitFactor',
  'drawdown',
  'numberOfTrades',
  'sharpeRatio',
  // MT4 — top 5 most useful side-by-side with SQX
  'liveNetProfit',
  'liveTotalReturn',
  'liveWinRate',
  'liveProfitFactor',
  'liveMaxDrawdownPercent',
];

/**
 * Column ids always present in the grid — never persisted in presets.
 * The Actions column's cellRenderer uses field 'id'.
 */
export const FIXED_COL_IDS: ReadonlySet<string> = new Set(['name', 'symbol', 'timeframe', 'id']);

@Component({
  selector: 'app-account-detail',
  standalone: true,
  imports: [
    CommonModule,
    AgGridAngular,
    AddStrategyModalComponent,
    SavePresetModalComponent,
    StrategyCommentsModalComponent,
    ImportTradesModalComponent,
    StrategyTradesGridComponent,
    StrategyAnalyticsModalComponent,
  ],
  templateUrl: './account-detail.component.html',
  styleUrl: './account-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly strategyService = inject(StrategyService);
  private readonly tradingAccountService = inject(TradingAccountService);
  private readonly gridPresetService = inject(GridPresetService);

  accountId = '';

  readonly account = signal<TradingAccountDto | null>(null);
  readonly strategies = signal<StrategyDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly showModal = signal(false);
  readonly showColumnPicker = signal(false);
  readonly showDeleteConfirm = signal(false);
  readonly pendingDeleteId = signal<string | null>(null);
  readonly visibleColumns = signal<(keyof StrategyDto)[]>([...DEFAULT_VISIBLE_COLS]);
  readonly presets = signal<GridPresetDto[]>([]);
  readonly showPresetsDropdown = signal(false);
  readonly showSavePresetModal = signal(false);
  readonly showCommentsModal = signal(false);
  readonly selectedStrategyForComments = signal<StrategyDto | null>(null);
  readonly showImportModal = signal(false);
  readonly showTradesGrid = signal(false);
  readonly activeStrategyId = signal<string | null>(null);
  /** Target of the analytics modal — set from any row (Actions column) or the trades panel header. */
  readonly analyticsTargetStrategy = signal<StrategyDto | null>(null);

  /** Currently selected strategy (or null when nothing is selected). */
  readonly activeStrategy = computed<StrategyDto | null>(() => {
    const id = this.activeStrategyId();
    if (!id) return null;
    return this.strategies().find((s) => s.id === id) ?? null;
  });

  /** KPI aggregates over the imported trades of the active strategy. */
  readonly tradeSummary = signal<StrategyTradeSummaryDto | null>(null);

  private gridApi: GridApi<StrategyDto> | null = null;

  constructor() {
    // Refetch the trade summary whenever the active strategy changes.
    effect(() => {
      const id = this.activeStrategyId();
      if (!id) {
        this.tradeSummary.set(null);
        return;
      }
      this.strategyService.getTradesSummaryByStrategy(id).subscribe({
        next: (summary) => this.tradeSummary.set(summary),
        error: () => this.tradeSummary.set(null),
      });
    });
  }

  readonly gridTheme = themeQuartz;
  readonly allKpiCols = ALL_KPI_COLS;
  readonly mt4KpiCols = MT4_KPI_COLS;

  readonly defaultColDef: ColDef<StrategyDto> = {
    sortable: true,
    filter: true,
    resizable: true,
    suppressHeaderMenuButton: true,
  };

  readonly columnDefs = computed<(ColDef<StrategyDto> | ColGroupDef<StrategyDto>)[]>(() => {
    const visible = this.visibleColumns();
    const nameDef: ColDef<StrategyDto> = {
      field: 'name',
      headerName: 'Name',
      flex: 1,
      minWidth: 160,
      pinned: 'left',
    };

    const symbolDef: ColDef<StrategyDto> = {
      field: 'symbol',
      headerName: 'Symbol',
      width: 120,
      cellStyle: (params: { value: string | null }) => ({
        backgroundColor: symbolToColor(params.value) + '20',
        borderLeft: `3px solid ${symbolToColor(params.value)}`,
      }),
    };

    const timeframeDef: ColDef<StrategyDto> = {
      field: 'timeframe',
      headerName: 'Timeframe',
      width: 110,
    };

    // Build a column from a KpiColDef using one of three formatters: text (skip),
    // currency, percent, or default toFixed(2).
    const buildCol = (c: KpiColDef): ColDef<StrategyDto> => {
      let formatter: ColDef<StrategyDto>['valueFormatter'];
      if (c.text) {
        formatter = undefined;
      } else if (c.currency) {
        formatter = (p: { value: number | null }) => formatCurrency(p.value);
      } else if (c.percent) {
        formatter = (p: { value: number | null }) =>
          p.value !== null && p.value !== undefined ? `${(p.value * 100).toFixed(2)}%` : '—';
      } else {
        formatter = (p: { value: number | null }) =>
          p.value !== null && p.value !== undefined ? p.value.toFixed(2) : '—';
      }

      const colDef: ColDef<StrategyDto> = {
        field: c.field,
        headerName: c.headerName,
        width: 150,
        hide: !visible.includes(c.field),
        ...(formatter ? { valueFormatter: formatter } : {}),
      };
      return colDef;
    };

    // SQX KPIs grouped under "SQX (Backtest)" header.
    const sqxGroup: ColGroupDef<StrategyDto> = {
      headerName: 'SQX (Backtest)',
      headerClass: 'col-group-sqx',
      children: ALL_KPI_COLS.map(buildCol),
    };

    // MT4 live KPIs grouped under "MT4 (Live)" header.
    const mt4Group: ColGroupDef<StrategyDto> = {
      headerName: 'MT4 (Live)',
      headerClass: 'col-group-mt4',
      children: MT4_KPI_COLS.map(buildCol),
    };

    const deleteDef: ColDef<StrategyDto> = {
      headerName: 'Actions',
      field: 'id',
      width: 150,
      sortable: false,
      filter: false,
      resizable: false,
      pinned: 'right',
      cellRenderer: (params: { value: string; data: StrategyDto }) => {
        const container = document.createElement('div');
        container.className = 'grid-actions';

        const performanceBtn = document.createElement('button');
        performanceBtn.className = 'grid-action-btn';
        performanceBtn.title = 'Open performance analysis';
        performanceBtn.innerHTML = '&#x1F4CA;';
        performanceBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          this.openAnalyticsModal(params.data);
        });

        const commentsBtn = document.createElement('button');
        commentsBtn.className = 'grid-action-btn';
        commentsBtn.title = 'View comments';
        commentsBtn.innerHTML = '&#x1F4AC;';
        commentsBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          this.openComments(params.data);
        });

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'grid-delete-btn';
        deleteBtn.title = 'Delete strategy';
        deleteBtn.innerHTML = '&#x1F5D1;';
        deleteBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          this.requestDelete(params.value);
        });

        container.appendChild(performanceBtn);
        container.appendChild(commentsBtn);
        container.appendChild(deleteBtn);
        return container;
      },
    };

    return [nameDef, symbolDef, timeframeDef, sqxGroup, mt4Group, deleteDef];
  });

  ngOnInit(): void {
    this.accountId = this.route.snapshot.params['accountId'];
    this.loadAccount();
    this.loadStrategies();
    this.loadPresets();
  }

  navigateBack(): void {
    this.router.navigate(['/darwinex/demo']);
  }

  onGridReady(event: GridReadyEvent<StrategyDto>): void {
    this.gridApi = event.api;
  }

  toggleColumn(field: keyof StrategyDto | string): void {
    this.visibleColumns.update((cols) => {
      const key = field as keyof StrategyDto;
      if (cols.includes(key)) {
        return cols.filter((c) => c !== key);
      }
      return [...cols, key];
    });
  }

  openAddStrategyModal(): void {
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  onStrategyCreated(dto: StrategyDto): void {
    this.strategies.update((list) => [dto, ...list]);
    this.showModal.set(false);
  }

  toggleColumnPicker(): void {
    this.showColumnPicker.update((v) => !v);
  }

  requestDelete(id: string): void {
    this.pendingDeleteId.set(id);
    this.showDeleteConfirm.set(true);
  }

  confirmDelete(): void {
    const id = this.pendingDeleteId();
    if (!id) return;
    this.strategyService.delete(id).subscribe({
      next: () => {
        this.strategies.update((list) => list.filter((s) => s.id !== id));
        this.showDeleteConfirm.set(false);
        this.pendingDeleteId.set(null);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to delete strategy.');
        this.showDeleteConfirm.set(false);
        this.pendingDeleteId.set(null);
      },
    });
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
    this.pendingDeleteId.set(null);
  }

  openComments(strategy: StrategyDto): void {
    this.selectedStrategyForComments.set(strategy);
    this.showCommentsModal.set(true);
  }

  closeCommentsModal(): void {
    this.showCommentsModal.set(false);
    this.selectedStrategyForComments.set(null);
  }

  openImportModal(): void {
    this.showImportModal.set(true);
  }

  closeImportModal(_result: TradeImportResultDto | null): void {
    this.showImportModal.set(false);
  }

  /** Opens the trades panel for the clicked row (or switches it if another row was active). */
  onRowClicked(event: RowClickedEvent<StrategyDto>): void {
    if (!event.data) return;
    this.selectStrategy(event.data.id);
  }

  selectStrategy(strategyId: string): void {
    this.activeStrategyId.set(strategyId);
    this.showTradesGrid.set(true);
  }

  closeTradesPanel(): void {
    this.showTradesGrid.set(false);
    this.activeStrategyId.set(null);
    this.gridApi?.deselectAll();
  }

  openAnalyticsModal(strategy: StrategyDto): void {
    this.analyticsTargetStrategy.set(strategy);
  }

  closeAnalyticsModal(): void {
    this.analyticsTargetStrategy.set(null);
  }

  togglePresetsDropdown(): void {
    this.showPresetsDropdown.update((v) => !v);
  }

  applyPreset(preset: GridPresetDto): void {
    const visible = preset.visibleColumns as (keyof StrategyDto)[];
    this.visibleColumns.set(visible);
    this.showPresetsDropdown.set(false);

    const api = this.gridApi;
    if (!api) return;
    const order = preset.columnOrder.length > 0 ? preset.columnOrder : preset.visibleColumns;
    const visibleSet = new Set(preset.visibleColumns);
    const orderSet = new Set(order);

    // Defer to the next macrotask so ag-grid has applied the new [columnDefs]
    // input (driven by the signal update above) before we reorder.
    setTimeout(() => {
      const current = api.getColumnState();
      // Keep fixed cols (name, symbol, timeframe, actions) in their current
      // position — don't let the preset reorder them.
      const fixedState = current.filter((s) => FIXED_COL_IDS.has(s.colId));
      // Preset cols in preset order with correct visibility.
      const presetState = order.map((colId) => ({ colId, hide: !visibleSet.has(colId) }));
      // KPI cols not in the preset stay hidden, appended at the end.
      const remainingState = current
        .filter((s) => !FIXED_COL_IDS.has(s.colId) && !orderSet.has(s.colId))
        .map((s) => ({ colId: s.colId, hide: true }));

      api.applyColumnState({
        state: [...fixedState, ...presetState, ...remainingState],
        applyOrder: true,
      });
    }, 0);
  }

  openSavePresetModal(): void {
    this.showSavePresetModal.set(true);
    this.showPresetsDropdown.set(false);
  }

  closeSavePresetModal(): void {
    this.showSavePresetModal.set(false);
  }

  savePreset(name: string): void {
    // Capture the real column state from ag-grid (accounts for drag-reordering
    // and for columns toggled via the picker). Fall back to the signal if the
    // grid api isn't ready yet.
    const api = this.gridApi;
    let visibleColumns: string[];
    let columnOrder: string[];

    if (api) {
      const state = api.getColumnState().filter((s) => !FIXED_COL_IDS.has(s.colId));
      columnOrder = state.map((s) => s.colId);
      visibleColumns = state.filter((s) => !s.hide).map((s) => s.colId);
    } else {
      visibleColumns = [...this.visibleColumns()];
      columnOrder = [...this.visibleColumns()];
    }

    const dto = { name, visibleColumns, columnOrder };
    this.gridPresetService.create(dto).subscribe({
      next: (preset) => {
        this.presets.update((list) => [...list, preset]);
        this.showSavePresetModal.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to save preset.');
        this.showSavePresetModal.set(false);
      },
    });
  }

  updatePreset(preset: GridPresetDto): void {
    // Same capture logic as savePreset: prefer the live grid state so drag-reorder
    // is persisted; fall back to the signal if the grid isn't ready yet.
    const api = this.gridApi;
    let visibleColumns: string[];
    let columnOrder: string[];

    if (api) {
      const state = api.getColumnState().filter((s) => !FIXED_COL_IDS.has(s.colId));
      columnOrder = state.map((s) => s.colId);
      visibleColumns = state.filter((s) => !s.hide).map((s) => s.colId);
    } else {
      visibleColumns = [...this.visibleColumns()];
      columnOrder = [...this.visibleColumns()];
    }

    this.gridPresetService.update(preset.id, { visibleColumns, columnOrder }).subscribe({
      next: (updated) => {
        this.presets.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
        this.showPresetsDropdown.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to update preset.');
      },
    });
  }

  deletePreset(id: string): void {
    this.gridPresetService.delete(id).subscribe({
      next: () => this.presets.update((list) => list.filter((p) => p.id !== id)),
      error: (err) =>
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to delete preset.'),
    });
  }

  private loadAccount(): void {
    this.tradingAccountService.getById(this.accountId).subscribe({
      next: (acc) => this.account.set(acc),
      error: () => this.account.set(null),
    });
  }

  private loadPresets(): void {
    this.gridPresetService.getAll().subscribe({
      next: (presets) => this.presets.set(presets),
      error: () => this.presets.set([]),
    });
  }

  private loadStrategies(): void {
    this.isLoading.set(true);
    // Client-side pagination via ag-grid: fetch all strategies in one call. 500 covers
    // any realistic per-account size. If we ever approach this, switch to server-side paging.
    this.strategyService.getByAccount(this.accountId, 1, 500).subscribe({
      next: (result) => {
        this.strategies.set(result.items);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to load strategies.');
        this.isLoading.set(false);
      },
    });
  }
}
