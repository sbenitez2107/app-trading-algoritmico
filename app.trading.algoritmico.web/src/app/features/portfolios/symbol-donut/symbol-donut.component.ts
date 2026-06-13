import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SymbolBreakdownDto } from '../../../core/services/portfolio.service';

/** Distinct categorical palette assigned by position so no two symbols share a colour. */
const PALETTE = [
  '#3b82f6', // blue
  '#f59e0b', // amber
  '#8b5cf6', // violet
  '#10b981', // emerald
  '#ec4899', // pink
  '#06b6d4', // cyan
  '#f97316', // orange
  '#a3e635', // lime
  '#6366f1', // indigo
  '#ef4444', // red
];

interface Slice {
  symbol: string;
  color: string;
  dash: string;
  offset: string;
  netProfit: number;
  returnPercent: number;
  tradeCount: number;
}

/**
 * Donut of profit composition by symbol. Slice size = |net profit| share (absolute P/L impact);
 * the legend shows each symbol's SIGNED return % (green/red) and trade count. Colors come from the
 * shared symbolToColor palette so they match the grids.
 */
@Component({
  selector: 'app-symbol-donut',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './symbol-donut.component.html',
  styleUrl: './symbol-donut.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SymbolDonutComponent {
  readonly data = input<SymbolBreakdownDto[]>([]);

  readonly slices = computed<Slice[]>(() => {
    const items = this.data();
    const total = items.reduce((s, i) => s + Math.abs(i.netProfit), 0);
    if (total <= 0) return [];

    let cumulative = 0;
    return items.map((i, idx) => {
      const pct = (Math.abs(i.netProfit) / total) * 100;
      const slice: Slice = {
        symbol: i.symbol,
        color: PALETTE[idx % PALETTE.length],
        dash: `${pct} ${100 - pct}`,
        offset: `${25 - cumulative}`,
        netProfit: i.netProfit,
        returnPercent: i.returnPercent,
        tradeCount: i.tradeCount,
      };
      cumulative += pct;
      return slice;
    });
  });

  pct(v: number): string {
    const sign = v > 0 ? '+' : '';
    return `${sign}${(v * 100).toFixed(2)}%`;
  }
}
