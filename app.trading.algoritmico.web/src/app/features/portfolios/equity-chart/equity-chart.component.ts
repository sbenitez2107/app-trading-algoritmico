import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  effect,
  input,
  signal,
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
  LineSeries,
  MouseEventParams,
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

/** One extra line drawn over the main curve — a portfolio member's contribution series. */
export interface EquityOverlay {
  id: string;
  label: string;
  color: string;
  /** ISO date + the value at that point. Values live on their OWN price scale (see below). */
  points: { date: string; value: number }[];
  /**
   * Ghost lines are drawn faint and thin with no legend identity. They exist to show the SHAPE of
   * the fan — which members break away, which sit flat — at any member count, because they carry
   * no colour to run out of. Hovering one still names it.
   */
  ghost?: boolean;
}

/** Ghost lines share one muted colour; identity comes from hovering, not from the palette. */
const GHOST_COLOR = 'rgba(128,128,128,0.45)';

/**
 * Overlay colours. Kept small on purpose: past roughly eight lines the hues stop being
 * distinguishable and the chart turns into spaghetti, so the caller caps the selection instead of
 * generating more colours.
 */
export const EQUITY_OVERLAY_PALETTE = [
  '#f59e0b',
  '#10b981',
  '#8b5cf6',
  '#ec4899',
  '#06b6d4',
  '#ef4444',
  '#84cc16',
  '#a855f7',
];

/**
 * Equity curve rendered with Lightweight Charts (TradingView, MIT). Per-trade equity points are
 * aggregated to one end-of-day value (the library needs unique, ascending time values).
 * Annotates the max-drawdown trough (marker) and the longest stagnation window (shaded band).
 */
