import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PortfolioCorrelationDto } from '../../../core/services/portfolio.service';

interface Row {
  index: number;
  label: string;
  cells: number[];
}

/**
 * Correlation heatmap between member strategies' daily returns. High positive correlation (red) =
 * redundant; near zero (neutral) = diversified; negative (green) = hedging. The diagonal is 1 (self).
 */
@Component({
  selector: 'app-correlation-matrix',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './correlation-matrix.component.html',
  styleUrl: './correlation-matrix.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CorrelationMatrixComponent {
  readonly data = input<PortfolioCorrelationDto | null>(null);

  readonly rows = computed<Row[]>(() => {
    const d = this.data();
    if (!d || d.matrix.length === 0) return [];
    return d.labels.map((label, i) => ({ index: i + 1, label, cells: d.matrix[i] ?? [] }));
  });

  readonly indices = computed<number[]>(() => this.rows().map((r) => r.index));

  /** Diagonal (self) = neutral; positive → red intensity; negative → green intensity. */
  cellStyle(value: number, isDiagonal: boolean): Record<string, string> {
    if (isDiagonal) return { background: 'var(--bg-surface-2)', color: 'var(--text-muted)' };
    const mag = Math.min(Math.abs(value), 1) * 0.8;
    const color = value >= 0 ? `rgba(255,59,48,${mag.toFixed(2)})` : `rgba(34,197,94,${mag.toFixed(2)})`;
    return { background: color };
  }
}
