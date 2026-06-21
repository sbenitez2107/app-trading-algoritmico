import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  effect,
  input,
  viewChild,
  OnDestroy,
} from '@angular/core';
import {
  AreaSeries,
  createChart,
  createSeriesMarkers,
  IChartApi,
  ISeriesApi,
  ISeriesMarkersPluginApi,
  SeriesAttachedParameter,
  SeriesMarker,
  Time,
} from 'lightweight-charts';
import { PortfolioEquityPointDto } from '../../../core/services/portfolio.service';
import { formatCurrency } from '../../../shared/utils/format';
import { computeEquityAnnotations } from './equity-annotations';

/** Minimal shape of the canvas target we use — avoids a direct dependency on `fancy-canvas`. */
interface MediaRenderingScope {
  context: CanvasRenderingContext2D;
  mediaSize: { width: number; height: number };
}
interface RenderTarget {
  useMediaCoordinateSpace(callback: (scope: MediaRenderingScope) => void): void;
}

/**
 * Series primitive that shades a time range [from, to] as a full-height translucent band —
 * used to highlight the longest equity-stagnation window. Lightweight Charts v5 has no built-in
 * vertical band, so we draw one directly on the pane canvas.
 */
class StagnationBand {
  private chart?: SeriesAttachedParameter<Time>['chart'];
  private requestUpdate?: () => void;
  private from: Time | null = null;
  private to: Time | null = null;

  constructor(private readonly color: string) {}

  attached(param: SeriesAttachedParameter<Time>): void {
    this.chart = param.chart;
    this.requestUpdate = param.requestUpdate;
  }

  detached(): void {
    this.chart = undefined;
    this.requestUpdate = undefined;
  }

  /** Set (or clear, with nulls) the highlighted range and request a redraw. */
  set(from: Time | null, to: Time | null): void {
    this.from = from;
    this.to = to;
    this.requestUpdate?.();
  }

  updateAllViews(): void {}

  paneViews() {
    const scale = this.chart?.timeScale();
    const x1 = scale && this.from !== null ? scale.timeToCoordinate(this.from) : null;
    const x2 = scale && this.to !== null ? scale.timeToCoordinate(this.to) : null;
    const color = this.color;
    return [
      {
        renderer: () => ({
          draw: (target: RenderTarget): void => {
            if (x1 === null || x2 === null) return;
            target.useMediaCoordinateSpace((scope) => {
              const left = Math.min(x1, x2);
              const width = Math.abs(x2 - x1);
              scope.context.fillStyle = color;
              scope.context.fillRect(left, 0, width, scope.mediaSize.height);
            });
          },
        }),
      },
    ];
  }
}

/**
 * Equity curve rendered with Lightweight Charts (TradingView, MIT). Per-trade equity points are
 * aggregated to one end-of-day value (the library needs unique, ascending time values).
 * Annotates the max-drawdown trough (marker) and the longest stagnation window (shaded band).
 */
@Component({
  selector: 'app-equity-chart',
  standalone: true,
  template: '<div #container class="equity-chart__canvas" [style.height.px]="height()"></div>',
  styles: [':host { display: block; }', '.equity-chart__canvas { width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EquityChartComponent implements OnDestroy {
  readonly points = input<PortfolioEquityPointDto[]>([]);
  /** Canvas height in px. Defaults to 640 (portfolio overview); panels can pass a smaller value. */
  readonly height = input<number>(640);
  private readonly container = viewChild<ElementRef<HTMLDivElement>>('container');

  private chart?: IChartApi;
  private series?: ISeriesApi<'Area'>;
  private markers?: ISeriesMarkersPluginApi<Time>;
  private band?: StagnationBand;

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
      this.applyAnnotations(pts);
      this.chart.timeScale().fitContent();
    });
  }

  /** Place the max-DD marker + stagnation band/label from the equity points. */
  private applyAnnotations(pts: PortfolioEquityPointDto[]): void {
    const ann = computeEquityAnnotations(pts);

    const markers: SeriesMarker<Time>[] = [];
    if (ann.maxDrawdown) {
      const loss = `-${(ann.maxDrawdown.percent * 100).toFixed(1)}%`;
      markers.push({
        time: ann.maxDrawdown.day as Time,
        position: 'belowBar',
        color: '#ef4444',
        shape: 'arrowUp',
        text: `Max DD ${loss} (${formatCurrency(-ann.maxDrawdown.amount)})`,
      });
    }
    if (ann.stagnation) {
      markers.push({
        time: ann.stagnation.fromDay as Time,
        position: 'aboveBar',
        color: '#f59e0b',
        shape: 'circle',
        text: `Stagnation ${ann.stagnation.days}d`,
      });
    }
    // Lightweight Charts requires markers in ascending time order.
    markers.sort((a, b) => String(a.time).localeCompare(String(b.time)));

    if (!this.markers) this.markers = createSeriesMarkers(this.series!, markers);
    else this.markers.setMarkers(markers);

    if (!this.band) {
      this.band = new StagnationBand('rgba(245, 158, 11, 0.14)');
      this.series!.attachPrimitive(this.band);
    }
    this.band.set(
      ann.stagnation ? (ann.stagnation.fromDay as Time) : null,
      ann.stagnation ? (ann.stagnation.toDay as Time) : null,
    );
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
