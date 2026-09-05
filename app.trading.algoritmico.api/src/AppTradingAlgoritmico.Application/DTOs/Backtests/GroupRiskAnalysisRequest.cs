using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One backtest group risk analysis, as asked for (design.md D8a/D8b).
/// <para>
/// It describes exactly ONE caller-named group. There is no list of groups and no ranking input:
/// comparing candidate groupings is a different capability, and keeping that impossible to express
/// here is one of this slice's two boundary tripwires.
/// </para>
/// </summary>
/// <param name="StrategyIds">The group's members. Order is preserved in the output so the payload is deterministic.</param>
/// <param name="InitialCapital">The percentage denominator for every VaR readout, echoed on the output so the figures can be read.</param>
/// <param name="TargetRiskPerTrade">The risk per trade every member's run is resized onto. THIS is the sizing decision.</param>
/// <param name="Segment">
/// <b>Nullable, and that is load-bearing.</b> <see cref="BacktestSegment.Unknown"/> is the enum's
/// default (<c>0</c>), so an omitted JSON property would bind to it and a non-nullable field could
/// not tell the caller who FORGOT from the caller who deliberately asked for <c>Unknown</c>.
/// "Required input" would then be unsatisfiable as typed. Both are refused, by different rules:
/// see <see cref="GroupRiskAnalysisStatus.SegmentNotSpecified"/> and
/// <see cref="GroupRiskAnalysisStatus.UnknownSegmentNotSelectable"/>.
/// </param>
/// <param name="RunKind">
/// The operator's explicit disambiguator for a strategy whose BOTH runs carry the requested
/// segment. It only narrows candidates that already matched — <c>Kind</c> never infers or overrides
/// <c>Segment</c>.
/// </param>
/// <param name="FundingService">
/// The broker label the group is analysed under, used for the per-service breakdown and to look up
/// the configured <c>BrokerRiskLimits</c> band readout. Null means the members carry none.
/// </param>
/// <param name="PortfolioId">
/// Optional. When supplied, each member's allocation weight is read from its
/// <c>PortfolioStrategy.Weight</c> and a weight other than <c>1</c> REFUSES the member: the series
/// is already sized at <paramref name="TargetRiskPerTrade"/>, so a second factor would double-size
/// it. Absent it every member's weight is <c>1</c> — a bare group carries no allocation decision.
/// </param>
/// <param name="SizeDecimals">Lot-grid decimals. Defaults describe the grid the committed XAUUSD exports were produced on.</param>
/// <param name="Step">Lot-grid quantum.</param>
/// <param name="MinLot">Smallest tradable size.</param>
/// <param name="MaxLots">Largest tradable size.</param>
public sealed record GroupRiskAnalysisRequest(
    Guid[] StrategyIds,
    decimal InitialCapital,
    decimal TargetRiskPerTrade,
    BacktestSegment? Segment,
    BacktestRunKind? RunKind = null,
    string? FundingService = null,
    Guid? PortfolioId = null,
    int SizeDecimals = 2,
    decimal Step = 0.01m,
    decimal MinLot = 0.01m,
    decimal MaxLots = 10m)
{
    /// <summary>
    /// The requested grid, or null when the four fields do not describe a valid one.
    /// <see cref="LotGrid"/>'s constructor validates rather than clamps, so an invalid grid is a
    /// refusal (<see cref="GroupRiskAnalysisStatus.InvalidLotGrid"/>) instead of a silently
    /// corrected one.
    /// </summary>
    public LotGrid? TryBuildGrid()
    {
        try
        {
            return new LotGrid(SizeDecimals, Step, MinLot, MaxLots);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
