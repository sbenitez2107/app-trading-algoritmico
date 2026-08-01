import { Component, ChangeDetectionStrategy, input, output, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PortfolioMonthlyReturnsDto } from '../../../core/services/portfolio.service';

interface MonthlyViewRow {
  portfolioId: string;
  name: string;
  memberCount: number;
  months: (number | null)[]; // 12 entries, null = no data for that month
  total: number;
  hasData: boolean;
}

/** Sortable columns: text fields, the compounded total, or a month index (0-11). */
export type PortfolioMonthlySortKey = 'name' | 'memberCount' | 'total' | number;

/**
 * Portfolios × months matrix of compounding returns for one year, mirroring the
 * per-strategy monthly returns view in the broker-accounts area.
 * Purely presentational: the parent owns the fetch so the same data also feeds
 * the per-row tooltip in the portfolios grid.
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
    return this.rows().map((r) => {
      const months: (number | null)[] = Array(12).fill(null);
      for (const m of r.returns) {
        if (m.year === year) months[m.month - 1] = m.returnPercent;
      }
      return {
        portfolioId: r.portfolioId,
        name: r.name,
        memberCount: r.memberCount,
        months,
        // Compounded return across the year's months with data.
        total: months.reduce<number>((acc, m) => (m == null ? acc : (1 + acc) * (1 + m) - 1), 0),
        hasData: months.some((m) => m !== null),
      };
    });
  });

  readonly sortKey = signal<PortfolioMonthlySortKey | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  /** Name starts ascending; numeric columns start descending (best first). */
  sortBy(key: PortfolioMonthlySortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
      return;
    }
    this.sortKey.set(key);
    this.sortDir.set(key === 'name' ? 'asc' : 'desc');
  }

  readonly sortedRows = computed<MonthlyViewRow[]>(() => {
    const key = this.sortKey();
    if (key === null) return this.viewRows();

    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const valueOf = (row: MonthlyViewRow): string | number | null => {
      if (key === 'name') return row.name;
      if (key === 'memberCount') return row.memberCount;
      if (key === 'total') return row.hasData ? row.total : null;
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

  /** Months without data render as an em-dash; totals of empty rows too. */
  fmt(v: number | null): string {
    if (v === null) return '—';
    return `${(v * 100).toFixed(2)}%`;
  }

  /** Green for gains, red for losses; neutral for zero/missing. Opacity scales with magnitude (capped at 10%). */
  cellStyle(v: number | null): Record<string, string> {
    if (v === null || v === 0) return { background: 'var(--bg-surface-2)' };
    const intensity = Math.min(Math.abs(v) / 0.1, 1) * 0.85 + 0.15;
    const color =
      v > 0 ? `rgba(34,197,94,${intensity.toFixed(2)})` : `rgba(255,59,48,${intensity.toFixed(2)})`;
    return { background: color };
  }
}
