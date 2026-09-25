using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// PR P2 — rescales one <see cref="BacktestTrade"/> onto an FTMO account by MONEY PER POINT, never
/// by lot count (design.md Decision 2, spec.md's Rescaling requirement). <c>internal static</c>,
/// pure: no I/O.
/// <para>
/// Deliberately does NOT call <c>BacktestReadService</c>'s existing <c>TryNormalize</c> /
/// <see cref="TradeResizer.Resize"/> pairing (<c>BacktestReadService.cs:232-246</c>), which passes
/// ONE <c>grid</c> to both calls and scales P/L by <c>q'/q</c> — correct only when a source lot and
/// an FTMO lot move the same money per point. This projector takes <c>Â</c> (the run's own
/// risk-per-trade estimate, from <c>TryNormalize</c> on the DECLARED SOURCE grid) as a caller
/// input, and performs its own money-per-point arithmetic:
/// </para>
/// <para>
/// <c>M = C_ftmo · FX</c> (USD per point per FTMO lot). With <c>q</c> the source size and
/// <c>P_src</c> the source calibration point value:
/// <code>
/// u    = q · target · P_src / (Â · M)                     one quotient, TradeResizer's u
/// q'   = clamp(floor(u / step) · step, min, max)           lot-grid rounding enters HERE ONLY
/// net' = Profit · ((q' · M) / (q · P_src))                 Profit/(q·P_src) = points moved
/// </code>
/// When <c>P_src == M</c>, this reduces exactly to <see cref="TradeResizer"/> +
/// <c>BacktestNetSeries.Bridge</c> (a pinned identity, tested against the production code). The
/// size ratio is taken FIRST, then multiplied by <c>Profit</c>, matching <c>Bridge</c>'s
/// <c>Profit * (ResizedSize / OriginalSize)</c>: the multiply-first form is closer to the exact
/// rational by up to a few units of 10^-27, and that difference alone broke the identity on
/// roughly half of a 54,000-case sweep (e.g. <c>123.45 · 0.03 / 0.07</c> gives
/// <c>…857</c> where <c>Bridge</c> gives <c>…861</c>). Bit-identical agreement with the existing
/// group-risk path is worth more than precision far below a cent.
/// <para>
/// Run-level inputs are validated by <see cref="RefuseRunInputs"/> before any row is projected.
/// </para>
/// </para>
/// </summary>
internal static class FtmoTradeProjector
{
    /// <summary>
    /// One rescaled row. <c>Net</c> is null exactly when <c>Outcome == Unscalable</c> — a zero net
    /// would assert a breakeven trade that never happened.
    /// </summary>
    internal sealed record ProjectedTrade(
        int RowIndex,
        DateTime OpenSource,
        DateTime CloseSource,
        decimal? Net,
        ResizeOutcome Outcome,
        decimal FtmoLots);

