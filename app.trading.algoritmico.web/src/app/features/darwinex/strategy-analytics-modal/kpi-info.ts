/**
 * Per-KPI explanation displayed inside the help tooltip in the analytics modal.
 *
 * `what` — concise definition of the metric.
 * `good` — interpretation guide (when is the value good / bad / neutral).
 *
 * Keep entries short and concrete; the tooltip is shown over the cards.
 */
export interface KpiInfo {
  what: string;
  good: string;
}

export const KPI_INFO: Record<string, KpiInfo> = {
  // --- Returns ---
  totalReturn: {
    what: 'Total accumulated return over the initial balance: NetProfit / InitialBalance.',
    good: 'Positive is the goal. Compare against the time span — +20% in 6 months is excellent; +5% in 5 years is poor.',
  },
  netProfit: {
    what: 'Sum of P/L across every trade, including commission, swap and taxes.',
    good: 'Positive = profitable strategy. Negative = losing money after costs.',
  },
  cagr: {
    what: 'Compound Annual Growth Rate: ((finalEquity / initialBalance) ^ (1 / years)) − 1.',
    good: '> 15% = strong. > 25% = excellent. < 5% = barely beats inflation. Always read alongside drawdown.',
  },
  yearlyAvgProfit: {
    what: 'NetProfit annualised by the calendar span of the trade history.',
    good: 'Compare against your annual income target.',
  },
  monthlyAvgProfit: {
    what: 'Yearly Avg Profit divided by 12.',
    good: 'Useful for cash-flow expectations on a monthly basis.',
  },
  dailyAvgProfit: {
    what: 'NetProfit divided by the number of calendar days the strategy has been running.',
    good: 'Mostly informational — daily P/L variance is typically very high.',
  },
  ahpr: {
    what: 'Average Holding Period Return: average per-trade return = avg(NetProfit / EquityAtOpen).',
    good: '> 0 means each trade is profitable on average. Higher = better edge per trade.',
  },

  // --- Drawdown / Risk-Adjusted ---
  maxDrawdownAmount: {
    what: 'Largest peak-to-valley drop in equity, in dollars.',
    good: 'Lower is better. Always read as a % of capital.',
  },
  maxDrawdownPercent: {
    what: 'Max Drawdown expressed as % of the equity peak when the drawdown started.',
    good: '< 10% excellent · 10–20% acceptable · 20–30% concerning · > 30% high risk of ruin.',
  },
  returnDrawdownRatio: {
    what: 'TotalReturn / MaxDrawdown%. How much you earned per unit of risk taken.',
    good: '> 1 = good · > 3 = excellent · < 1 = the drawdown was bigger than the gain.',
  },
  annualReturnMaxDdRatio: {
    what: 'CAGR / MaxDrawdown%. Also called Calmar Ratio — risk-adjusted annual return.',
    good: '> 0.5 acceptable · > 1 good · > 3 excellent.',
  },
  stagnationInDays: {
    what: 'Longest period (in calendar days) without a new equity high.',
    good: '< 60 days = healthy · 60–180 = tolerable · > 180 = system may be broken.',
  },
  sharpeRatio: {
    what: 'Sharpe over the synthetic daily-return series, annualised with √252.',
    good: '> 1 acceptable · > 2 good · > 3 excellent · < 0 the strategy lost money on a risk-adjusted basis.',
  },
  sqn: {
    what: "System Quality Number: mean(NetProfitPerTrade) / std × √N. Van Tharp's rating.",
    good: '< 1.6 poor · 1.6–2.0 below average · 2.0–2.5 average · 2.5–3.0 good · 3.0–5.0 excellent · > 5.0 superb.',
  },
  standardDeviation: {
    what: 'Standard deviation of net profit per closed trade — measures trade-to-trade volatility.',
    good: 'No absolute target. Compare against AvgTrade: a high std relative to a small avg = unreliable system.',
  },

  // --- Trade Stats ---
  closedCount: {
    what: 'Total number of closed trades.',
    good: '≥ 100 trades for statistically meaningful KPIs. Below 30 numbers are noisy.',
  },
  winsLosses: {
    what: 'Number of winning trades / number of losing trades (closed only).',
    good: 'Read together with PayoutRatio — high wins% with low payout can still be unprofitable.',
  },
  winRate: {
    what: 'WinCount / ClosedCount. Probability that a closed trade is profitable.',
    good: 'No magic number. 30% wins with 3:1 payout beats 70% wins with 0.3:1 payout. Use Expectancy instead.',
  },
  profitFactor: {
    what: 'GrossProfit / |GrossLoss|. Dollars earned per dollar lost.',
    good: '< 1 losing system · 1–1.5 marginal · 1.5–2 good · > 2 excellent · > 4 suspicious (overfit?).',
  },
  payoutRatio: {
    what: 'AverageWin / |AverageLoss|. Reward-to-risk per trade.',
    good: '> 1 = winners bigger than losers. > 2 = strong edge. Often inversely correlated with WinRate.',
  },
  winsLossesRatio: {
    what: 'WinCount / LossCount.',
    good: 'Mostly informational. Combine with PayoutRatio.',
  },
  expectancy: {
    what: 'Average net profit per trade. (WinRate × AvgWin) − (LossRate × |AvgLoss|) − costs.',
    good: 'Must be positive — a negative expectancy means the system loses money trade after trade.',
  },
  rExpectancy: {
    what: 'Expectancy / |AvgLoss|. Average reward in multiples of "1R" (one average loss).',
    good: '> 0.25R = good · > 0.5R = excellent · ≤ 0 = the system is leaking money.',
  },
  averageTrade: {
    what: 'Net profit per closed trade on average — same as Expectancy.',
    good: 'Must comfortably cover spread + commission. Below the cost of one trade = unviable.',
  },
  averageWin: {
    what: 'Average net profit across winning trades only.',
    good: "Compare against AverageLoss — that's your reward/risk profile.",
  },
  averageLoss: {
    what: 'Average net loss across losing trades only.',
    good: 'Smaller (closer to zero) = tighter risk control.',
  },
  largestWin: {
    what: 'Single best trade in the entire history.',
    good: 'If it dominates GrossProfit, the strategy may rely on a fluke. Drop it and re-check expectancy.',
  },
  largestLoss: {
    what: 'Single worst trade in the entire history.',
    good: 'Should be in the same order of magnitude as AvgLoss. A large outlier suggests broken risk management.',
  },
  grossProfit: {
    what: 'Sum of net P/L across winning trades only.',
    good: 'Higher is better; only meaningful in combination with GrossLoss (see Profit Factor).',
  },
  grossLoss: {
    what: 'Sum of net P/L across losing trades only (negative number).',
    good: 'Less negative is better; combined with GrossProfit produces Profit Factor.',
  },
  commission: {
    what: 'Sum of commissions paid (typically negative).',
    good: 'No target — informational. Watch the commission/profit ratio: high commissions can wipe out edge.',
  },
  swap: {
    what: 'Sum of overnight swap charges (positive or negative depending on direction and pair).',
    good: 'Watch the magnitude — heavy negative swap is a sign of holding losers too long.',
  },
  taxes: {
    what: 'Sum of taxes withheld at the broker level.',
    good: 'Informational; depends on broker / jurisdiction.',
  },

  // --- Streaks ---
  maxConsecutiveWins: {
    what: 'Longest streak of consecutive winning closed trades.',
    good: 'Big streaks are pleasant, but they can also signal the system is not stationary — keep an eye on Z-Score.',
  },
  maxConsecutiveLosses: {
    what: 'Longest streak of consecutive losing closed trades.',
    good: 'You must be psychologically prepared for this. If maxConsecLosses × avgLoss > tolerable drawdown, reduce size.',
  },
  averageConsecutiveWins: {
    what: 'Average length of winning streaks.',
    good: 'Helpful for sizing expectations between losing streaks.',
  },
  averageConsecutiveLosses: {
    what: 'Average length of losing streaks.',
    good: 'Bigger = the system tends to cluster losses; reinforces position sizing rules.',
  },
  zScore: {
    what: 'Statistical Z-score on win/loss runs. Tests whether wins and losses cluster (negative Z) or alternate (positive Z) more than chance.',
    good: '|Z| < 1.96 random (95% confidence). |Z| > 2 = clustering or alternating is statistically significant.',
  },
  zProbability: {
    what: 'Two-tailed probability that the observed |Z-score| is NOT random (1 − pNormal).',
    good: '> 95% = clustering / alternating is real. < 95% = could be noise.',
  },

  // --- Other ---
  exposure: {
    what: 'Percentage of wall-clock time with at least one open position. Overlapping trades count once.',
    good: 'Lower exposure with the same return = better risk-adjusted system. Compare across strategies.',
  },
  firstTrade: { what: 'Open time of the earliest closed trade in the dataset.', good: '' },
  lastTrade: { what: 'Close time of the most recent closed trade in the dataset.', good: '' },
};
