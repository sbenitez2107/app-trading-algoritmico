import { computeEquityAnnotations } from './equity-annotations';
import { PortfolioEquityPointDto } from '../../../core/services/portfolio.service';

/**
 * Builds a realistic equity curve from a list of equities, computing per-point drawdown
 * against the running peak exactly like the backend does. Days are consecutive from Jan 1.
 */
function curve(equities: number[]): PortfolioEquityPointDto[] {
  let peak = -Infinity;
  return equities.map((equity, i) => {
    peak = Math.max(peak, equity);
    const drawdown = peak - equity;
    return {
      date: `2026-01-${String(i + 1).padStart(2, '0')}T00:00:00Z`,
      equity,
      drawdown,
      drawdownPercent: peak > 0 ? drawdown / peak : 0,
    };
  });
}

describe('computeEquityAnnotations', () => {
  it('returns nulls for an empty curve', () => {
    expect(computeEquityAnnotations([])).toEqual({ maxDrawdown: null, stagnation: null });
  });

  it('finds the deepest peak-relative drop as the max drawdown', () => {
    // peaks: 100,110,110,110,120 → drawdowns: 0,0,5,20,0 → trough at equity 90 (day 4).
    const { maxDrawdown } = computeEquityAnnotations(curve([100, 110, 105, 90, 120]));

    expect(maxDrawdown).not.toBeNull();
    expect(maxDrawdown!.day).toBe('2026-01-04');
    expect(maxDrawdown!.equity).toBe(90);
    expect(maxDrawdown!.amount).toBe(20); // 110 peak − 90
    expect(maxDrawdown!.percent).toBeCloseTo(20 / 110, 6);
  });

  it('marks the longest span from a peak until equity exceeds it again', () => {
    // peak 110 set on day 2; equity stays below (105, 90) until day 5 (120) → 3-day stagnation.
    const { stagnation } = computeEquityAnnotations(curve([100, 110, 105, 90, 120]));

    expect(stagnation).not.toBeNull();
    expect(stagnation!.fromDay).toBe('2026-01-02');
    expect(stagnation!.toDay).toBe('2026-01-05');
    expect(stagnation!.days).toBe(3);
  });

  it('counts a trailing stagnation that never recovers to a new high', () => {
    // New high on day 2 (120), then never beaten → stagnation runs day 2 → day 4 (2 days).
    const { stagnation } = computeEquityAnnotations(curve([100, 120, 110, 115]));

    expect(stagnation!.fromDay).toBe('2026-01-02');
    expect(stagnation!.toDay).toBe('2026-01-04');
    expect(stagnation!.days).toBe(2);
  });

  it('reports no drawdown for a strictly rising curve', () => {
    const { maxDrawdown } = computeEquityAnnotations(curve([100, 110, 120, 130]));
    expect(maxDrawdown).toBeNull();
  });
});
