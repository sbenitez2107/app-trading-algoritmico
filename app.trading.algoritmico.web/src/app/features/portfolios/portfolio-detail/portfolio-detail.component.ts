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
  CellValueChangedEvent,
  ICellRendererParams,
  ValueFormatterParams,
  themeQuartz,
} from 'ag-grid-community';
import { formatCurrency } from '../../../shared/utils/format';
import { symbolToColor } from '../../../shared/utils/symbol-color';
import {
  PortfolioService,
  PortfolioDto,
  PortfolioAnalyticsDto,
  PortfolioRiskDto,
  PortfolioEquityPointDto,
  MonthlyReturnDto,
  StrategyCandidateDto,
  ServiceGuardrailDto,
  FundingService,
  DrawdownModel,
} from '../../../core/services/portfolio.service';
import { EquityChartComponent } from '../equity-chart/equity-chart.component';
import { MonthlyHeatmapComponent } from '../monthly-heatmap/monthly-heatmap.component';
import { SymbolDonutComponent } from '../symbol-donut/symbol-donut.component';

interface KpiCard {
  label: string;
  value: string;
  tone: 'neutral' | 'good' | 'bad';
}

interface StatItem {
  label: string;
  value: string;
}

interface StatGroup {
  title: string;
  items: StatItem[];
}

interface CompositionRow {
  strategyId: string;
  strategyName: string;
  accountName?: string;
  broker?: string;
  symbol?: string;
  timeframe?: string;
  magicNumber?: number;
  weight: number;
  normalizedWeight: number | null;
  contributionPercent: number | null;
  weightedNetProfit: number | null;
  isTotal?: boolean;
  // SQX (Backtest)
  totalProfit?: number;
  numberOfTrades?: number;
  sharpeRatio?: number;
  profitFactor?: number;
  winningPercentage?: number;
  drawdown?: number;
  // MT4 (Live)
  liveTradeCount?: number;
  liveNetProfit?: number;
  liveTotalReturn?: number;
  liveWinRate?: number;
  liveProfitFactor?: number;
  liveMaxDrawdownPercent?: number;
  liveSharpeRatio?: number;
}

type Tab = 'overview' | 'composition' | 'risk';

