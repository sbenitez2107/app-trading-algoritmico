/**
 * Metric rendered in each cell of a monthly performance matrix.
 *
 * `maxDrawdown` and `underwater` deliberately answer DIFFERENT questions and are both offered:
 * `maxDrawdown` resets its peak every month ("how much did THIS month hurt"), while `underwater`
 * carries the all-time peak, so one bad month keeps showing up until a new high is made.
 */
export const MONTHLY_METRICS = ['return', 'maxDrawdown', 'underwater', 'winRate'] as const;

/** Derived from MONTHLY_METRICS so the runtime allow-list and the type cannot drift apart. */
export type MonthlyMetric = (typeof MONTHLY_METRICS)[number];

/** Structural shape shared by the portfolio and strategy monthly DTOs. */
export interface MonthlyMetricSource {
  returnPercent: number;
  maxDrawdownPercent: number;
  underwaterPercent: number;
  winCount: number;
  lossCount: number;
}

/** Drawdown metrics rank best-first ascending; the others rank best-first descending. */
export function isLowerBetter(metric: MonthlyMetric): boolean {
  return metric === 'maxDrawdown' || metric === 'underwater';
}

/** The month's value for one metric, or null when the month cannot report it. */
export function monthlyMetricValue(m: MonthlyMetricSource, metric: MonthlyMetric): number | null {
  switch (metric) {
    case 'return':
      return m.returnPercent;
    case 'maxDrawdown':
      return m.maxDrawdownPercent;
    case 'underwater':
      return m.underwaterPercent;
    case 'winRate': {
      const decided = m.winCount + m.lossCount;
      // A month of nothing but breakeven trades has no win rate to report.
      return decided === 0 ? null : m.winCount / decided;
    }
  }
}

/**
 * Year-level aggregate for one metric across the 12 month slots (null where the month has no data).
 * Each metric aggregates the only way that is meaningful for it: returns compound, drawdowns take
 * the worst month, and the win rate is recomputed from the summed counts — NOT averaged across
 * months, which would weigh a 2-trade month like a 200-trade one.
 */
export function monthlyMetricTotal(
  months: readonly (MonthlyMetricSource | null)[],
  metric: MonthlyMetric,
): number | null {
  const present = months.filter((m): m is MonthlyMetricSource => m !== null);
  if (present.length === 0) return null;

  if (metric === 'winRate') {
    const wins = present.reduce((acc, m) => acc + m.winCount, 0);
    const decided = present.reduce((acc, m) => acc + m.winCount + m.lossCount, 0);
    return decided === 0 ? null : wins / decided;
  }

  if (metric === 'return') {
    return present.reduce((acc, m) => (1 + acc) * (1 + m.returnPercent) - 1, 0);
  }

  return present.reduce((acc, m) => Math.max(acc, monthlyMetricValue(m, metric) ?? 0), 0);
}

/** Formats any of the metrics — all four are percentages. */
export function formatMonthlyMetric(v: number | null): string {
  if (v === null) return '—';
  return `${(v * 100).toFixed(2)}%`;
}

/** Wins and losses backing a win-rate figure, summed across the given months. */
function winLossCounts(months: readonly (MonthlyMetricSource | null)[]): {
  wins: number;
  losses: number;
} {
  return months.reduce(
    (acc, m) =>
      m === null ? acc : { wins: acc.wins + m.winCount, losses: acc.losses + m.lossCount },
    { wins: 0, losses: 0 },
  );
}

/**
 * Win rate rendered as the raw counts plus the rounded percentage, e.g. `3/1 (75%)`.
 * The counts carry the confidence the percentage hides: 3/1 and 30/10 are both 75%, and only
 * one of them means anything. They also make the decimals redundant, so the percentage rounds.
 */
function formatWinRate(wins: number, losses: number, value: number): string {
  return `${wins}/${losses} (${Math.round(value * 100)}%)`;
}

/** Cell text for one month: the win rate leads with its counts, every other metric is a percentage. */
export function formatMonthlyCell(m: MonthlyMetricSource | null, metric: MonthlyMetric): string {
  const value = m === null ? null : monthlyMetricValue(m, metric);
  if (m === null || value === null || metric !== 'winRate') return formatMonthlyMetric(value);
  return formatWinRate(m.winCount, m.lossCount, value);
}

/** Cell text for the year column, matching `formatMonthlyCell` on the win rate. */
export function formatMonthlyTotalCell(
  months: (MonthlyMetricSource | null)[],
  metric: MonthlyMetric,
): string {
  const total = monthlyMetricTotal(months, metric);
  if (total === null || metric !== 'winRate') return formatMonthlyMetric(total);
  const { wins, losses } = winLossCounts(months);
  return formatWinRate(wins, losses, total);
}

