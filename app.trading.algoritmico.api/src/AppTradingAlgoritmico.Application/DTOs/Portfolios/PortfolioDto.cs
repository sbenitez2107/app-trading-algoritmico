using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>A portfolio with its membership. Money fields are in <see cref="BaseCurrency"/>.</summary>
public sealed record PortfolioDto(
    Guid Id,
    string Name,
    string? Description,
    string Broker,
    AccountType AccountType,
    decimal InitialCapital,
    string BaseCurrency,
    int MemberCount,
    DateTime CreatedAt,
    IReadOnlyList<PortfolioMemberDto> Members);

/// <summary>A member strategy of a portfolio, enriched with its source account for display.</summary>
public sealed record PortfolioMemberDto(
    Guid StrategyId,
    string StrategyName,
    Guid? AccountId,
    string? AccountName,
    string? Broker,
    decimal Weight);

public sealed record CreatePortfolioDto(
    string Name,
    string? Description,
    string Broker,
    AccountType AccountType,
    decimal InitialCapital,
    string? BaseCurrency,
    IReadOnlyList<AddPortfolioMemberDto>? Members = null);

/// <summary>Updates portfolio metadata. <c>AccountType</c> is immutable (it scopes the members).</summary>
public sealed record UpdatePortfolioDto(
    string Name,
    string? Description,
    decimal InitialCapital,
    string? BaseCurrency);

public sealed record AddPortfolioMemberDto(
    Guid StrategyId,
    decimal Weight);

public sealed record UpdateMemberWeightDto(
    decimal Weight);