@Component({
  selector: 'app-portfolio-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AgGridAngular,
    EquityChartComponent,
    MonthlyHeatmapComponent,
    SymbolDonutComponent,
  ],
  templateUrl: './portfolio-detail.component.html',
  styleUrl: './portfolio-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfolioDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PortfolioService);

  private portfolioId = '';
  private portfoliosBase = '/portfolios';

  readonly portfolio = signal<PortfolioDto | null>(null);
  readonly analytics = signal<PortfolioAnalyticsDto | null>(null);
  readonly risk = signal<PortfolioRiskDto | null>(null);
  readonly equityCurve = signal<PortfolioEquityPointDto[]>([]);
  readonly monthlyReturns = signal<MonthlyReturnDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeTab = signal<Tab>('overview');

  readonly showAddMember = signal(false);
  readonly showLimitsModal = signal(false);
  readonly savingLimits = signal(false);
  readonly FundingService = FundingService;
  readonly DrawdownModel = DrawdownModel;
  limitsForm = {
    broker: '',
    fundingService: FundingService.Other,
    dailyLossPct: null as number | null,
    maxLossPct: null as number | null,
    profitTargetPct: null as number | null,
    drawdownModel: DrawdownModel.Static,
    verified: false,
  };
  readonly candidates = signal<StrategyCandidateDto[]>([]);
  /** Per-strategy SQX + live KPIs (keyed by strategyId) for the composition comparison grid. */
  readonly memberKpis = signal<Map<string, StrategyCandidateDto>>(new Map());
  addStrategyId = '';
  addWeight = 1;

  readonly kpis = computed<KpiCard[]>(() => {
    const a = this.analytics();
    if (!a) return [];
    return [
      { label: 'Net Profit', value: this.money(a.netProfit), tone: this.tone(a.netProfit) },
      { label: 'Total Return', value: this.pct(a.totalReturn), tone: this.tone(a.totalReturn) },
      { label: 'CAGR', value: this.pct(a.cagr), tone: this.tone(a.cagr) },
      {
        label: 'Max Drawdown',
        value: this.pct(a.maxDrawdownPercent),
        tone: a.maxDrawdownPercent > 0 ? 'bad' : 'neutral',
      },
      {
        label: 'Return / DD',
        value: this.num(a.returnDrawdownRatio),
        tone: this.tone(a.returnDrawdownRatio),
      },
      { label: 'Sharpe', value: this.num(a.sharpeRatio), tone: this.tone(a.sharpeRatio) },
      {
        label: 'Profit Factor',
        value: this.num(a.profitFactor),
        tone: a.profitFactor >= 1 ? 'good' : 'bad',
      },
      { label: 'SQN', value: this.num(a.sqn), tone: this.tone(a.sqn) },
      { label: 'Win Rate', value: this.pct(a.winRate), tone: 'neutral' },
      { label: 'Trades', value: String(a.tradeCount), tone: 'neutral' },
      { label: 'Exposure', value: this.pct(a.exposure), tone: 'neutral' },
      { label: 'Final Equity', value: this.money(a.finalEquity), tone: 'neutral' },
    ];
  });

  /** Full SQX-style stats block (Retornos / Riesgo / Trades). */
  readonly statGroups = computed<StatGroup[]>(() => {
    const a = this.analytics();
    if (!a) return [];
    return [
      {
        title: 'Rendimiento y Riesgo',
        items: [
          { label: 'Total Return', value: this.pct(a.totalReturn) },
          { label: 'CAGR', value: this.pct(a.cagr) },
          { label: 'Profit diario prom.', value: this.money(a.dailyAvgProfit) },
          { label: 'Profit mensual prom.', value: this.money(a.monthlyAvgProfit) },
          { label: 'Profit anual prom.', value: this.money(a.yearlyAvgProfit) },
          { label: 'AHPR', value: this.pct(a.ahpr) },
          { label: 'Equity final', value: this.money(a.finalEquity) },
          { label: 'Max Drawdown $', value: this.money(a.maxDrawdownAmount) },
          { label: 'Max Drawdown %', value: this.pct(a.maxDrawdownPercent) },
          { label: 'Return / DD', value: this.num(a.returnDrawdownRatio) },
          { label: 'Ann. Return / Max DD', value: this.num(a.annualReturnMaxDdRatio) },
          { label: 'Sharpe Ratio', value: this.num(a.sharpeRatio) },
          { label: 'SQN', value: this.num(a.sqn) },
          { label: 'Stagnation (días)', value: String(a.stagnationInDays) },
          { label: 'Exposure', value: this.pct(a.exposure) },
        ],
      },
      {
        title: 'Trades',
        items: [
          { label: 'Trades', value: String(a.tradeCount) },
          { label: 'Trades / mes prom.', value: this.avgPerMonth(a.tradeCount, a.daysSpanned) },
          { label: 'Profit mensual prom.', value: this.money(a.monthlyAvgProfit) },
          { label: 'Wins / Losses', value: `${a.winCount} / ${a.lossCount}` },
          { label: 'Win %', value: this.pct(a.winRate) },
          { label: 'Profit Factor', value: this.num(a.profitFactor) },
          { label: 'Payout Ratio', value: this.num(a.payoutRatio) },
          { label: 'Wins/Losses Ratio', value: this.num(a.winsLossesRatio) },
          { label: 'Expectancy', value: this.money(a.expectancy) },
          { label: 'R-Expectancy', value: this.num(a.rExpectancy) },
          { label: 'Gross Profit', value: this.money(a.grossProfit) },
          { label: 'Gross Loss', value: this.money(a.grossLoss) },
          { label: 'Avg Trade', value: this.money(a.averageTrade) },
          { label: 'Avg Win', value: this.money(a.averageWin) },
          { label: 'Avg Loss', value: this.money(a.averageLoss) },
          { label: 'Largest Win', value: this.money(a.largestWin) },
          { label: 'Largest Loss', value: this.money(a.largestLoss) },
          { label: 'Max Consec. Wins', value: String(a.maxConsecutiveWins) },
          { label: 'Max Consec. Losses', value: String(a.maxConsecutiveLosses) },
          { label: 'Avg Consec. Wins', value: this.num(a.averageConsecutiveWins) },
          { label: 'Avg Consec. Losses', value: this.num(a.averageConsecutiveLosses) },
          { label: 'Desviación', value: this.money(a.standardDeviation) },
          { label: 'Z-Score', value: this.num(a.zScore) },
          { label: 'Z-Probability', value: this.pct(a.zProbability) },
        ],
      },
    ];
  });

  // ---- Composition grid (sortable ag-grid, same theme as strategies) ----
  readonly gridTheme = themeQuartz;
  readonly compositionRowId = (p: { data: CompositionRow }) => p.data.strategyId;

  readonly compositionDefaultColDef: ColDef<CompositionRow> = {
    sortable: true,
    filter: true,
    resizable: true,
    suppressHeaderMenuButton: true,
  };

  readonly compositionRows = computed<CompositionRow[]>(() => {
    const p = this.portfolio();
    if (!p) return [];
    const contrib = new Map(this.analytics()?.members.map((m) => [m.strategyId, m]) ?? []);
    const kpis = this.memberKpis();
    return p.members.map((m) => {
      const c = contrib.get(m.strategyId);
      const k = kpis.get(m.strategyId);
      return {
        strategyId: m.strategyId,
        strategyName: m.strategyName,
        accountName: m.accountName,
        broker: m.broker,
        symbol: k?.symbol,
        timeframe: k?.timeframe,
        magicNumber: k?.magicNumber,
        weight: m.weight,
        normalizedWeight: c?.normalizedWeight ?? null,
        contributionPercent: c?.contributionPercent ?? null,
        weightedNetProfit: c?.weightedNetProfit ?? null,
        totalProfit: k?.totalProfit,
        numberOfTrades: k?.numberOfTrades,
        sharpeRatio: k?.sharpeRatio,
        profitFactor: k?.profitFactor,
        winningPercentage: k?.winningPercentage,
        drawdown: k?.drawdown,
        liveTradeCount: k?.liveTradeCount,
        liveNetProfit: k?.liveNetProfit,
        liveTotalReturn: k?.liveTotalReturn,
        liveWinRate: k?.liveWinRate,
        liveProfitFactor: k?.liveProfitFactor,
        liveMaxDrawdownPercent: k?.liveMaxDrawdownPercent,
        liveSharpeRatio: k?.liveSharpeRatio,
      };
    });
  });

  /** Pinned bottom TOTAL row: portfolio-level COMBINED values (recomputed, not column sums). */
  readonly compositionPinnedTotal = computed<CompositionRow[]>(() => {
    const a = this.analytics();
    const p = this.portfolio();
    if (!a || !p || p.members.length === 0) return [];
    return [
      {
        strategyId: '__total__',
        strategyName: 'TOTAL (combinado)',
        weight: p.members.reduce((s, m) => s + m.weight, 0),
        normalizedWeight: 1,
        contributionPercent: 1,
        weightedNetProfit: a.netProfit,
        isTotal: true,
        // MT4 group shows the portfolio's COMBINED live values (note: NOT column sums — diversified).
        liveTradeCount: a.tradeCount,
        liveNetProfit: a.netProfit,
        liveTotalReturn: a.totalReturn,
        liveWinRate: a.winRate,
        liveProfitFactor: a.profitFactor,
        liveMaxDrawdownPercent: a.maxDrawdownPercent,
        liveSharpeRatio: a.sharpeRatio,
      },
    ];
  });

  readonly compositionColumnDefs: (ColDef<CompositionRow> | ColGroupDef<CompositionRow>)[] = [
    { field: 'strategyName', headerName: 'Estrategia', pinned: 'left', minWidth: 200, flex: 1 },
    {
      field: 'symbol',
      headerName: 'Symbol',
      width: 120,
      pinned: 'left',
      cellStyle: (p: { value: string | null }) => ({
        backgroundColor: symbolToColor(p.value) + '20',
        borderLeft: `3px solid ${symbolToColor(p.value)}`,
      }),
    },
    { field: 'timeframe', headerName: 'TF', width: 70 },
    { field: 'broker', headerName: 'Broker', width: 120 },
    {
      headerName: 'Asignación',
      headerClass: 'col-group-alloc',
      children: [
        {
          field: 'weight',
          headerName: 'Peso',
          width: 100,
          editable: (p) => !p.node.rowPinned,
          cellEditor: 'agNumberCellEditor',
          cellEditorParams: { min: 0, precision: 4 },
          cellClass: (p) => (p.node.rowPinned ? '' : 'comp-grid__editable'),
        },
        {
          field: 'normalizedWeight',
          headerName: 'Peso Norm.',
          width: 120,
          valueFormatter: (p) => (p.value == null ? '—' : this.pct(p.value)),
        },
        {
          field: 'contributionPercent',
          headerName: 'Contribución',
          width: 130,
          valueFormatter: (p) => (p.value == null ? '—' : this.pct(p.value)),
        },
        {
          field: 'weightedNetProfit',
          headerName: 'Aporte $',
          width: 120,
          headerTooltip:
            'Peso × PnL de la estrategia. La suma de esta columna = Net Profit del portfolio.',
          valueFormatter: (p) => (p.value == null ? '—' : formatCurrency(p.value)),
          cellStyle: (p) => this.signColor(p.value),
        },
      ],
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
          valueFormatter: (p: ValueFormatterParams<CompositionRow>) => formatCurrency(p.value),
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
    {
      headerName: '',
      width: 60,
      sortable: false,
      filter: false,
      pinned: 'right',
      cellRenderer: (params: ICellRendererParams<CompositionRow>) => {
        if (params.node.rowPinned || !params.data) return '';
        const btn = document.createElement('button');
        btn.textContent = '🗑️';
        btn.title = 'Quitar';
        btn.className = 'comp-grid__remove';
        const id = params.data.strategyId;
        btn.addEventListener('click', () => this.removeMember(id));
        return btn;
      },
    },
  ];

  onCompositionCellChanged(e: CellValueChangedEvent<CompositionRow>): void {
    if (e.colDef.field === 'weight' && e.data) {
      this.saveWeight(e.data.strategyId, e.newValue);
    }
  }

  ngOnInit(): void {
    this.portfolioId = this.route.snapshot.params['id'];
    this.portfoliosBase = this.route.snapshot.data['portfoliosBase'] ?? '/portfolios';
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.getById(this.portfolioId).subscribe({
      next: (p) => {
        this.portfolio.set(p);
        this.isLoading.set(false);
        this.loadAnalytics();
        // Per-strategy KPIs for the composition comparison grid (reuses the candidates endpoint).
        this.service.getCandidates(p.broker, p.accountType).subscribe({
          next: (list) => this.memberKpis.set(new Map(list.map((c) => [c.id, c]))),
          error: () => this.memberKpis.set(new Map()),
        });
      },
      error: () => {
        this.error.set('No se pudo cargar el portfolio');
        this.isLoading.set(false);
      },
    });
  }

  loadAnalytics(): void {
    this.service.getAnalytics(this.portfolioId).subscribe({
      next: (a) => this.analytics.set(a),
      error: () => this.analytics.set(null),
    });
    this.service.getEquityCurve(this.portfolioId).subscribe({
      next: (c) => this.equityCurve.set(c),
      error: () => this.equityCurve.set([]),
    });
    this.service.getMonthlyReturns(this.portfolioId).subscribe({
      next: (m) => this.monthlyReturns.set(m),
      error: () => this.monthlyReturns.set([]),
    });
  }

  loadRisk(): void {
    this.service.getRisk(this.portfolioId).subscribe({
      next: (r) => this.risk.set(r),
      error: () => this.risk.set(null),
    });
  }

  /** Risk is recomputed from current trades — drop the cache when membership/weights change. */
  private invalidateRisk(): void {
    this.risk.set(null);
    if (this.activeTab() === 'risk') this.loadRisk();
  }

  // ---- prop-firm guardrail limits ----

  openLimitsEditor(g: ServiceGuardrailDto): void {
    this.limitsForm = {
      broker: g.service,
      fundingService: g.fundingService,
      dailyLossPct: g.dailyLossLimitPct != null ? g.dailyLossLimitPct * 100 : null,
      maxLossPct: g.maxLossLimitPct != null ? g.maxLossLimitPct * 100 : null,
      profitTargetPct: g.profitTargetPct != null ? g.profitTargetPct * 100 : null,
      drawdownModel: g.drawdownModel ?? DrawdownModel.Static,
      verified: g.verified,
    };
    this.showLimitsModal.set(true);
  }

  saveLimits(): void {
    const f = this.limitsForm;
    const toFrac = (v: number | null) => (v == null || isNaN(v) ? undefined : v / 100);
    this.savingLimits.set(true);
    this.service
      .upsertRiskLimits({
        broker: f.broker,
        fundingService: f.fundingService,
        dailyLossLimitPct: toFrac(f.dailyLossPct),
        maxLossLimitPct: toFrac(f.maxLossPct),
        profitTargetPct: toFrac(f.profitTargetPct),
        drawdownModel: f.drawdownModel,
        verified: f.verified,
      })
      .subscribe({
        next: () => {
          this.savingLimits.set(false);
          this.showLimitsModal.set(false);
          this.loadRisk();
        },
        error: () => {
          this.savingLimits.set(false);
          this.error.set('No se pudieron guardar los límites');
        },
      });
  }

  fundingLabel(fs: FundingService): string {
    switch (fs) {
      case FundingService.Ftmo:
        return 'FTMO';
      case FundingService.Axi:
        return 'Axi';
      case FundingService.DarwinexZero:
        return 'Darwinex Zero';
      default:
        return 'Otro';
    }
  }

  back(): void {
    this.router.navigate([this.portfoliosBase]);
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
    if (tab === 'risk' && !this.risk()) this.loadRisk();
  }

  // ---- members ----

  openAddMember(): void {
    const p = this.portfolio();
    if (!p) return;
    this.addStrategyId = '';
    this.addWeight = 1;
    this.showAddMember.set(true);
    this.service.getCandidates(p.broker, p.accountType).subscribe({
      next: (list) => {
        const memberIds = new Set(p.members.map((m) => m.strategyId));
        this.candidates.set(list.filter((c) => !memberIds.has(c.id)));
      },
      error: () => this.candidates.set([]),
    });
  }

  submitAddMember(): void {
    if (!this.addStrategyId) return;
    this.service
      .addMember(this.portfolioId, { strategyId: this.addStrategyId, weight: this.addWeight })
      .subscribe({
        next: (p) => {
          this.portfolio.set(p);
          this.showAddMember.set(false);
          this.loadAnalytics();
          this.invalidateRisk();
        },
        error: (err) => this.error.set(err?.error?.error ?? 'No se pudo agregar la estrategia'),
      });
  }

  removeMember(strategyId: string): void {
    if (!confirm('¿Quitar esta estrategia del portfolio?')) return;
    this.service.removeMember(this.portfolioId, strategyId).subscribe({
      next: (p) => {
        this.portfolio.set(p);
        this.loadAnalytics();
        this.invalidateRisk();
      },
      error: () => this.error.set('No se pudo quitar la estrategia'),
    });
  }

  saveWeight(strategyId: string, value: string | number): void {
    const weight = Number(value);
    if (!Number.isFinite(weight) || weight < 0) return;
    this.service.updateMemberWeight(this.portfolioId, strategyId, weight).subscribe({
      next: (p) => {
        this.portfolio.set(p);
        this.loadAnalytics();
        this.invalidateRisk();
      },
      error: () => this.error.set('No se pudo actualizar el peso'),
    });
  }

  // ---- formatters ----

  money(v: number): string {
    const cur = this.portfolio()?.baseCurrency ?? 'USD';
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: cur,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(v);
  }

  pct(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${(v * 100).toFixed(2)}%`;
  }

  num(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : v.toFixed(2);
  }

  /** For values already stored as a percent number (SQX Win % = 51.96 → "51.96%"). */
  numPct(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${v.toFixed(2)}%`;
  }

  /** Average count per ~month from a total and the day span (30.4375 days/month). */
  private avgPerMonth(count: number, daysSpanned: number): string {
    if (daysSpanned <= 0) return count.toFixed(1);
    return (count / (daysSpanned / 30.4375)).toFixed(1);
  }

  signColor(v: number | null | undefined): { color: string } | null {
    if (v === null || v === undefined || v === 0) return null;
    return { color: v > 0 ? '#22c55e' : '#ff3b30' };
  }

  private tone(v: number): 'good' | 'bad' | 'neutral' {
    if (v > 0) return 'good';
    if (v < 0) return 'bad';
    return 'neutral';
  }
}
