import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MonthlyReturnDto } from '../../../core/services/portfolio.service';

interface HeatRow {
  year: number;
  months: (number | null)[]; // 12 entries, null = no data
  total: number;
}

@Component({
  selector: 'app-monthly-heatmap',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './monthly-heatmap.component.html',
  styleUrl: './monthly-heatmap.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonthlyHeatmapComponent {
  readonly returns = input<MonthlyReturnDto[]>([]);

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

  readonly rows = computed<HeatRow[]>(() => {
    const byYear = new Map<number, (number | null)[]>();
    for (const r of this.returns()) {
      if (!byYear.has(r.year)) byYear.set(r.year, Array(12).fill(null));
      byYear.get(r.year)![r.month - 1] = r.returnPercent;
    }
    return [...byYear.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([year, months]) => ({
        year,
        months,
        // Compounded yearly return from the monthly returns.
        total: months.reduce<number>((acc, m) => (m == null ? acc : (1 + acc) * (1 + m) - 1), 0),
      }));
  });

  /** Missing months show 0.00%. */
  fmt(v: number | null): string {
    return `${((v ?? 0) * 100).toFixed(2)}%`;
  }

  /** Green for gains, red for losses; neutral for zero/missing. Opacity scales with magnitude (capped at 10%). */
  cellStyle(v: number | null): Record<string, string> {
    const val = v ?? 0;
    if (val === 0) return { background: 'var(--bg-surface-2)' };
    const intensity = Math.min(Math.abs(val) / 0.1, 1) * 0.85 + 0.15;
    const color =
      val > 0
        ? `rgba(34,197,94,${intensity.toFixed(2)})`
        : `rgba(255,59,48,${intensity.toFixed(2)})`;
    return { background: color };
  }
}