    /// <summary>
    /// Projects <paramref name="trade"/> onto <paramref name="ftmoGrid"/> using the declared FTMO
    /// point value (<paramref name="ftmoContractSize"/> × <paramref name="fxRate"/>) and the
    /// source calibration point value <paramref name="sourcePointValue"/>.
    /// </summary>
    /// <param name="trade">The source backtest trade. <c>Size &lt;= 0</c> is <see cref="ResizeOutcome.Unscalable"/> — nothing to scale FROM.</param>
    /// <param name="estimatedRiskPerTrade">Â — the run's own risk-per-trade estimate on the declared SOURCE grid.</param>
    /// <param name="targetRiskPerTrade">The FTMO-side target risk per trade.</param>
    /// <param name="sourcePointValue">P_src — the backtest calibration's point value for this symbol.</param>
    /// <param name="ftmoContractSize">C_ftmo — the FTMO contract's point value (never inferred from calibration).</param>
    /// <param name="fxRate">FX — USD per unit of the FTMO contract's settlement currency. <c>1</c> when it already settles in USD.</param>
    /// <param name="ftmoGrid">The FTMO lot grid (step/min/max). Never <see cref="LotGrid.ImoxRetester"/> as a silent default.</param>
    /// <summary>
    /// The run-level guard, called ONCE before any row is projected. Every input here is a property
    /// of the run or its configuration, never of one row — the only per-row input,
    /// <see cref="BacktestTrade.Size"/>, is handled inside <see cref="Project"/> as
    /// <see cref="ResizeOutcome.Unscalable"/>. Zero is a divisor that would throw mid-replay;
    /// a negative value flips the sign of every lot count and net, which is worse because it
    /// throws nothing. Both are refused with the EXISTING reason that already owns the input:
    /// <list type="bullet">
    /// <item><c>Â &lt;= 0</c> → <see cref="FtmoSimulationRefusal.RiskNotEstimable"/> (no usable estimate).</item>
    /// <item><c>target &lt;= 0</c> → <see cref="FtmoSimulationRefusal.InvalidRequest"/> (bad target on the request).</item>
    /// <item><c>P_src &lt;= 0</c> → <see cref="FtmoSimulationRefusal.PointValueNotCalibrated"/> (no usable calibration).</item>
    /// <item><c>C_ftmo &lt;= 0</c> → <see cref="FtmoSimulationRefusal.InstrumentSpecMissing"/> (no usable instrument spec).</item>
    /// <item><c>FX &lt;= 0</c> → <see cref="FtmoSimulationRefusal.InvalidFxBand"/> (a degenerate rate).</item>
    /// </list>
    /// Checked in that order; the first failing input wins. <c>null</c> means every input is usable.
    /// </summary>
    internal static FtmoSimulationRefusal? RefuseRunInputs(
        decimal estimatedRiskPerTrade,
        decimal targetRiskPerTrade,
        decimal sourcePointValue,
        decimal ftmoContractSize,
        decimal fxRate)
    {
        if (estimatedRiskPerTrade <= 0m)
            return FtmoSimulationRefusal.RiskNotEstimable;

        if (targetRiskPerTrade <= 0m)
            return FtmoSimulationRefusal.InvalidRequest;

        if (sourcePointValue <= 0m)
            return FtmoSimulationRefusal.PointValueNotCalibrated;

        if (ftmoContractSize <= 0m)
            return FtmoSimulationRefusal.InstrumentSpecMissing;

        if (fxRate <= 0m)
            return FtmoSimulationRefusal.InvalidFxBand;

        return null;
    }

    internal static ProjectedTrade Project(
        BacktestTrade trade,
        decimal estimatedRiskPerTrade,
        decimal targetRiskPerTrade,
        decimal sourcePointValue,
        decimal ftmoContractSize,
        decimal fxRate,
        LotGrid ftmoGrid)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(ftmoGrid);

        // Defence in depth behind RefuseRunInputs: reaching here with a non-positive run input is a
        // wiring bug, so it is a NAMED programming error rather than a DivideByZeroException (zero)
        // or a silently sign-flipped series (negative).
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedRiskPerTrade);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetRiskPerTrade);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourcePointValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ftmoContractSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fxRate);

        var q = trade.Size;
        if (q <= 0m)
        {
            return new ProjectedTrade(
                trade.RowIndex, trade.OpenTime, trade.CloseTime, Net: null, ResizeOutcome.Unscalable, FtmoLots: 0m);
        }

        var m = ftmoContractSize * fxRate;
        var u = q * targetRiskPerTrade * sourcePointValue / (estimatedRiskPerTrade * m);
        var floored = Math.Floor(u / ftmoGrid.Step) * ftmoGrid.Step;

        decimal qPrime;
        ResizeOutcome outcome;
        if (floored < ftmoGrid.MinLot)
        {
            qPrime = ftmoGrid.MinLot;
            outcome = ResizeOutcome.RaisedToMinimum;
        }
        else if (floored > ftmoGrid.MaxLots)
        {
            qPrime = ftmoGrid.MaxLots;
            outcome = ResizeOutcome.CappedAtMaximum;
        }
        else
        {
            qPrime = floored;
            outcome = ResizeOutcome.OnTarget;
        }

        var netPrime = trade.Profit * (qPrime * m / (q * sourcePointValue));

        return new ProjectedTrade(trade.RowIndex, trade.OpenTime, trade.CloseTime, netPrime, outcome, qPrime);
    }
}