@Component({
  selector: 'app-equity-chart',
  standalone: true,
  template: `
    <div class="equity-chart">
      <div #container class="equity-chart__canvas" [style.height.px]="height()"></div>
      @if (hovered(); as tip) {
        <div
          class="equity-chart__tip"
          [style.left.px]="tip.x"
          [style.top.px]="tip.y"
          role="tooltip"
        >
          <span class="equity-chart__tip-dot" [style.background]="tip.color"></span>
          <span class="equity-chart__tip-name">{{ tip.label }}</span>
          <span class="equity-chart__tip-value">{{ tip.value }}</span>
        </div>
      }
    </div>
  `,
  styles: [
    ':host { display: block; }',
    '.equity-chart { position: relative; }',
    '.equity-chart__canvas { width: 100%; }',
    `
      .equity-chart__tip {
        position: absolute;
        z-index: 3;
        display: flex;
        align-items: center;
        gap: 6px;
        transform: translate(12px, -50%);
        padding: 4px 8px;
        border-radius: 4px;
        background: var(--bg-surface, #1e1e2e);
        border: 1px solid var(--color-border, #313244);
        color: var(--text-main, #cdd6f4);
        font-size: 0.72rem;
        white-space: nowrap;
        pointer-events: none;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);
      }
    `,
    '.equity-chart__tip-dot { width: 8px; height: 8px; border-radius: 2px; flex: none; }',
    '.equity-chart__tip-value { font-variant-numeric: tabular-nums; opacity: 0.75; }',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EquityChartComponent implements OnDestroy {
  readonly points = input<PortfolioEquityPointDto[]>([]);
  /** Canvas height in px. Defaults to 640 (portfolio overview); panels can pass a smaller value. */
  readonly height = input<number>(640);
  private readonly container = viewChild<ElementRef<HTMLDivElement>>('container');

  /**
   * Extra lines drawn alongside the main curve. They render on the LEFT price scale, which
   * autoscales independently: a member's contribution swings by hundreds while the combined
   * equity sits near six figures, so sharing one axis would flatten every overlay into a
   * straight line.
   */
  readonly overlays = input<EquityOverlay[]>([]);

  private chart?: IChartApi;
  private series?: ISeriesApi<'Area'>;
  private markers?: ISeriesMarkersPluginApi<Time>;
  private band?: StagnationBand;
  private readonly overlaySeries = new Map<string, ISeriesApi<'Line'>>();
  /** Reverse of `overlaySeries`: the crosshair hands back a series, we need its overlay. */
  private readonly overlayBySeries = new Map<ISeriesApi<'Line'>, EquityOverlay>();
  /** Whether each drawn series is currently a ghost, so a flag flip can force a re-create. */
  private readonly overlayIsGhost = new Map<string, boolean>();

  readonly hovered = signal<{
    label: string;
    value: string;
    color: string;
    x: number;
    y: number;
  } | null>(null);

  constructor() {
    effect(() => {
      const el = this.container()?.nativeElement;
      const pts = this.points();
      const overlays = this.overlays();
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
          leftPriceScale: { borderVisible: false, visible: false },
          timeScale: { borderVisible: false },
        });
        this.series = this.chart.addSeries(AreaSeries, {
          lineColor: '#3b82f6',
          topColor: 'rgba(59,130,246,0.35)',
          bottomColor: 'rgba(59,130,246,0.02)',
          lineWidth: 2,
        });
        this.chart.subscribeCrosshairMove((param) => this.onCrosshair(param));
      }

      this.series!.setData(this.toDailyData(pts));
      this.applyAnnotations(pts);
      this.syncOverlays(overlays);
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

  private hoveredId: string | null = null;

  /**
   * Names the line under the cursor. This is what makes the ghost fan usable: the lines carry no
   * colour identity, so hovering is the only way to ask "which strategy is that one?".
   * `hoveredSeriesOnTop` (on by default) already lifts the hovered line above the rest.
   */
  private onCrosshair(param: MouseEventParams): void {
    const series = param.hoveredInfo?.series;
    const overlay = series ? this.overlayBySeries.get(series as ISeriesApi<'Line'>) : undefined;

    if (!overlay || !param.point) {
      this.hoveredId = null;
      this.hovered.set(null);
      return;
    }

    const point = param.seriesData.get(series!) as { value?: number } | undefined;
    this.hoveredId = overlay.id;
    this.hovered.set({
      label: overlay.label,
      value: point?.value === undefined ? '' : formatCurrency(point.value),
      // Ghosts keep their assigned colour in the tooltip dot even though the line is grey.
      color: overlay.ghost ? GHOST_COLOR : overlay.color,
      x: param.point.x,
      y: param.point.y,
    });
  }

  /**
   * Reconcile the drawn overlay lines with the requested ones: drop what is gone, create what is
   * new, refresh the rest. Lightweight Charts has no declarative series list, so the diff is ours
   * to keep — rebuilding every series on each change would reset the chart's zoom.
   */
  private syncOverlays(overlays: EquityOverlay[]): void {
    const wanted = new Set(overlays.map((o) => o.id));

    for (const [id, series] of this.overlaySeries) {
      if (wanted.has(id)) continue;
      this.chart!.removeSeries(series);
      this.overlaySeries.delete(id);
      this.overlayBySeries.delete(series);
      this.overlayIsGhost.delete(id);
    }
    // A line that stops being drawn must not leave its tooltip stranded on screen.
    if (this.hoveredId !== null && !wanted.has(this.hoveredId)) {
      this.hoveredId = null;
      this.hovered.set(null);
    }

    for (const overlay of overlays) {
      const ghost = overlay.ghost === true;
      const color = ghost ? GHOST_COLOR : overlay.color;
      let series = this.overlaySeries.get(overlay.id);

      // A series that changed sides is re-created rather than restyled: Lightweight Charts draws in
      // creation order, so promoting a ghost to a coloured line must also lift it above the fan.
      if (series && this.overlayIsGhost.get(overlay.id) !== ghost) {
        this.chart!.removeSeries(series);
        this.overlayBySeries.delete(series);
        this.overlaySeries.delete(overlay.id);
        series = undefined;
      }

      if (!series) {
        series = this.chart!.addSeries(LineSeries, {
          color,
          lineWidth: ghost ? 1 : 2,
          priceScaleId: 'left',
          priceLineVisible: false,
          lastValueVisible: false,
          crosshairMarkerVisible: !ghost,
        });
        this.overlaySeries.set(overlay.id, series);
      } else {
        series.applyOptions({ color });
      }

      this.overlayIsGhost.set(overlay.id, ghost);
      this.overlayBySeries.set(series, overlay);
      series.setData(this.toDailySeries(overlay.points));
    }

    // The left axis only earns its width while something is drawn on it.
    this.chart!.priceScale('left').applyOptions({ visible: overlays.length > 0 });
  }

  /** Aggregate per-trade points to the last equity of each calendar day. */
  private toDailyData(pts: PortfolioEquityPointDto[]): { time: Time; value: number }[] {
    return this.toDailySeries(pts.map((p) => ({ date: p.date, value: p.equity })));
  }

  /** Same end-of-day collapse for any dated series — the library needs unique ascending times. */
  private toDailySeries(pts: { date: string; value: number }[]): { time: Time; value: number }[] {
    const byDay = new Map<string, number>();
    for (const p of pts) {
      const day = p.date.slice(0, 10); // yyyy-mm-dd
      byDay.set(day, p.value); // later trades on the same day overwrite → end-of-day value
    }
    return [...byDay.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([day, value]) => ({ time: day as Time, value }));
  }

  ngOnDestroy(): void {
    this.chart?.remove();
  }
}
