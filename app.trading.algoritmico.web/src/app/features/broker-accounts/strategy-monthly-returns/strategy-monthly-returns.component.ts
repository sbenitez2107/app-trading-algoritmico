import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  signal,
  computed,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  StrategyService,
  StrategyMonthlyReturnsDto,
  MonthlyReturnDto,
} from '../../../core/services/strategy.service';
import { AccountType } from '../../../core/services/portfolio.service';
import { CreatePortfolioModalComponent } from '../../portfolios/create-portfolio-modal/create-portfolio-modal.component';
import { symbolToColor } from '../../../shared/utils/symbol-color';
import {
  MONTHLY_METRICS,
  MonthlyMetric,
  formatMonthlyCell,
  formatMonthlyColumnGrandTotal,
  formatMonthlyColumnTotal,
  monthlyColumnGrandTotal,
  monthlyColumnTotal,
  formatMonthlyTotalCell,
  isLowerBetter,
  monthlyMetricCellStyle,
  monthlyMetricTooltip,
  monthlyMetricTotal,
  monthlyMetricValue,
} from '../../../shared/utils/monthly-metric';
import { readViewPreference, writeViewPreference } from '../../../shared/utils/view-preference';

interface MonthlyViewRow {
  strategyId: string;
  name: string;
  symbol: string | null;
  timeframe: string | null;
  /** 12 entries of the SELECTED metric, null = the month has no value to show. Drives sorting
   *  and the heatmap; what the cell READS is `monthTexts`, which the win rate formats differently. */
  months: (number | null)[];
  /** 12 entries of rendered cell text, aligned with `months`. */
  monthTexts: string[];
  /** 12 entries of extra cell detail (win/loss counts), null = nothing to add. */
  tooltips: (string | null)[];
  total: number | null;
  totalText: string;
  hasData: boolean;
  /**
   * Per-month extremes, read from the month sources rather than the selected metric, so the gate
   * filters keep working while the matrix shows something else. Null when no month of the selected
   * year reports that quantity.
   */
  worstMonthReturn: number | null;
  worstMonthWinRate: number | null;
  worstMonthMaxDd: number | null;
  worstMonthTrades: number | null;
  totalTrades: number | null;
  /** The 12 raw month slots, kept so the summary row can aggregate anything the cells hide. */
  sources: (MonthlyReturnDto | null)[];
}

/**
 * Threshold parser for the gate filters. Zero and negatives are meaningful here (every month above
 * 0% is the whole point), so only a blank or unparseable box disables them.
 */
function parseThreshold(value: string): number | null {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : null;
}

/**
 * Trade counts are whole numbers, and a bar of 0 trades filters nothing. A typed decimal rounds UP,
 * so the box never claims to accept a count no strategy can actually reach.
 */
