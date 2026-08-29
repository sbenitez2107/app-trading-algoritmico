import { PortfolioEquityPointDto } from '../../../core/services/portfolio.service';

/** The max-drawdown trough: when it happened and how deep (peak-relative). */
export interface MaxDrawdownAnnotation {
  /** Calendar day (yyyy-mm-dd) of the trough. */
  day: string;
  equity: number;
  /** Drop from the running peak, in currency (positive). */
  amount: number;
  /** Drop from the running peak, as a fraction (0..1). */
  percent: number;
}

/** The longest stretch the equity stayed below a prior peak. */
export interface StagnationAnnotation {
  /** Day the peak was set (start of the stagnation). */
  fromDay: string;
  /** Day the equity finally exceeded that peak — or the last day, if never recovered. */
  toDay: string;
  days: number;
}

export interface EquityAnnotations {
  maxDrawdown: MaxDrawdownAnnotation | null;
  stagnation: StagnationAnnotation | null;
}

const dayOf = (iso: string): string => iso.slice(0, 10);
const msBetween = (fromIso: string, toIso: string): number =>
  Date.parse(toIso) - Date.parse(fromIso);
const MS_PER_DAY = 86_400_000;

/**
 * Derives the max-drawdown trough and the longest-stagnation window from an equity curve.
 *
 * Mirrors the backend `AnalyticsSeries.ComputeEquityStats` so the chart annotations match the
 * analytics KPIs: max drawdown is the deepest peak-relative drop (the points already carry
 * per-point drawdown), and stagnation is the longest span between a peak and the next time
 * equity exceeds it (with a trailing span to the last point).
 */
export function computeEquityAnnotations(points: PortfolioEquityPointDto[]): EquityAnnotations {
  if (points.length === 0) return { maxDrawdown: null, stagnation: null };

  // --- Max drawdown: the point with the largest peak-relative drop. ---
  let trough = points[0];
  for (const p of points) {
    if (p.drawdownPercent > trough.drawdownPercent) trough = p;
  }
  const maxDrawdown: MaxDrawdownAnnotation | null =
    trough.drawdown > 0
      ? {
          day: dayOf(trough.date),
          equity: trough.equity,
          amount: trough.drawdown,
          percent: trough.drawdownPercent,
        }
      : null;

  // --- Max stagnation: longest span from a peak until equity exceeds it again. ---
  let peakEquity = points[0].equity;
  let peakIso = points[0].date;
  let bestMs = 0;
  let bestFromIso: string | null = null;
  let bestToIso: string | null = null;

  const consider = (fromIso: string, toIso: string): void => {
    const ms = msBetween(fromIso, toIso);
    if (ms > bestMs) {
      bestMs = ms;
      bestFromIso = fromIso;
      bestToIso = toIso;
    }
  };

  for (const p of points) {
    if (p.equity > peakEquity) {
      consider(peakIso, p.date); // recovered — close the stagnation that started at the old peak
      peakEquity = p.equity;
      peakIso = p.date;
    }
  }
  consider(peakIso, points[points.length - 1].date); // trailing: never recovered to a new high

  const stagnation: StagnationAnnotation | null =
    bestFromIso !== null && bestToIso !== null
      ? {
          fromDay: dayOf(bestFromIso),
          toDay: dayOf(bestToIso),
          days: Math.floor(bestMs / MS_PER_DAY),
        }
      : null;

  return { maxDrawdown, stagnation };
}
