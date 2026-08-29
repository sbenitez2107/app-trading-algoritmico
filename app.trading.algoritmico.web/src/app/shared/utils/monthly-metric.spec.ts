import {
  MonthlyMetricSource,
  formatMonthlyCell,
  formatMonthlyColumnGrandTotal,
  formatMonthlyColumnTotal,
  monthlyColumnGrandTotal,
  monthlyColumnTotal,
  formatMonthlyMetric,
  formatMonthlyTotalCell,
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

describe('formatMonthlyCell', () => {
  it('leadsTheWinRateWithItsRawCounts', () => {
    expect(formatMonthlyCell(month({ winCount: 3, lossCount: 1 }), 'winRate')).toBe('3/1 (75%)');
  });

  it('roundsTheWinRatePercentageBecauseTheCountsCarryThePrecision', () => {
    expect(formatMonthlyCell(month({ winCount: 4, lossCount: 5 }), 'winRate')).toBe('4/5 (44%)');
  });

  it('keepsThePlainPercentageForEveryOtherMetric', () => {
    expect(formatMonthlyCell(month({ returnPercent: 0.0123 }), 'return')).toBe('1.23%');
  });

  it('rendersAnEmDashWhenTheMonthHasNothingToReport', () => {
    expect(formatMonthlyCell(null, 'winRate')).toBe('—');
    // A month of nothing but breakeven trades has no win rate.
    expect(formatMonthlyCell(month({ winCount: 0, lossCount: 0 }), 'winRate')).toBe('—');
  });
});

describe('formatMonthlyTotalCell', () => {
  it('sumsTheCountsAcrossMonthsForTheWinRate', () => {
    const months = [
      month({ winCount: 3, lossCount: 1 }),
      null,
      month({ winCount: 1, lossCount: 3 }),
    ];
    expect(formatMonthlyTotalCell(months, 'winRate')).toBe('4/4 (50%)');
  });

  it('keepsThePlainPercentageForEveryOtherMetric', () => {
    const months = [month({ returnPercent: 0.1 }), month({ returnPercent: 0.1 })];
    expect(formatMonthlyTotalCell(months, 'return')).toBe('21.00%');
  });

  it('rendersAnEmDashWhenNoMonthHasData', () => {
    expect(formatMonthlyTotalCell([null, null], 'winRate')).toBe('—');
  });
});

describe('formatMonthlyColumnTotal', () => {
  it('sumsReturnsAcrossStrategiesBecauseTheyShareOneAccountBalance', () => {
    const cells = [month({ returnPercent: 0.01 }), month({ returnPercent: 0.005 }), null];
    expect(formatMonthlyColumnTotal(cells, 'return')).toBe('1.50%');
  });

  it('sumsDrawdownsOnTheSameBase', () => {
    const cells = [month({ maxDrawdownPercent: 0.02 }), month({ maxDrawdownPercent: 0.03 })];
    expect(formatMonthlyColumnTotal(cells, 'maxDrawdown')).toBe('5.00%');
  });

  it('poolsTheWinRateCountsInsteadOfAddingPercentages', () => {
    const cells = [month({ winCount: 3, lossCount: 1 }), month({ winCount: 1, lossCount: 3 })];
    // Adding 75% and 25% would read 100%; the pooled figure is 4/4 = 50%.
    expect(formatMonthlyColumnTotal(cells, 'winRate')).toBe('4/4 (50%)');
  });

  it('rendersAnEmDashWhenNoStrategyTradedThatMonth', () => {
    expect(formatMonthlyColumnTotal([null, null], 'return')).toBe('—');
    // Every strategy traded, but none decided a trade: still nothing to report.
    expect(formatMonthlyColumnTotal([month(), month()], 'winRate')).toBe('—');
  });
});

describe('formatMonthlyColumnGrandTotal', () => {
  it('compoundsWithinEachStrategyThenSumsAcrossThem', () => {
    const a = [month({ returnPercent: 0.1 }), month({ returnPercent: 0.1 })];
    const b = [month({ returnPercent: 0.05 })];
    // a compounds to 21%, b is 5%.
    expect(formatMonthlyColumnGrandTotal([a, b], 'return')).toBe('26.00%');
  });

  it('sumsTheWorstMonthOfEachStrategyForDrawdowns', () => {
    const a = [month({ maxDrawdownPercent: 0.02 }), month({ maxDrawdownPercent: 0.06 })];
    const b = [month({ maxDrawdownPercent: 0.01 })];
    expect(formatMonthlyColumnGrandTotal([a, b], 'maxDrawdown')).toBe('7.00%');
  });

  it('poolsEveryCountOfEveryStrategyForTheWinRate', () => {
    const a = [month({ winCount: 3, lossCount: 1 }), month({ winCount: 1, lossCount: 1 })];
    const b = [month({ winCount: 0, lossCount: 2 })];
    expect(formatMonthlyColumnGrandTotal([a, b], 'winRate')).toBe('4/4 (50%)');
  });

  it('rendersAnEmDashWithNoStrategies', () => {
    expect(formatMonthlyColumnGrandTotal([], 'return')).toBe('—');
  });
});

describe('monthlyColumnTotal', () => {
  it('returnsTheRawSumSoCallersCanColourIt', () => {
    const cells = [month({ returnPercent: 0.01 }), month({ returnPercent: -0.03 })];
    expect(monthlyColumnTotal(cells, 'return')).toBeCloseTo(-0.02, 10);
  });

  it('returnsThePooledRatioForTheWinRate', () => {
    const cells = [month({ winCount: 3, lossCount: 1 }), month({ winCount: 1, lossCount: 3 })];
    expect(monthlyColumnTotal(cells, 'winRate')).toBe(0.5);
  });

  it('returnsNullWhenThereIsNothingToAggregate', () => {
    expect(monthlyColumnTotal([null], 'return')).toBeNull();
    expect(monthlyColumnTotal([month()], 'winRate')).toBeNull();
  });
});

describe('monthlyColumnGrandTotal', () => {
  it('agreesWithItsFormattedCounterpart', () => {
    const a = [month({ returnPercent: 0.1 }), month({ returnPercent: 0.1 })];
    const b = [month({ returnPercent: 0.05 })];
    expect(monthlyColumnGrandTotal([a, b], 'return')).toBeCloseTo(0.26, 10);
    expect(formatMonthlyColumnGrandTotal([a, b], 'return')).toBe('26.00%');
  });

  it('returnsNullWithNoStrategies', () => {
    expect(monthlyColumnGrandTotal([], 'return')).toBeNull();
  });
});
