import { Component, ChangeDetectionStrategy, input, output, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  MonthlyReturnDto,
  PortfolioMonthlyReturnsDto,
} from '../../../core/services/portfolio.service';
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
  portfolioId: string;
  name: string;
  memberCount: number;
  /** 12 entries of the SELECTED metric, null = the month has no value to show. */
  months: (number | null)[];
  /** 12 entries of extra cell detail (win/loss counts), null = nothing to add. */
  tooltips: (string | null)[];
  total: number | null;
  hasData: boolean;
}

/** Sortable columns: text fields, the year total, or a month index (0-11). */
export type PortfolioMonthlySortKey = 'name' | 'memberCount' | 'total' | number;

/**
 * Portfolios × months matrix for one year, mirroring the per-strategy monthly view in the
 * broker-accounts area. The cell metric is selectable: compounding return, intra-month max
 * drawdown, underwater depth, or win rate — see `shared/utils/monthly-metric`.
 * Purely presentational: the parent owns the fetch so the same data also feeds the per-row
 * tooltip in the portfolios grid.
 */
@Component({
  selector: 'app-portfolio-monthly-returns',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './portfolio-monthly-returns.component.html',
  styleUrl: './portfolio-monthly-returns.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfolioMonthlyReturnsComponent {
  readonly rows = input<PortfolioMonthlyReturnsDto[]>([]);
  readonly isLoading = input(false);
  readonly error = input<string | null>(null);

  readonly portfolioSelected = output<string>();

  readonly selectedYear = signal(new Date().getFullYear());
  /**
   * Remembered per screen, so the portfolios matrix can sit on a different metric than the other
   * one — reading drawdowns here and returns there is a normal way to work.
   */
  private readonly metricStorageKey = 'monthly_metric_portfolios';
  readonly metric = signal<MonthlyMetric>(
    readViewPreference(this.metricStorageKey, MONTHLY_METRICS, 'return'),
  );

  readonly metricOptions: ReadonlyArray<{ value: MonthlyMetric; label: string; hint: string }> = [
    { value: 'return', label: 'Retorno', hint: 'Retorno compuesto del mes' },
    {
      value: 'maxDrawdown',
      label: 'Max DD',
      hint: 'Peor caída producida DENTRO del mes: el pico se reinicia el día 1, así que mide cuánto dolió ese mes',
    },
    {
      value: 'underwater',
      label: 'Bajo el agua',
      hint: 'Distancia máxima por debajo del pico histórico durante el mes: la misma caída se repite hasta hacer un nuevo máximo',
    },
    { value: 'winRate', label: 'W/L', hint: 'Ganadores / (ganadores + perdedores) del mes' },
  ];

  readonly title = computed(() => {
    switch (this.metric()) {
      case 'return':
        return 'Retorno mensual (compuesto)';
      case 'maxDrawdown':
        return 'Max DD del mes';
      case 'underwater':
        return 'Bajo el agua (pico histórico)';
      case 'winRate':
        return 'Win rate mensual';
    }
  });

  /** Header of the year column, which aggregates differently per metric. */
  readonly totalLabel = computed(() => {
    const metric = this.metric();
    if (isLowerBetter(metric)) return 'Peor';
    return metric === 'winRate' ? 'Global' : 'Total';
  });

  readonly monthLabels = [
    'Ene',
    'Feb',
    'Mar',
    'Abr',
    'May',
    'Jun',
    'Jul',
    'Ago',
    'Sep',
    'Oct',
    'Nov',
    'Dic',
  ];

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
        portfolioId: r.portfolioId,
        name: r.name,
        memberCount: r.memberCount,
        months: sources.map((m) => (m === null ? null : monthlyMetricValue(m, metric))),
        tooltips: sources.map((m) => monthlyMetricTooltip(m, metric)),
        total: monthlyMetricTotal(sources, metric),
        // Driven by the presence of months, NOT of values: a breakeven-only month reports no
        // win rate, but the portfolio still traded that year.
        hasData: sources.some((m) => m !== null),
      };
    });
  });

  readonly sortKey = signal<PortfolioMonthlySortKey | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  setMetric(metric: MonthlyMetric): void {
    if (this.metric() === metric) return;
    this.metric.set(metric);
    writeViewPreference(this.metricStorageKey, metric);
    // The active sort now ranks a different quantity, so re-apply its best-first direction.
    const key = this.sortKey();
    if (key !== null && key !== 'name' && key !== 'memberCount') {
      this.sortDir.set(isLowerBetter(metric) ? 'asc' : 'desc');
    }
  }

  /** Name starts ascending; metric columns start best-first, which flips for drawdowns. */
  sortBy(key: PortfolioMonthlySortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
      return;
    }
    this.sortKey.set(key);
    if (key === 'name') this.sortDir.set('asc');
    else if (key === 'memberCount') this.sortDir.set('desc');
    else this.sortDir.set(isLowerBetter(this.metric()) ? 'asc' : 'desc');
  }

  readonly sortedRows = computed<MonthlyViewRow[]>(() => {
    const key = this.sortKey();
    if (key === null) return this.viewRows();

    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const valueOf = (row: MonthlyViewRow): string | number | null => {
      if (key === 'name') return row.name;
      if (key === 'memberCount') return row.memberCount;
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

  sortIndicator(key: PortfolioMonthlySortKey): string {
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
}
