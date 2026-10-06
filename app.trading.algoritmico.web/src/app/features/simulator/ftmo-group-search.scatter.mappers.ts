import { FtmoGroupSearchRowDto } from '../../core/models/ftmo-group-search.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { breachShare, fractionToPercent, isWithinCeiling } from './ftmo-group-search.mappers';

/**
 * View-models of the scatter (slice 4a): one point per ranked row, x = median days to both targets and
 * y = breach share (percent, converted once), both of the WORSE kind. A row missing either value on either
 * kind is not plotted (counted, never placed at 0); a real 0 is plotted. The frontier is the Pareto-minimal
 * set: no other point has fewer days AND a lower breach share.
 */

/** Fixed drawing box, in SVG user units; the component scales it with `viewBox`. */
export const SCATTER_LAYOUT = {
  viewWidth: 640,
  viewHeight: 380,
  left: 56,
  top: 16,
  width: 560,
  height: 300,
} as const;

export interface NiceAxis {
  max: number;
  ticks: number[];
}

export interface ScatterTickVm {
  value: number;
  position: number;
}

export interface ScatterPointVm {
  row: FtmoGroupSearchRowDto;
  memberNames: string[];
  days: number;
  breachPercent: number;
  cx: number;
  cy: number;
  frontier: boolean;
  withinCeiling: boolean;
}

export interface ScatterVm {
  points: ScatterPointVm[];
  notPlotted: number;
  xAxis: NiceAxis;
  yAxis: NiceAxis;
  xTicks: ScatterTickVm[];
  yTicks: ScatterTickVm[];
}

const TARGET_INTERVALS = 5;
const REQUIRED_KINDS: readonly BacktestRunKind[] = [
  BacktestRunKind.Deploy,
  BacktestRunKind.Evaluation,
];

/** A 1/2/5 step axis from 0 up to a tick that covers `max`; `max <= 0` still yields a drawable axis. */
export function niceAxis(max: number): NiceAxis {
  if (!Number.isFinite(max) || max <= 0) return { max: 1, ticks: [0, 1] };
  const raw = max / TARGET_INTERVALS;
  const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
  const residual = raw / magnitude;
  const factor = residual <= 1.5 ? 1 : residual <= 3 ? 2 : residual <= 7 ? 5 : 10;
  const step = factor * magnitude;
  const count = Math.ceil(max / step - 1e-9);
  const ticks = Array.from({ length: count + 1 }, (_, i) => Number((i * step).toFixed(10)));
  return { max: ticks[ticks.length - 1], ticks };
}

/**
 * Whether each point is on the Pareto frontier (same order as the input). Sorted by (x, y), a point is kept
 * iff its y is strictly below the running minimum; an exact duplicate of the last kept point shares its status.
 */
export function frontierFlags(points: readonly { x: number; y: number }[]): boolean[] {
  const order = points
    .map((p, index) => ({ ...p, index }))
    .sort((a, b) => a.x - b.x || a.y - b.y || a.index - b.index);
  const flags = new Array<boolean>(points.length).fill(false);
  let minY = Number.POSITIVE_INFINITY;
  let last: { x: number; y: number } | null = null;
  for (const p of order) {
    if (p.y < minY) {
      flags[p.index] = true;
      minY = p.y;
      last = p;
    } else if (last !== null && p.x === last.x && p.y === last.y) {
      flags[p.index] = true;
    }
  }
  return flags;
}

interface Plottable {
  row: FtmoGroupSearchRowDto;
  days: number;
  breach: number;
}

/** The worse kind's days and breach share, or `null` when any kind lacks either value. */
function plottable(row: FtmoGroupSearchRowDto): Plottable | null {
  // Both kinds must be present: a row with a single kind has no "worse of the two" to plot.
  if (!REQUIRED_KINDS.every((k) => row.kinds.some((r) => r.kind === k))) return null;
  let days = Number.NEGATIVE_INFINITY;
  let breach = Number.NEGATIVE_INFINITY;
  for (const kind of row.kinds) {
    const median = kind.run?.summary?.daysToBothTargets.median ?? null;
    const share = breachShare(kind);
    if (median === null || share === null) return null;
    if (median > days) days = median;
    if (share > breach) breach = share;
  }
  return { row, days, breach };
}

function position(value: number, max: number, origin: number, length: number): number {
  return origin + (value / max) * length;
}

export function toScatter(
  rows: readonly FtmoGroupSearchRowDto[],
  ceilingFraction: number | null,
): ScatterVm {
  const plotted = rows.map(plottable).filter((p): p is Plottable => p !== null);
  const percents = plotted.map((p) => ({ x: p.days, y: fractionToPercent(p.breach) }));
  const flags = frontierFlags(percents);
  const xAxis = niceAxis(Math.max(0, ...percents.map((p) => p.x)));
  const yAxis = niceAxis(Math.max(0, ...percents.map((p) => p.y)));
  const { left, top, width, height } = SCATTER_LAYOUT;
  const baseline = top + height;
  return {
    notPlotted: rows.length - plotted.length,
    xAxis,
    yAxis,
    xTicks: xAxis.ticks.map((value) => ({
      value,
      position: position(value, xAxis.max, left, width),
    })),
    yTicks: yAxis.ticks.map((value) => ({
      value,
      position: baseline - position(value, yAxis.max, 0, height),
    })),
    points: plotted.map((p, i) => ({
      row: p.row,
      memberNames: [...p.row.memberNames],
      days: percents[i].x,
      breachPercent: percents[i].y,
      cx: position(percents[i].x, xAxis.max, left, width),
      cy: baseline - position(percents[i].y, yAxis.max, 0, height),
      frontier: flags[i],
      withinCeiling: ceilingFraction !== null && isWithinCeiling(p.row, ceilingFraction),
    })),
  };
}
