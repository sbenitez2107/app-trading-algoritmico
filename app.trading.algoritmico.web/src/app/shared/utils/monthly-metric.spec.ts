import {
  MonthlyMetricSource,
  formatMonthlyMetric,
  isLowerBetter,
  monthlyMetricCellStyle,
  monthlyMetricTooltip,
  monthlyMetricTotal,
  monthlyMetricValue,
} from './monthly-metric';

const month = (over: Partial<MonthlyMetricSource> = {}): MonthlyMetricSource => ({
  returnPercent: 0,
  maxDrawdownPercent: 0,
  underwaterPercent: 0,
  winCount: 0,
  lossCount: 0,
  ...over,
});

describe('monthlyMetricValue', () => {
  it('readsEachMetric_FromItsOwnField', () => {
    const m = month({ returnPercent: 0.05, maxDrawdownPercent: 0.02, underwaterPercent: 0.08 });
    expect(monthlyMetricValue(m, 'return')).toBe(0.05);
    expect(monthlyMetricValue(m, 'maxDrawdown')).toBe(0.02);
    expect(monthlyMetricValue(m, 'underwater')).toBe(0.08);
  });

  it('computesWinRate_FromDecidedTradesOnly', () => {
    expect(monthlyMetricValue(month({ winCount: 3, lossCount: 1 }), 'winRate')).toBe(0.75);
  });

  it('returnsNullWinRate_WhenNoTradeWasDecided', () => {
    // Breakeven-only month: 0 wins and 0 losses is not a 0% win rate.
    expect(monthlyMetricValue(month(), 'winRate')).toBeNull();
  });
});

describe('monthlyMetricTotal', () => {
  it('compoundsReturns_AcrossMonthsWithData', () => {
    const total = monthlyMetricTotal(
      [month({ returnPercent: 0.1 }), null, month({ returnPercent: 0.1 })],
      'return',
    );
    expect(total).toBeCloseTo(0.21, 10);
  });

  it('takesTheWorstMonth_ForDrawdownMetrics', () => {
    const months = [month({ underwaterPercent: 0.03 }), month({ underwaterPercent: 0.08 })];
    expect(monthlyMetricTotal(months, 'underwater')).toBe(0.08);
  });

  it('recomputesWinRate_FromSummedCounts_NotAveragedMonths', () => {
    // 1/2 in a tiny month and 90/110 in a big one. Averaging the rates gives ~0.66;
    // the honest figure weighs by trade count → 91/202.
    const months = [month({ winCount: 1, lossCount: 1 }), month({ winCount: 90, lossCount: 110 })];
    expect(monthlyMetricTotal(months, 'winRate')).toBeCloseTo(91 / 202, 10);
  });

  it('returnsNull_WhenNoMonthHasData', () => {
    expect(monthlyMetricTotal([null, null], 'return')).toBeNull();
  });
});

describe('monthlyMetricCellStyle', () => {
  it('paintsDrawdownRed_AndNeverGreen', () => {
    expect(monthlyMetricCellStyle(0.05, 'maxDrawdown')['background']).toContain('255,59,48');
    expect(monthlyMetricCellStyle(0, 'maxDrawdown')['background']).toBe('var(--bg-surface-2)');
  });

  it('divergesWinRate_AroundFiftyPercent', () => {
    expect(monthlyMetricCellStyle(0.6, 'winRate')['background']).toContain('34,197,94');
    expect(monthlyMetricCellStyle(0.4, 'winRate')['background']).toContain('255,59,48');
    expect(monthlyMetricCellStyle(0.5, 'winRate')['background']).toBe('var(--bg-surface-2)');
  });

  it('divergesReturn_AroundZero', () => {
    expect(monthlyMetricCellStyle(0.02, 'return')['background']).toContain('34,197,94');
    expect(monthlyMetricCellStyle(-0.02, 'return')['background']).toContain('255,59,48');
  });

  it('rendersMissingMonths_AsNeutral', () => {
    expect(monthlyMetricCellStyle(null, 'return')['background']).toBe('var(--bg-surface-2)');
  });
});

describe('metric helpers', () => {
  it('ranksDrawdownsAscending_AndTheRestDescending', () => {
    expect(isLowerBetter('maxDrawdown')).toBe(true);
    expect(isLowerBetter('underwater')).toBe(true);
    expect(isLowerBetter('return')).toBe(false);
    expect(isLowerBetter('winRate')).toBe(false);
  });

  it('formatsEveryMetric_AsAPercentage', () => {
    expect(formatMonthlyMetric(0.0525)).toBe('5.25%');
    expect(formatMonthlyMetric(null)).toBe('—');
  });

  it('showsRawCounts_OnlyForWinRate', () => {
    const m = month({ winCount: 12, lossCount: 8 });
    expect(monthlyMetricTooltip(m, 'winRate')).toBe('12 W / 8 L');
    expect(monthlyMetricTooltip(m, 'return')).toBeNull();
    expect(monthlyMetricTooltip(null, 'winRate')).toBeNull();
  });
});
