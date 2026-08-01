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
} from '../../../core/services/strategy.service';
import { symbolToColor } from '../../../shared/utils/symbol-color';

interface MonthlyViewRow {
  strategyId: string;
  name: string;
  symbol: string | null;
  months: (number | null)[]; // 12 entries, null = no data for that month
  total: number;
  hasData: boolean;
}

/** Sortable columns: text fields, the compounded total, or a month index (0-11). */
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
    return this.rows().map((r) => {
      const months: (number | null)[] = Array(12).fill(null);
      for (const m of r.returns) {
        if (m.year === year) months[m.month - 1] = m.returnPercent;
      }
      return {
        strategyId: r.strategyId,
        name: r.name,
        symbol: r.symbol,
        months,
        // Compounded return across the year's months with data.
        total: months.reduce<number>((acc, m) => (m == null ? acc : (1 + acc) * (1 + m) - 1), 0),
        hasData: months.some((m) => m !== null),
      };
    });
  });

  readonly sortKey = signal<MonthlySortKey | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  /** New text column starts ascending; numeric columns start descending (best first). */
  sortBy(key: MonthlySortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
      return;
    }
    this.sortKey.set(key);
    this.sortDir.set(key === 'name' || key === 'symbol' ? 'asc' : 'desc');
  }

  readonly sortedRows = computed<MonthlyViewRow[]>(() => {
    const key = this.sortKey();
    if (key === null) return this.viewRows();

    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const valueOf = (row: MonthlyViewRow): string | number | null => {
      if (key === 'name') return row.name;
      if (key === 'symbol') return row.symbol;
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

  symbolStyle(symbol: string | null): Record<string, string> {
    return {
      backgroundColor: symbolToColor(symbol) + '20',
      borderLeft: `3px solid ${symbolToColor(symbol)}`,
    };
  }
}
