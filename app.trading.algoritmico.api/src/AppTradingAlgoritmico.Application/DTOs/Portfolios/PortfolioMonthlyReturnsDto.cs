using AppTradingAlgoritmico.Application.DTOs.Trades;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>
/// Monthly compounding returns of one portfolio, for the broker-level
/// "monthly returns per portfolio" matrix. Returns are computed on demand from the
/// member strategies' trades against the portfolio's initial capital; portfolios
/// without members (or without trades) carry an empty list.
/// </summary>
public sealed record PortfolioMonthlyReturnsDto(
    Guid PortfolioId,
    string Name,
    int MemberCount,
    IReadOnlyList<MonthlyReturnDto> Returns);
