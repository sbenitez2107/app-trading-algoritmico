import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, themeQuartz } from 'ag-grid-community';
import { StrategyService, StrategyDto } from '../../../core/services/strategy.service';
import {
  TradingAccountService,
  TradingAccountDto,
} from '../../../core/services/trading-account.service';
import { GridPresetService, GridPresetDto } from '../../../core/services/grid-preset.service';
import { AddStrategyModalComponent } from '../add-strategy-modal/add-strategy-modal.component';
import { SavePresetModalComponent } from '../save-preset-modal/save-preset-modal.component';
import { StrategyCommentsModalComponent } from '../strategy-comments-modal/strategy-comments-modal.component';
import { symbolToColor } from '../../../shared/utils/symbol-color';

export interface KpiColDef {
  field: keyof StrategyDto;
  headerName: string;
  /** When true, skip the numeric toFixed(2) formatter — the value is a plain string. */
  text?: boolean;
}

export const ALL_KPI_COLS: KpiColDef[] = [
  { field: 'totalProfit', headerName: 'Total Profit' },
  { field: 'profitInPips', headerName: 'Profit (pips)' },
  { field: 'yearlyAvgProfit', headerName: 'Yearly Avg Profit' },
  { field: 'yearlyAvgReturn', headerName: 'Yearly Avg Return' },
  { field: 'cagr', headerName: 'CAGR' },
  { field: 'numberOfTrades', headerName: 'Trades' },
  { field: 'sharpeRatio', headerName: 'Sharpe Ratio' },
  { field: 'profitFactor', headerName: 'Profit Factor' },
  { field: 'returnDrawdownRatio', headerName: 'Return/DD Ratio' },
  { field: 'winningPercentage', headerName: 'Win %' },
  { field: 'drawdown', headerName: 'Drawdown' },
  { field: 'drawdownPercent', headerName: 'Drawdown %' },
  { field: 'dailyAvgProfit', headerName: 'Daily Avg Profit' },
  { field: 'monthlyAvgProfit', headerName: 'Monthly Avg Profit' },
  { field: 'averageTrade', headerName: 'Avg Trade' },
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
  { field: 'grossProfit', headerName: 'Gross Profit' },
  { field: 'grossLoss', headerName: 'Gross Loss' },
  { field: 'averageWin', headerName: 'Avg Win' },
  { field: 'averageLoss', headerName: 'Avg Loss' },
  { field: 'largestWin', headerName: 'Largest Win' },
  { field: 'largestLoss', headerName: 'Largest Loss' },
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
  'totalProfit',
  'winningPercentage',
  'profitFactor',
  'drawdown',
  'numberOfTrades',
  'sharpeRatio',
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

  private gridApi: GridApi<StrategyDto> | null = null;

  readonly gridTheme = themeQuartz;
  readonly allKpiCols = ALL_KPI_COLS;

  readonly defaultColDef: ColDef<StrategyDto> = {
    sortable: true,
    filter: true,
    resizable: true,
    suppressHeaderMenuButton: true,
  };

  readonly columnDefs = computed<ColDef<StrategyDto>[]>(() => {
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

    // All KPI columns stay in the grid at all times; visibility is toggled via
    // the `hide` property. This lets `applyColumnState` reorder any column (the
    // preset flow relies on this — you cannot reorder a column that isn't in the grid).
    const kpiDefs: ColDef<StrategyDto>[] = ALL_KPI_COLS.map((c) => {
      // String columns (indicators) do not use the numeric toFixed formatter.
      const colDef = {
        field: c.field as string,
        headerName: c.headerName,
        width: 150,
        hide: !visible.includes(c.field),
        ...(c.text
          ? {}
          : {
              valueFormatter: (p: { value: number | null }) =>
                p.value !== null && p.value !== undefined ? p.value.toFixed(2) : '—',
            }),
      };
      return colDef as ColDef<StrategyDto>;
    });

    const deleteDef: ColDef<StrategyDto> = {
      headerName: 'Actions',
      field: 'id',
      width: 120,
      sortable: false,
      filter: false,
      resizable: false,
      pinned: 'right',
      cellRenderer: (params: { value: string; data: StrategyDto }) => {
        const container = document.createElement('div');
        container.className = 'grid-actions';

        const commentsBtn = document.createElement('button');
        commentsBtn.className = 'grid-action-btn';
        commentsBtn.title = 'View comments';
        commentsBtn.innerHTML = '&#x1F4AC;';
        commentsBtn.addEventListener('click', () => this.openComments(params.data));

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'grid-delete-btn';
        deleteBtn.title = 'Delete strategy';
        deleteBtn.innerHTML = '&#x1F5D1;';
        deleteBtn.addEventListener('click', () => this.requestDelete(params.value));

        container.appendChild(commentsBtn);
        container.appendChild(deleteBtn);
        return container;
      },
    };

    return [nameDef, symbolDef, timeframeDef, ...kpiDefs, deleteDef];
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
    this.strategyService.getByAccount(this.accountId, 1, 20).subscribe({
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
