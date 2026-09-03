using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One trade of a run whose <c>Â</c> was successfully estimated, carrying WHERE its risk figure came
/// from as well as what it is (design.md D5/D6).
/// <para>
/// There is no scalar <c>R</c> here, only <see cref="RLow"/> and <see cref="RHigh"/>, for the same
/// reason <see cref="TradeRiskInterval"/> has no scalar accessor: an R computed from an imputed band
/// is a range, and collapsing it to one number would let a ranking treat a guess as a measurement.
/// </para>
/// </summary>
/// <param name="TradeId">The source <c>BacktestTrade.Id</c>.</param>
/// <param name="RowIndex">0-based ordinal within the source file — the stable key, together with the run.</param>
/// <param name="Ticket">Informational only; tickets collide across independently generated runs.</param>
/// <param name="CloseType">Verbatim SQX close type, the input that decided <see cref="Basis"/>.</param>
/// <param name="Size">The recorded lot size, already on the grid.</param>
/// <param name="Profit">Raw <c>Profit/Loss</c>. Used ONLY as the numerator of the R bounds, never as a risk source.</param>
/// <param name="Basis">Where <see cref="Risk"/> came from. Never omit it when quoting an R bound.</param>
/// <param name="Risk">The dollar risk band. A point only when <see cref="Basis"/> is <see cref="RiskBasis.Measured"/>.</param>
/// <param name="RLow">
/// Lower R bound, or null when the endpoint it needs is open or zero. NOTE the ordering rule:
/// <c>Profit &gt;= 0</c> puts <c>Profit/High</c> here, <c>Profit &lt; 0</c> puts <c>Profit/Low</c>
/// here — the endpoints SWAP, because dividing a negative number by a larger divisor gives a LARGER
/// result.
/// </param>
/// <param name="RHigh">Upper R bound, subject to the same swap.</param>
public sealed record NormalizedTrade(
    Guid TradeId,
    int RowIndex,
    long Ticket,
    string CloseType,
    decimal Size,
    decimal Profit,
    RiskBasis Basis,
    TradeRiskInterval Risk,
    decimal? RLow,
    decimal? RHigh);
