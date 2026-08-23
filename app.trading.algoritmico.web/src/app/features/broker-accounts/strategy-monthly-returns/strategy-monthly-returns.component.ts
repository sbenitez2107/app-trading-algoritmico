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
import {
  StrategyService,
  StrategyMonthlyReturnsDto,
  MonthlyReturnDto,
} from '../../../core/services/strategy.service';
import { symbolToColor } from '../../../shared/utils/symbol-color';
import {
  MONTHLY_METRICS,
  MonthlyMetric,
  formatMonthlyMetric,
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
  /** 12 entries of the SELECTED metric, null = the month has no value to show. */
  months: (number | null)[];
  /** 12 entries of extra cell detail (win/loss counts), null = nothing to add. */
  tooltips: (string | null)[];
  total: number | null;
  hasData: boolean;
}

/** Sortable columns: text fields, the year total, or a month index (0-11). */
export type MonthlySortKey = 'name' | 'symbol' | 'total' | number;

@Component({
  selector: 'app-strategy-monthly-returns',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './strategy-monthly-returns.component.html',
  styleUrl: './strategy-monthly-returns.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyMonthlyReturnsComponent {
  readonly accountId = input.required<string>();

  private readonly strategyService = inject(StrategyService);

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

      return {
        strategyId: r.strategyId,
        name: r.name,
        symbol: r.symbol,
        months: sources.map((m) => (m === null ? null : monthlyMetricValue(m, metric))),
        tooltips: sources.map((m) => monthlyMetricTooltip(m, metric)),
        total: monthlyMetricTotal(sources, metric),
        // Driven by the presence of months, NOT of values: a breakeven-only month reports no
        // win rate, but the strategy still traded that year.
        hasData: sources.some((m) => m !== null),
      };
    });
  });

  readonly sortKey = signal<MonthlySortKey | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  setMetric(metric: MonthlyMetric): void {
    if (this.metric() === metric) return;
    this.metric.set(metric);
    writeViewPreference(this.metricStorageKey, metric);
    // The active sort now ranks a different quantity, so re-apply its best-first direction.
    const key = this.sortKey();
    if (key !== null && key !== 'name' && key !== 'symbol') {
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
    if (key === 'name' || key === 'symbol') this.sortDir.set('asc');
    else this.sortDir.set(isLowerBetter(this.metric()) ? 'asc' : 'desc');
  }

  readonly sortedRows = computed<MonthlyViewRow[]>(() => {
    const key = this.sortKey();
    if (key === null) return this.viewRows();

    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const valueOf = (row: MonthlyViewRow): string | number | null => {
      if (key === 'name') return row.name;
      if (key === 'symbol') return row.symbol;
      if (key === 'total') return row.total;
      return row.months[key];
    };

    return [...this.viewRows()].sort((a, b) => {
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

  /** Months without a value render as an em-dash. */
  fmt(v: number | null): string {
    return formatMonthlyMetric(v);
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