/**
 * One column of the matrix collapsed across MANY strategies - the summary row above the grid.
 *
 * Returns and drawdowns SUM rather than average, and that is not the usual mistake of adding
 * percentages: every strategy in an account measures its month against the SAME account balance
 * (see `AnalyticsSeries.BuildMonthlyReturns`), so the column sum is the account's own figure.
 * The drawdown columns sum on the same base but read as an upper bound, since they assume every
 * strategy bottomed out in the same month.
 *
 * The win rate is the exception: percentages there are not additive at all, so its counts pool -
 * the same rule each row's year column already uses.
 */
export function monthlyColumnTotal(
  cells: readonly (MonthlyMetricSource | null)[],
  metric: MonthlyMetric,
): number | null {
  const present = cells.filter((m): m is MonthlyMetricSource => m !== null);
  if (present.length === 0) return null;

  if (metric === 'winRate') {
    const { wins, losses } = winLossCounts(present);
    const decided = wins + losses;
    return decided === 0 ? null : wins / decided;
  }

  return present.reduce((acc, m) => acc + (monthlyMetricValue(m, metric) ?? 0), 0);
}

/** Rendered form of {@link monthlyColumnTotal}; the win rate leads with its pooled counts. */
export function formatMonthlyColumnTotal(
  cells: readonly (MonthlyMetricSource | null)[],
  metric: MonthlyMetric,
): string {
  const value = monthlyColumnTotal(cells, metric);
  if (value === null || metric !== 'winRate') return formatMonthlyMetric(value);
  const { wins, losses } = winLossCounts(cells);
  return formatWinRate(wins, losses, value);
}

/**
 * Year column of the summary row. Each strategy's own year total is computed first - so returns
 * still compound WITHIN a strategy - and only then summed across strategies. The win rate pools
 * every count instead, for the same reason as above.
 */
export function monthlyColumnGrandTotal(
  perStrategy: readonly (MonthlyMetricSource | null)[][],
  metric: MonthlyMetric,
): number | null {
  if (metric === 'winRate') return monthlyColumnTotal(perStrategy.flat(), 'winRate');

  const totals = perStrategy
    .map((months) => monthlyMetricTotal(months, metric))
    .filter((t): t is number => t !== null);

  return totals.length === 0 ? null : totals.reduce((acc, t) => acc + t, 0);
}

/** Rendered form of {@link monthlyColumnGrandTotal}. */
export function formatMonthlyColumnGrandTotal(
  perStrategy: readonly (MonthlyMetricSource | null)[][],
  metric: MonthlyMetric,
): string {
  if (metric === 'winRate') return formatMonthlyColumnTotal(perStrategy.flat(), 'winRate');
  return formatMonthlyMetric(monthlyColumnGrandTotal(perStrategy, metric));
}

/** Extra detail for the cell's native tooltip; win rate carries the raw counts. */
export function monthlyMetricTooltip(
  m: MonthlyMetricSource | null,
  metric: MonthlyMetric,
): string | null {
  if (m === null) return null;
  if (metric === 'winRate') return `${m.winCount} W / ${m.lossCount} L`;
  return null;
}

/**
 * Heatmap background for one cell. Returns and win rate diverge around their neutral point
 * (0 and 50% respectively); the drawdown metrics ramp red from zero, because there is no
 * "good" drawdown to paint green.
 */
export function monthlyMetricCellStyle(
  v: number | null,
  metric: MonthlyMetric,
): Record<string, string> {
  const neutral = { background: 'var(--bg-surface-2)' };
  if (v === null) return neutral;

  const green = (a: number) => ({ background: `rgba(34,197,94,${a.toFixed(2)})` });
  const red = (a: number) => ({ background: `rgba(255,59,48,${a.toFixed(2)})` });
  // Keeps faint values visible instead of fading into the surface colour.
  const ramp = (magnitude: number, cap: number) => Math.min(magnitude / cap, 1) * 0.85 + 0.15;

  if (metric === 'winRate') {
    const edge = v - 0.5;
    if (edge === 0) return neutral;
    // Saturates at 25 points away from a coin flip, i.e. 25% and 75%.
    const alpha = ramp(Math.abs(edge), 0.25);
    return edge > 0 ? green(alpha) : red(alpha);
  }

  if (metric === 'maxDrawdown' || metric === 'underwater') {
    if (v <= 0) return neutral;
    return red(ramp(v, 0.1));
  }

  if (v === 0) return neutral;
  const alpha = ramp(Math.abs(v), 0.1);
  return v > 0 ? green(alpha) : red(alpha);
}
