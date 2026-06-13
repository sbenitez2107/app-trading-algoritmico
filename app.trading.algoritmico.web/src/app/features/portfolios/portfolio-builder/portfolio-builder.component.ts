import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  computed,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import {
  ColDef,
  ColGroupDef,
  GridApi,
  GridReadyEvent,
  RowSelectionOptions,
  SelectionChangedEvent,
  ValueFormatterParams,
  themeQuartz,
} from 'ag-grid-community';
import {
  PortfolioService,
  AccountType,
  StrategyCandidateDto,
} from '../../../core/services/portfolio.service';
import { formatCurrency } from '../../../shared/utils/format';
import { symbolToColor } from '../../../shared/utils/symbol-color';

interface AccountOption {
  accountId: string;
  accountName: string;
  broker: string;
  count: number;
}

@Component({
  selector: 'app-portfolio-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, AgGridAngular],
  templateUrl: './portfolio-builder.component.html',
  styleUrl: './portfolio-builder.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfolioBuilderComponent implements OnInit {
  private readonly service = inject(PortfolioService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private gridApi?: GridApi<StrategyCandidateDto>;

  readonly AccountType = AccountType;

  broker = '';
  private portfoliosBase = '/portfolios';

  readonly accountType = signal<AccountType>(AccountType.Demo);
  readonly candidates = signal<StrategyCandidateDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly creating = signal(false);

  readonly selectedAccountIds = signal<Set<string>>(new Set());
  readonly selectedStrategyIds = signal<Set<string>>(new Set());

  // Signals (not plain fields) so `canCreate` reacts to name/capital edits, not just selection.
  readonly name = signal('');
  readonly capital = signal<number>(100000);

  // ---- ag-grid (same theme + format as the MT4/SQX strategies grids) ----
  readonly gridTheme = themeQuartz;

  readonly rowSelection: RowSelectionOptions = {
    mode: 'multiRow',
    checkboxes: true,
    headerCheckbox: true,
    enableClickSelection: true,
  };

  readonly defaultColDef: ColDef<StrategyCandidateDto> = {
    sortable: true,
    filter: true,
    resizable: true,
    suppressHeaderMenuButton: true,
  };

  readonly columnDefs: (ColDef<StrategyCandidateDto> | ColGroupDef<StrategyCandidateDto>)[] = [
    { field: 'name', headerName: 'Estrategia', pinned: 'left', minWidth: 220, flex: 1 },
    {
      field: 'symbol',
      headerName: 'Symbol',
      width: 120,
      cellStyle: (p: { value: string | null }) => ({
        backgroundColor: symbolToColor(p.value) + '20',
        borderLeft: `3px solid ${symbolToColor(p.value)}`,
      }),
    },
    { field: 'timeframe', headerName: 'TF', width: 80 },
    {
      headerName: 'Cuenta',
      minWidth: 170,
      valueGetter: (p) => (p.data ? `${p.data.broker} · ${p.data.accountName}` : ''),
    },
    {
      headerName: 'SQX (Backtest)',
      headerClass: 'col-group-sqx',
      children: [
        {
          field: 'totalProfit',
          headerName: 'Total Profit',
          width: 120,
          valueFormatter: (p) => formatCurrency(p.value),
        },
        { field: 'numberOfTrades', headerName: 'Trades', width: 90 },
        {
          field: 'sharpeRatio',
          headerName: 'Sharpe',
          width: 90,
          valueFormatter: (p) => this.num(p.value),
        },
        {
          field: 'profitFactor',
          headerName: 'PF',
          width: 80,
          valueFormatter: (p) => this.num(p.value),
        },
        {
          field: 'winningPercentage',
          headerName: 'Win %',
          width: 90,
          valueFormatter: (p) => this.numPct(p.value),
        },
        {
          field: 'drawdown',
          headerName: 'Drawdown',
          width: 120,
          valueFormatter: (p) => formatCurrency(p.value),
        },
      ],
    },
    {
      headerName: 'MT4 (Live)',
      headerClass: 'col-group-mt4',
      children: [
        { field: 'magicNumber', headerName: 'Magic', width: 100 },
        { field: 'liveTradeCount', headerName: '# Trades', width: 100 },
        {
          field: 'liveNetProfit',
          headerName: 'Net Profit',
          width: 120,
          valueFormatter: (p: ValueFormatterParams<StrategyCandidateDto>) =>
            formatCurrency(p.value),
          cellStyle: (p) => this.signColor(p.value),
        },
        {
          field: 'liveTotalReturn',
          headerName: 'Total Return %',
          width: 130,
          valueFormatter: (p) => this.pct(p.value),
          cellStyle: (p) => this.signColor(p.value),
        },
        {
          field: 'liveWinRate',
          headerName: 'Win %',
          width: 90,
          valueFormatter: (p) => this.pct(p.value),
        },
        {
          field: 'liveProfitFactor',
          headerName: 'PF',
          width: 80,
          valueFormatter: (p) => this.num(p.value),
        },
        {
          field: 'liveMaxDrawdownPercent',
          headerName: 'Max DD %',
          width: 110,
          valueFormatter: (p) => this.pct(p.value),
          cellStyle: () => ({ color: '#ff3b30' }),
        },
        {
          field: 'liveSharpeRatio',
          headerName: 'Sharpe',
          width: 90,
          valueFormatter: (p) => this.num(p.value),
        },
      ],
    },
  ];

  readonly accounts = computed<AccountOption[]>(() => {
    const map = new Map<string, AccountOption>();
    for (const c of this.candidates()) {
      const existing = map.get(c.accountId);
      if (existing) existing.count++;
      else
        map.set(c.accountId, {
          accountId: c.accountId,
          accountName: c.accountName,
          broker: c.broker,
          count: 1,
        });
    }
    return [...map.values()];
  });

  readonly filtered = computed<StrategyCandidateDto[]>(() => {
    const accs = this.selectedAccountIds();
    return this.candidates().filter((c) => accs.has(c.accountId));
  });

  readonly selectedCount = computed(() => this.selectedStrategyIds().size);

  readonly canCreate = computed(
    () => this.name().trim().length > 0 && this.capital() > 0 && this.selectedCount() > 0,
  );

  ngOnInit(): void {
    this.broker = this.route.snapshot.data['broker'] ?? '';
    this.portfoliosBase = this.route.snapshot.data['portfoliosBase'] ?? '/portfolios';
    this.loadCandidates();
  }

  setType(t: AccountType): void {
    if (this.accountType() === t) return;
    this.accountType.set(t);
    this.loadCandidates();
  }

  loadCandidates(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.selectedStrategyIds.set(new Set());
    this.service.getCandidates(this.broker, this.accountType()).subscribe({
      next: (list) => {
        this.candidates.set(list);
        this.selectedAccountIds.set(new Set(list.map((c) => c.accountId)));
        this.isLoading.set(false);
      },
      error: () => {
        this.candidates.set([]);
        this.error.set('No se pudieron cargar las estrategias');
        this.isLoading.set(false);
      },
    });
  }

  // ---- account filter ----
  isAccountSelected(accountId: string): boolean {
    return this.selectedAccountIds().has(accountId);
  }

  toggleAccount(accountId: string): void {
    this.selectedAccountIds.update((s) => {
      const next = new Set(s);
      if (next.has(accountId)) next.delete(accountId);
      else next.add(accountId);
      return next;
    });
  }

  allAccountsSelected(): boolean {
    return this.accounts().length > 0 && this.selectedAccountIds().size === this.accounts().length;
  }

  toggleAllAccounts(): void {
    if (this.allAccountsSelected()) this.selectedAccountIds.set(new Set());
    else this.selectedAccountIds.set(new Set(this.accounts().map((a) => a.accountId)));
  }

  // ---- ag-grid wiring ----
  onGridReady(e: GridReadyEvent<StrategyCandidateDto>): void {
    this.gridApi = e.api;
  }

  onSelectionChanged(e: SelectionChangedEvent<StrategyCandidateDto>): void {
    this.selectedStrategyIds.set(new Set(e.api.getSelectedRows().map((r) => r.id)));
  }

  create(): void {
    if (!this.canCreate()) return;
    this.creating.set(true);
    const members = [...this.selectedStrategyIds()].map((strategyId) => ({
      strategyId,
      weight: 1,
    }));
    this.service
      .create({
        name: this.name().trim(),
        broker: this.broker,
        accountType: this.accountType(),
        initialCapital: this.capital(),
        baseCurrency: 'USD',
        members,
      })
      .subscribe({
        next: (created) => {
          this.creating.set(false);
          this.router.navigate([this.portfoliosBase, created.id]);
        },
        error: (err) => {
          this.creating.set(false);
          this.error.set(err?.error?.error ?? 'No se pudo crear el portfolio');
        },
      });
  }

  cancel(): void {
    this.router.navigate([this.portfoliosBase]);
  }

  // ---- formatters ----
  private pct(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${(v * 100).toFixed(2)}%`;
  }

  /** For values already stored as a percent number (e.g. SQX Win % = 51.96 → "51.96%"). */
  private numPct(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${v.toFixed(2)}%`;
  }

  private num(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : v.toFixed(2);
  }

  private signColor(v: number | null | undefined): { color: string } | null {
    if (v === null || v === undefined || v === 0) return null;
    return { color: v > 0 ? '#22c55e' : '#ff3b30' };
  }
}