function parseTradeCount(value: string): number | null {
  const parsed = Number.parseFloat(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return null;
  return Math.ceil(parsed);
}

/** Smallest value across the months that report one, or null when none do. */
function lowestOf(values: (number | null)[]): number | null {
  const present = values.filter((v): v is number => v !== null);
  return present.length === 0 ? null : Math.min(...present);
}

/** Largest value across the months that report one, or null when none do. */
function highestOf(values: (number | null)[]): number | null {
  const present = values.filter((v): v is number => v !== null);
  return present.length === 0 ? null : Math.max(...present);
}

/** One cell of the summary row: its rendered text and the sign it should read as. */
interface SummaryCell {
  text: string;
  tone: 'pos' | 'neg' | '';
}

/** Sign of the year total a row must carry to survive the filter; `all` disables it. */
export type TotalFilter = 'all' | 'pos' | 'neg';

/** Sortable columns: text fields, the year total, or a month index (0-11). */
export type MonthlySortKey = 'name' | 'symbol' | 'timeframe' | 'total' | number;

/** Text columns, which sort alphabetically and start ascending. */
const TEXT_SORT_KEYS: ReadonlySet<MonthlySortKey> = new Set<MonthlySortKey>([
  'name',
  'symbol',
  'timeframe',
]);

@Component({
  selector: 'app-strategy-monthly-returns',
  standalone: true,
  imports: [CommonModule, CreatePortfolioModalComponent],
  templateUrl: './strategy-monthly-returns.component.html',
  styleUrl: './strategy-monthly-returns.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyMonthlyReturnsComponent {
  readonly accountId = input.required<string>();
  /**
   * Broker and account type of the account this matrix belongs to. Taken from the loaded account
   * rather than route data, because they are the portfolio's data, not this screen's routing
   * config. Null until the account resolves, which keeps the create button hidden.
   */
  readonly broker = input<string | null>(null);
  readonly accountType = input<AccountType | null>(null);

  private readonly strategyService = inject(StrategyService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly rows = signal<StrategyMonthlyReturnsDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedYear = signal(new Date().getFullYear());
  /**
   * Remembered per screen, so the per-strategy matrix can sit on a different metric than the other
   * one — reading drawdowns here and returns there is a normal way to work.
   */
  private readonly metricStorageKey = 'monthly_metric_strategies';
  readonly metric = signal<MonthlyMetric>(
    readViewPreference(this.metricStorageKey, MONTHLY_METRICS, 'return'),
  );

  readonly metricOptions: ReadonlyArray<{ value: MonthlyMetric; label: string; hint: string }> = [
    { value: 'return', label: 'Return', hint: "The month's compounding return" },
    {
      value: 'maxDrawdown',
      label: 'Max DD',
      hint: 'Worst drawdown produced INSIDE the month — the peak resets on the 1st, so it measures how much that month hurt',
    },
    {
      value: 'underwater',
      label: 'Underwater',
      hint: 'Deepest distance below the all-time peak during the month — the same drawdown repeats until a new high is made',
    },
    { value: 'winRate', label: 'W/L', hint: 'Wins / (wins + losses) for the month' },
  ];

  readonly totalFilterOptions: ReadonlyArray<{ value: TotalFilter; label: string; hint: string }> =
    [
      { value: 'all', label: 'All', hint: 'Every strategy' },
      { value: 'pos', label: 'Positive', hint: 'Only strategies whose year total reads green' },
      { value: 'neg', label: 'Negative', hint: 'Only strategies whose year total reads red' },
    ];

  readonly title = computed(() => {
    switch (this.metric()) {
      case 'return':
        return 'Monthly Returns (Compounding)';
      case 'maxDrawdown':
        return 'Max Drawdown Within Month';
      case 'underwater':
        return 'Underwater (All-Time Peak)';
      case 'winRate':
        return 'Monthly Win Rate';
    }
  });

  /** Header of the year column, which aggregates differently per metric. */
  readonly totalLabel = computed(() => {
    const metric = this.metric();
    if (isLowerBetter(metric)) return 'Worst';
    return metric === 'winRate' ? 'Overall' : 'Total';
  });

  readonly monthLabels = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];

  constructor() {
    effect(() => {
      const id = this.accountId();
      this.isLoading.set(true);
      this.error.set(null);
      this.strategyService.getMonthlyReturnsByAccount(id).subscribe({
        next: (rows) => {
          this.rows.set(rows);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? err?.message ?? 'Failed to load monthly returns.');
          this.isLoading.set(false);
        },
      });
    });
  }

  /** Year navigation bounds: earliest year with data up to the current year. */
  readonly yearBounds = computed(() => {
    const current = new Date().getFullYear();
    const years = this.rows().flatMap((r) => r.returns.map((m) => m.year));
    if (years.length === 0) return { min: current, max: current };
    return { min: Math.min(...years, current), max: Math.max(...years, current) };
  });

  readonly canPrev = computed(() => this.selectedYear() > this.yearBounds().min);
  readonly canNext = computed(() => this.selectedYear() < this.yearBounds().max);

  readonly viewRows = computed<MonthlyViewRow[]>(() => {
    const year = this.selectedYear();
    const metric = this.metric();

    return this.rows().map((r) => {
      const sources: (MonthlyReturnDto | null)[] = Array(12).fill(null);
      for (const m of r.returns) {
        if (m.year === year) sources[m.month - 1] = m;
      }

      const traded = sources.filter((m): m is MonthlyReturnDto => m !== null);

      return {
        strategyId: r.strategyId,
        name: r.name,
        symbol: r.symbol,
        timeframe: r.timeframe,
        months: sources.map((m) => (m === null ? null : monthlyMetricValue(m, metric))),
        monthTexts: sources.map((m) => formatMonthlyCell(m, metric)),
        tooltips: sources.map((m) => monthlyMetricTooltip(m, metric)),
        total: monthlyMetricTotal(sources, metric),
        totalText: formatMonthlyTotalCell(sources, metric),
        // Driven by the presence of months, NOT of values: a breakeven-only month reports no
        // win rate, but the strategy still traded that year.
        hasData: sources.some((m) => m !== null),
        worstMonthReturn: lowestOf(traded.map((m) => m.returnPercent)),
        // A month of nothing but breakeven trades has no win rate, so it is skipped rather than
        // counted as a zero that would sink every strategy.
        worstMonthWinRate: lowestOf(traded.map((m) => monthlyMetricValue(m, 'winRate'))),
        worstMonthMaxDd: highestOf(traded.map((m) => m.maxDrawdownPercent)),
        worstMonthTrades: lowestOf(traded.map((m) => m.tradeCount)),
        totalTrades: traded.length === 0 ? null : traded.reduce((acc, m) => acc + m.tradeCount, 0),
        sources,
      };
    });
  });

  /**
   * Filters are deliberately NOT persisted, unlike the metric: a narrowed matrix that survives a
   * reload reads as missing data, and the year total silently stops describing the account.
   */
  readonly nameFilter = signal('');
  readonly symbolFilter = signal<string | null>(null);
  readonly timeframeFilter = signal<string | null>(null);
  readonly totalFilter = signal<TotalFilter>('all');
  /**
   * Per-month gates: EVERY month that reports the quantity has to clear the threshold, so one bad
   * month disqualifies a strategy however good its year total looks. Percent units; null disables.
   */
  readonly maxDdFilter = signal<number | null>(null);
  readonly minReturnFilter = signal<number | null>(null);
  readonly minWinRateFilter = signal<number | null>(null);
  /** Trade counts: one on the year total, one as a per-month gate. Null disables. */
  readonly minTotalTradesFilter = signal<number | null>(null);
  readonly minMonthlyTradesFilter = signal<number | null>(null);

  /** Symbols present in the loaded data, for the symbol picker. */
  readonly availableSymbols = computed<string[]>(() => {
    const symbols = new Set<string>();
    for (const r of this.rows()) {
      if (r.symbol) symbols.add(r.symbol);
    }
    return [...symbols].sort((a, b) => a.localeCompare(b));
  });

  /** Timeframes present in the loaded data, for the timeframe picker. */
  readonly availableTimeframes = computed<string[]>(() => {
    const timeframes = new Set<string>();
    for (const r of this.rows()) {
      if (r.timeframe) timeframes.add(r.timeframe);
    }
    return [...timeframes].sort((a, b) => a.localeCompare(b));
  });

  readonly hasActiveFilters = computed(
    () =>
      this.nameFilter().trim() !== '' ||
      this.symbolFilter() !== null ||
      this.timeframeFilter() !== null ||
      this.totalFilter() !== 'all' ||
      this.maxDdFilter() !== null ||
      this.minReturnFilter() !== null ||
      this.minWinRateFilter() !== null ||
      this.minTotalTradesFilter() !== null ||
      this.minMonthlyTradesFilter() !== null,
  );

  readonly filteredRows = computed<MonthlyViewRow[]>(() => {
    const needle = this.nameFilter().trim().toLowerCase();
    // A selection left over from another account would blank the matrix with no way to tell why.
    const symbol = this.availableSymbols().includes(this.symbolFilter() ?? '')
      ? this.symbolFilter()
      : null;
    const timeframe = this.availableTimeframes().includes(this.timeframeFilter() ?? '')
      ? this.timeframeFilter()
      : null;
    const tone = this.totalFilter();
    const maxDd = this.maxDdFilter();
    const minReturn = this.minReturnFilter();
    const minWinRate = this.minWinRateFilter();
    const minTotalTrades = this.minTotalTradesFilter();
    const minMonthlyTrades = this.minMonthlyTradesFilter();

    /** Clears only when the worst month sits strictly above the threshold, in percent units. */
    const above = (worst: number | null, threshold: number) =>
      worst !== null && worst * 100 > threshold;

    return this.viewRows().filter((row) => {
      if (needle !== '' && !row.name.toLowerCase().includes(needle)) return false;
      if (symbol !== null && row.symbol !== symbol) return false;
      if (timeframe !== null && row.timeframe !== timeframe) return false;
      // Excludes an unknown drawdown rather than letting it through: a strategy with no imported
      // trades has nothing proving it stays under the bar, and this list feeds a real portfolio.
      // Every gate below rejects a row with nothing to judge, for the same reason: this list feeds
      // a real portfolio, and an absent month is not a passing month.
      if (maxDd !== null && (row.worstMonthMaxDd === null || row.worstMonthMaxDd * 100 >= maxDd))
        return false;
      if (minReturn !== null && !above(row.worstMonthReturn, minReturn)) return false;
      if (minWinRate !== null && !above(row.worstMonthWinRate, minWinRate)) return false;
      if (minMonthlyTrades !== null && (row.worstMonthTrades ?? -1) < minMonthlyTrades)
        return false;
      if (minTotalTrades !== null && (row.totalTrades ?? -1) < minTotalTrades) return false;
      // Reuses the colour rule so the filter means the same thing the cell shows: the win rate
      // splits at 50%, and a drawdown depth is never positive.
      if (tone !== 'all' && this.totalTone(row.total) !== tone) return false;
      return true;
    });
  });

  clearFilters(): void {
    this.nameFilter.set('');
    this.symbolFilter.set(null);
    this.timeframeFilter.set(null);
    this.totalFilter.set('all');
    this.maxDdFilter.set(null);
    this.minReturnFilter.set(null);
    this.minWinRateFilter.set(null);
    this.minTotalTradesFilter.set(null);
    this.minMonthlyTradesFilter.set(null);
  }

  /** Empty value means "every symbol", which is what the picker's first option submits. */
  setSymbolFilter(value: string): void {
    this.symbolFilter.set(value === '' ? null : value);
  }

  /** Empty value means "every timeframe". */
  setTimeframeFilter(value: string): void {
    this.timeframeFilter.set(value === '' ? null : value);
  }

  setMaxDdFilter(value: string): void {
    const parsed = Number.parseFloat(value);
    // A drawdown bar at or below 0% is one no month can clear, so it disables instead of blanking.
    this.maxDdFilter.set(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }

  setMinReturnFilter(value: string): void {
    this.minReturnFilter.set(parseThreshold(value));
  }

  setMinWinRateFilter(value: string): void {
    this.minWinRateFilter.set(parseThreshold(value));
  }

  setMinTotalTradesFilter(value: string): void {
    this.minTotalTradesFilter.set(parseTradeCount(value));
  }

  setMinMonthlyTradesFilter(value: string): void {
    this.minMonthlyTradesFilter.set(parseTradeCount(value));
  }

  readonly isCreatingPortfolio = signal(false);

  /** Frozen when the dialog opens, so editing a filter behind it cannot change what gets created. */
  readonly portfolioStrategyIds = signal<string[]>([]);

  /** Hidden until the account is known: broker and account type are not ours to guess. */
  readonly canCreatePortfolio = computed(
    () => this.accountType() !== null && !!this.broker() && this.filteredRows().length > 0,
  );

  openCreatePortfolio(): void {
    if (!this.canCreatePortfolio()) return;
    this.portfolioStrategyIds.set(this.filteredRows().map((r) => r.strategyId));
    this.isCreatingPortfolio.set(true);
  }

  closeCreatePortfolio(): void {
    this.isCreatingPortfolio.set(false);
  }

  /** The portfolio only becomes useful on its own screen, so creating it navigates there. */
  onPortfolioCreated(portfolioId: string): void {
    this.isCreatingPortfolio.set(false);
    const basePath = this.route.snapshot.data['basePath'] ?? '';
    this.router.navigate([`${basePath}/portfolios`, portfolioId]);
  }

  /**
   * The filtered set collapsed into one row, shown above the grid so the effect of a filter on the
   * book is visible while filtering rather than after scrolling to the bottom.
   */
  readonly summaryRow = computed<{
    months: SummaryCell[];
    total: SummaryCell;
    count: number;
  }>(() => {
    const rows = this.filteredRows();
    const metric = this.metric();
    const perStrategy = rows.map((r) => r.sources);

    return {
      months: this.monthLabels.map((_, i) => {
        const cells = perStrategy.map((sources) => sources[i]);
        return {
          text: formatMonthlyColumnTotal(cells, metric),
          // Same rule as every other total on screen, so a red month here means what it means
          // one row below: the win rate splits at 50%, and a drawdown is never good news.
          tone: this.totalTone(monthlyColumnTotal(cells, metric)),
        };
      }),
      total: {
        text: formatMonthlyColumnGrandTotal(perStrategy, metric),
        tone: this.totalTone(monthlyColumnGrandTotal(perStrategy, metric)),
      },
      count: rows.length,
    };
  });

  readonly sortKey = signal<MonthlySortKey | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  setMetric(metric: MonthlyMetric): void {
    if (this.metric() === metric) return;
    this.metric.set(metric);
    writeViewPreference(this.metricStorageKey, metric);
    // The active sort now ranks a different quantity, so re-apply its best-first direction.
    const key = this.sortKey();
    if (key !== null && !TEXT_SORT_KEYS.has(key)) {
      this.sortDir.set(isLowerBetter(metric) ? 'asc' : 'desc');
    }
  }

  /** Text columns start ascending; metric columns start best-first, which flips for drawdowns. */
  sortBy(key: MonthlySortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
      return;
    }
    this.sortKey.set(key);
    if (TEXT_SORT_KEYS.has(key)) this.sortDir.set('asc');
    else this.sortDir.set(isLowerBetter(this.metric()) ? 'asc' : 'desc');
  }

  readonly sortedRows = computed<MonthlyViewRow[]>(() => {
    const key = this.sortKey();
    if (key === null) return this.filteredRows();

    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const valueOf = (row: MonthlyViewRow): string | number | null => {
      if (key === 'name') return row.name;
      if (key === 'symbol') return row.symbol;
      if (key === 'timeframe') return row.timeframe;
      if (key === 'total') return row.total;
      return row.months[key];
    };

    return [...this.filteredRows()].sort((a, b) => {
      const va = valueOf(a);
      const vb = valueOf(b);
      // Rows without data always sink to the bottom, regardless of direction.
      if (va === null && vb === null) return 0;
      if (va === null) return 1;
      if (vb === null) return -1;
      if (typeof va === 'string') return va.localeCompare(vb as string) * dir;
      return (va - (vb as number)) * dir;
    });
  });

  sortIndicator(key: MonthlySortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '▲' : '▼';
  }

  prevYear(): void {
    if (this.canPrev()) this.selectedYear.update((y) => y - 1);
  }

  nextYear(): void {
    if (this.canNext()) this.selectedYear.update((y) => y + 1);
  }

  cellStyle(v: number | null): Record<string, string> {
    return monthlyMetricCellStyle(v, this.metric());
  }

  /**
   * Colour of the year column. Returns and win rate diverge around their neutral point;
   * any drawdown depth is bad news, so it never renders green.
   */
  totalTone(v: number | null): 'pos' | 'neg' | '' {
    if (v === null) return '';
    const metric = this.metric();
    if (metric === 'winRate') return v > 0.5 ? 'pos' : v < 0.5 ? 'neg' : '';
    if (isLowerBetter(metric)) return v > 0 ? 'neg' : '';
    return v > 0 ? 'pos' : v < 0 ? 'neg' : '';
  }

  symbolStyle(symbol: string | null): Record<string, string> {
    return {
      backgroundColor: symbolToColor(symbol) + '20',
      borderLeft: `3px solid ${symbolToColor(symbol)}`,
    };
  }
}
