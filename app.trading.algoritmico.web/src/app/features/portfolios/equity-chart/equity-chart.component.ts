import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  effect,
  input,
  viewChild,
  OnDestroy,
} from '@angular/core';
import { AreaSeries, createChart, IChartApi, ISeriesApi, Time } from 'lightweight-charts';
import { PortfolioEquityPointDto } from '../../../core/services/portfolio.service';

/**
 * Combined portfolio equity curve rendered with Lightweight Charts (TradingView, MIT).
 * The per-trade equity points are aggregated to one end-of-day value (Lightweight Charts
 * needs unique, ascending time values).
 */
@Component({
  selector: 'app-equity-chart',
  standalone: true,
  template: '<div #container class="equity-chart__canvas"></div>',
  styles: [':host { display: block; }', '.equity-chart__canvas { width: 100%; height: 640px; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EquityChartComponent implements OnDestroy {
  readonly points = input<PortfolioEquityPointDto[]>([]);
  private readonly container = viewChild<ElementRef<HTMLDivElement>>('container');

  private chart?: IChartApi;
  private series?: ISeriesApi<'Area'>;

  constructor() {
    effect(() => {
      const el = this.container()?.nativeElement;
      const pts = this.points();
      if (!el) return;

      if (!this.chart) {
        this.chart = createChart(el, {
          autoSize: true,
          layout: {
            background: { color: 'transparent' },
            textColor: '#6b7280',
            attributionLogo: false,
          },
          grid: {
            vertLines: { color: 'rgba(128,128,128,0.12)' },
            horzLines: { color: 'rgba(128,128,128,0.12)' },
          },
          rightPriceScale: { borderVisible: false },
          timeScale: { borderVisible: false },
        });
        this.series = this.chart.addSeries(AreaSeries, {
          lineColor: '#3b82f6',
          topColor: 'rgba(59,130,246,0.35)',
          bottomColor: 'rgba(59,130,246,0.02)',
          lineWidth: 2,
        });
      }

      this.series!.setData(this.toDailyData(pts));
      this.chart.timeScale().fitContent();
    });
  }

  /** Aggregate per-trade points to the last equity of each calendar day. */
  private toDailyData(pts: PortfolioEquityPointDto[]): { time: Time; value: number }[] {
    const byDay = new Map<string, number>();
    for (const p of pts) {
      const day = p.date.slice(0, 10); // yyyy-mm-dd
      byDay.set(day, p.equity); // later trades on the same day overwrite → end-of-day equity
    }
    return [...byDay.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([day, value]) => ({ time: day as Time, value }));
  }

  ngOnDestroy(): void {
    this.chart?.remove();
  }
}
