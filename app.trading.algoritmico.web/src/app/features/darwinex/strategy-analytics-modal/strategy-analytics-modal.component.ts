import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  output,
  signal,
  computed,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import {
  StrategyService,
  StrategyAnalyticsDto,
  MonthlyReturnDto,
} from '../../../core/services/strategy.service';
import { formatCurrency } from '../../../shared/utils/format';
import { KpiHelpComponent } from './kpi-help.component';
import { KPI_INFO } from './kpi-info';

interface MonthlyHeatmapCell {
  year: number;
  month: number;
  returnPercent: number;
  profit: number;
  empty: boolean;
}

interface MonthlyHeatmapRow {
  year: number;
  cells: MonthlyHeatmapCell[];
  totalReturn: number;
}

@Component({
  selector: 'app-strategy-analytics-modal',
  standalone: true,
  imports: [CommonModule, KpiHelpComponent],
  templateUrl: './strategy-analytics-modal.component.html',
  styleUrl: './strategy-analytics-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyAnalyticsModalComponent {
  readonly strategyId = input.required<string>();
  readonly strategyName = input.required<string>();
  readonly closed = output<void>();

  readonly kpiInfo = KPI_INFO;

  private readonly strategyService = inject(StrategyService);

  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly analytics = signal<StrategyAnalyticsDto | null>(null);
  readonly monthly = signal<MonthlyReturnDto[]>([]);

  /** Months × Year matrix used by the heatmap. */
  readonly heatmap = computed<MonthlyHeatmapRow[]>(() => {
    const months = this.monthly();
    if (months.length === 0) return [];

    // Build a lookup map and the full year range from the data.
    const byKey = new Map<string, MonthlyReturnDto>();
    let minYear = Number.POSITIVE_INFINITY;
    let maxYear = Number.NEGATIVE_INFINITY;
    for (const m of months) {
      byKey.set(`${m.year}-${m.month}`, m);
      if (m.year < minYear) minYear = m.year;
      if (m.year > maxYear) maxYear = m.year;
    }

    const rows: MonthlyHeatmapRow[] = [];
    for (let y = minYear; y <= maxYear; y++) {
      const cells: MonthlyHeatmapCell[] = [];
      let yearProfit = 0;
      let yearStart: number | null = null;
      for (let m = 1; m <= 12; m++) {
        const found = byKey.get(`${y}-${m}`);
        if (found) {
          cells.push({
            year: y,
            month: m,
            returnPercent: found.returnPercent,
            profit: found.profit,
            empty: false,
          });
          yearProfit += found.profit;
          if (yearStart === null) yearStart = found.equityStart;
        } else {
          cells.push({ year: y, month: m, returnPercent: 0, profit: 0, empty: true });
        }
      }
      const totalReturn = yearStart && yearStart > 0 ? yearProfit / yearStart : 0;
      rows.push({ year: y, cells, totalReturn });
    }
    return rows;
  });

  /** Used to scale color intensity in the heatmap relative to the strategy's own range. */
  readonly maxAbsMonthly = computed(() => {
    const months = this.monthly();
    if (months.length === 0) return 0;
    return Math.max(...months.map((m) => Math.abs(m.returnPercent)));
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
      this.strategyId(); // re-runs when the input changes
      this.load();
    });
  }

  cellStyle(cell: MonthlyHeatmapCell): Record<string, string> {
    if (cell.empty) return { backgroundColor: 'transparent' };
    const max = this.maxAbsMonthly();
    if (max === 0) return { backgroundColor: 'transparent' };
    const intensity = Math.min(1, Math.abs(cell.returnPercent) / max);
    // Subtle floor of 0.08 so even tiny returns are visible; cap at 0.55 to keep readable.
    const alpha = 0.08 + intensity * 0.47;
    const rgb = cell.returnPercent >= 0 ? '34, 197, 94' : '239, 68, 68';
    return { backgroundColor: `rgba(${rgb}, ${alpha})` };
  }

  formatPercent(value: number, fractionDigits = 2): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '—';
    return `${(value * 100).toFixed(fractionDigits)}%`;
  }

  formatNumber(value: number | null | undefined, fractionDigits = 2): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '—';
    return value.toFixed(fractionDigits);
  }

  formatMoney(value: number | null | undefined): string {
    return formatCurrency(value ?? null);
  }

  formatInt(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return value.toString();
  }

  onClose(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('analytics-backdrop')) {
      this.onClose();
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    const id = this.strategyId();

    forkJoin({
      analytics: this.strategyService.getAnalyticsByStrategy(id),
      monthly: this.strategyService.getMonthlyReturnsByStrategy(id),
    }).subscribe({
      next: (res) => {
        this.analytics.set(res.analytics);
        this.monthly.set(res.monthly);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to load analytics.');
        this.isLoading.set(false);
      },
    });
  }
}
