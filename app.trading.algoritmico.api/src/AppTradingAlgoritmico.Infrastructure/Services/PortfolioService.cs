using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Persistence + orchestration for platform-level portfolios. Membership and weights are stored;
/// every analytics figure is recomputed on demand from current member trades via
/// <see cref="PortfolioAnalyticsCalculator"/> (no cached/precomputed numbers → never stale).
/// </summary>
public sealed class PortfolioService(AppDbContext db) : IPortfolioService
{
    private sealed record StratInfo(Guid Id, string Name, Guid? TradingAccountId);
    private sealed record AccInfo(Guid Id, string Name, string Broker);

    // -------------------------------------------------------------------------
    // CRUD
    // -------------------------------------------------------------------------

    public async Task<PagedResult<PortfolioDto>> GetAllAsync(string? broker = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.Portfolios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(broker))
            query = query.Where(p => p.Broker == broker);
        var totalCount = await query.CountAsync(ct);

        var portfolios = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Members)
            .ToListAsync(ct);

        var dtos = await BuildDtosAsync(portfolios, ct);
        return new PagedResult<PortfolioDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<PortfolioDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var portfolio = await db.Portfolios
            .AsNoTracking()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Portfolio {id} not found.");

        var dtos = await BuildDtosAsync([portfolio], ct);
        return dtos[0];
    }

    public async Task<PortfolioDto> CreateAsync(CreatePortfolioDto dto, CancellationToken ct = default)
    {
        var entity = new Portfolio
        {
            Name = dto.Name,
            Description = dto.Description,
            Broker = dto.Broker.Trim(),
            AccountType = dto.AccountType,
            InitialCapital = dto.InitialCapital,
            BaseCurrency = NormalizeCurrency(dto.BaseCurrency),
            CreatedAt = DateTime.UtcNow
        };

        // Optional: create with members in one shot (the portfolio builder). Validate every
        // strategy belongs to an account of this portfolio's broker + type before persisting.
        if (dto.Members is { Count: > 0 })
        {
            var distinct = dto.Members
                .GroupBy(m => m.StrategyId)
                .Select(g => g.First())
                .ToList();

            await ValidateMembersAsync(entity.Broker, dto.AccountType, distinct.Select(m => m.StrategyId).ToList(), ct);

            foreach (var m in distinct)
            {
                entity.Members.Add(new PortfolioStrategy
                {
                    StrategyId = m.StrategyId,
                    Weight = m.Weight > 0 ? m.Weight : 1m,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        db.Portfolios.Add(entity);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    /// <summary>Validates that every strategy exists and belongs to an account of the given broker + type.</summary>
    private async Task ValidateMembersAsync(
        string broker, AccountType portfolioType, IReadOnlyCollection<Guid> strategyIds, CancellationToken ct)
    {
        if (strategyIds.Count == 0) return;

        var info = await db.Strategies
            .AsNoTracking()
            .Where(s => strategyIds.Contains(s.Id))
            .Select(s => new { s.Id, s.TradingAccountId })
            .ToListAsync(ct);

        var found = info.Select(i => i.Id).ToHashSet();
        var missing = strategyIds.FirstOrDefault(id => !found.Contains(id));
        if (missing != Guid.Empty && !found.Contains(missing))
            throw new KeyNotFoundException($"Strategy {missing} not found.");

        if (info.Any(i => i.TradingAccountId is null))
            throw new InvalidOperationException(
                "One or more strategies are not assigned to a trading account.");

        var accountIds = info.Select(i => i.TradingAccountId!.Value).Distinct().ToList();
        var accById = (await db.TradingAccounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => new { a.Id, a.AccountType, a.Broker })
                .ToListAsync(ct))
            .ToDictionary(a => a.Id);

        foreach (var i in info)
        {
            if (!accById.TryGetValue(i.TradingAccountId!.Value, out var acc) || acc.AccountType != portfolioType)
                throw new InvalidOperationException($"All strategies must belong to {portfolioType} accounts.");
            if (acc.Broker != broker)
                throw new InvalidOperationException($"All strategies must belong to {broker} accounts.");
        }
    }

    public async Task<PortfolioDto> UpdateAsync(Guid id, UpdatePortfolioDto dto, CancellationToken ct = default)
    {
        var entity = await db.Portfolios.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Portfolio {id} not found.");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.InitialCapital = dto.InitialCapital;
        if (!string.IsNullOrWhiteSpace(dto.BaseCurrency))
            entity.BaseCurrency = NormalizeCurrency(dto.BaseCurrency);
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Portfolios.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Portfolio {id} not found.");

        db.Portfolios.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Membership
    // -------------------------------------------------------------------------

    public async Task<PortfolioDto> AddMemberAsync(Guid portfolioId, AddPortfolioMemberDto dto, CancellationToken ct = default)
    {
        var portfolio = await db.Portfolios
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, ct)
            ?? throw new KeyNotFoundException($"Portfolio {portfolioId} not found.");

        var strategy = await db.Strategies
            .AsNoTracking()
            .Where(s => s.Id == dto.StrategyId)
            .Select(s => new { s.Id, s.TradingAccountId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Strategy {dto.StrategyId} not found.");

        if (strategy.TradingAccountId is not Guid accountId)
            throw new InvalidOperationException(
                "Strategy is not assigned to a trading account, so it cannot join a portfolio.");

        var account = await db.TradingAccounts
            .AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.AccountType, a.Broker })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Strategy's trading account was not found.");

        if (account.AccountType != portfolio.AccountType)
            throw new InvalidOperationException(
                $"Strategy belongs to a {account.AccountType} account but this portfolio is scoped to {portfolio.AccountType}.");

        if (account.Broker != portfolio.Broker)
            throw new InvalidOperationException(
                $"Strategy belongs to {account.Broker} but this portfolio is scoped to {portfolio.Broker}.");

        if (portfolio.Members.Any(m => m.StrategyId == dto.StrategyId))
            throw new InvalidOperationException("Strategy is already a member of this portfolio.");

        portfolio.Members.Add(new PortfolioStrategy
        {
            PortfolioId = portfolioId,
            StrategyId = dto.StrategyId,
            Weight = dto.Weight > 0 ? dto.Weight : 1m,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(portfolioId, ct);
    }

    public async Task<PortfolioDto> UpdateMemberWeightAsync(
        Guid portfolioId, Guid strategyId, decimal weight, CancellationToken ct = default)
    {
        var member = await db.PortfolioStrategies
            .FirstOrDefaultAsync(m => m.PortfolioId == portfolioId && m.StrategyId == strategyId, ct)
            ?? throw new KeyNotFoundException(
                $"Strategy {strategyId} is not a member of portfolio {portfolioId}.");

        member.Weight = weight < 0 ? 0m : weight;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(portfolioId, ct);
    }

    public async Task<PortfolioDto> RemoveMemberAsync(Guid portfolioId, Guid strategyId, CancellationToken ct = default)
    {
        var member = await db.PortfolioStrategies
            .FirstOrDefaultAsync(m => m.PortfolioId == portfolioId && m.StrategyId == strategyId, ct)
            ?? throw new KeyNotFoundException(
                $"Strategy {strategyId} is not a member of portfolio {portfolioId}.");

        db.PortfolioStrategies.Remove(member);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(portfolioId, ct);
    }

    // -------------------------------------------------------------------------
    // Trades (combined member trades)
    // -------------------------------------------------------------------------

    public async Task<PagedResult<PortfolioTradeDto>> GetTradesAsync(
        Guid portfolioId, TradeStatusFilter status, int page, int pageSize, CancellationToken ct = default)
    {
        var portfolio = await db.Portfolios
            .AsNoTracking()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, ct)
            ?? throw new KeyNotFoundException($"Portfolio {portfolioId} not found.");

        var memberIds = portfolio.Members.Select(m => m.StrategyId).ToList();
        if (memberIds.Count == 0)
            return new PagedResult<PortfolioTradeDto>([], 0, page, pageSize);

        var nameById = (await db.Strategies.AsNoTracking()
                .Where(s => memberIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(ct))
            .ToDictionary(s => s.Id, s => s.Name);

        var query = db.StrategyTrades.AsNoTracking().Where(t => memberIds.Contains(t.StrategyId));
        query = status switch
        {
            TradeStatusFilter.Open => query.Where(t => t.IsOpen),
            TradeStatusFilter.Closed => query.Where(t => !t.IsOpen),
            _ => query,
        };

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(t => t.IsOpen)
            .ThenByDescending(t => t.CloseTime)
            .ThenByDescending(t => t.OpenTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(t => new PortfolioTradeDto(
            t.Id, t.StrategyId, nameById.GetValueOrDefault(t.StrategyId, "(unknown)"),
            t.Ticket, t.OpenTime, t.CloseTime, t.Type, t.Size, t.Item, t.OpenPrice, t.ClosePrice,
            t.StopLoss, t.TakeProfit, t.Commission, t.Taxes, t.Swap, t.Profit, t.CloseReason, t.IsOpen)).ToList();

        return new PagedResult<PortfolioTradeDto>(items, total, page, pageSize);
    }

    // -------------------------------------------------------------------------
    // Analytics (computed on demand)
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<PortfolioSummaryDto>> GetSummariesAsync(string? broker = null, CancellationToken ct = default)
    {
        var loaded = await LoadPortfoliosWithMemberInputsAsync(broker, ct);

        var results = new List<PortfolioSummaryDto>(loaded.Count);
        foreach (var (p, inputs) in loaded)
        {
            var kpis = PortfolioAnalyticsCalculator.Compute(p.InitialCapital, inputs);

            results.Add(new PortfolioSummaryDto(
                Id: p.Id,
                Name: p.Name,
                Broker: p.Broker,
                AccountType: p.AccountType,
                InitialCapital: p.InitialCapital,
                BaseCurrency: p.BaseCurrency,
                MemberCount: p.Members.Count,
                CreatedAt: p.CreatedAt,
                FinalEquity: kpis.FinalEquity,
                NetProfit: kpis.NetProfit,
                TotalReturn: kpis.TotalReturn,
                ReturnDrawdownRatio: kpis.ReturnDrawdownRatio,
                ProfitFactor: kpis.ProfitFactor,
                SharpeRatio: kpis.SharpeRatio,
                Cagr: kpis.Cagr,
                MaxDrawdownPercent: kpis.MaxDrawdownPercent,
                Sqn: kpis.Sqn,
                Exposure: kpis.Exposure,
                TradeCount: kpis.TradeCount,
                WinCount: kpis.WinCount,
                LossCount: kpis.LossCount,
                WinRate: kpis.WinRate,
                MonthlyAvgProfit: kpis.MonthlyAvgProfit,
                DailyAvgProfit: kpis.DailyAvgProfit));
        }

        return results;
    }

    public async Task<IReadOnlyList<PortfolioMonthlyReturnsDto>> GetMonthlyReturnsByBrokerAsync(string? broker = null, CancellationToken ct = default)
    {
        var loaded = await LoadPortfoliosWithMemberInputsAsync(broker, ct);

        return loaded
            .Select(x => new PortfolioMonthlyReturnsDto(
                PortfolioId: x.Portfolio.Id,
                Name: x.Portfolio.Name,
                MemberCount: x.Portfolio.Members.Count,
                Returns: PortfolioAnalyticsCalculator.ComputeMonthlyReturns(x.Portfolio.InitialCapital, x.Members)))
            .ToList();
    }

    public async Task<PortfolioAnalyticsDto> GetAnalyticsAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var (initialCapital, members) = await LoadMemberInputsAsync(portfolioId, ct);
        return PortfolioAnalyticsCalculator.Compute(initialCapital, members);
    }

    public async Task<IReadOnlyList<MonthlyReturnDto>> GetMonthlyReturnsAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var (initialCapital, members) = await LoadMemberInputsAsync(portfolioId, ct);
        return PortfolioAnalyticsCalculator.ComputeMonthlyReturns(initialCapital, members);
    }

    public async Task<IReadOnlyList<PortfolioEquityPointDto>> GetEquityCurveAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var (initialCapital, members) = await LoadMemberInputsAsync(portfolioId, ct);
        return PortfolioAnalyticsCalculator.ComputeEquityCurve(initialCapital, members);
    }

    public async Task<PortfolioRiskDto> GetRiskAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var (initialCapital, riskMembers) = await LoadMemberInputsAsync(portfolioId, ct);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(initialCapital, riskMembers);

        // Enrich each per-service VaR with the user-configured prop-firm guardrails (if any).
        var brokers = risk.ByService.Select(s => s.Service).Distinct().ToList();
        var limitsByBroker = (await db.BrokerRiskLimits
                .AsNoTracking()
                .Where(l => brokers.Contains(l.Broker))
                .ToListAsync(ct))
            .ToDictionary(l => l.Broker);

        var guardrails = risk.ByService.Select(s =>
        {
            limitsByBroker.TryGetValue(s.Service, out var lim);
            var dailyLimit = lim?.DailyLossLimitPct;
            return new ServiceGuardrailDto(
                Service: s.Service,
                FundingService: lim?.FundingService ?? FundingService.Other,
                Configured: lim is not null,
                Verified: lim?.Verified ?? false,
                DailyLossLimitPct: dailyLimit,
                MaxLossLimitPct: lim?.MaxLossLimitPct,
                ProfitTargetPct: lim?.ProfitTargetPct,
                DrawdownModel: lim?.DrawdownModel,
                ServiceVar95Percent: s.Var95Percent,
                DailyHeadroomPct: dailyLimit.HasValue ? dailyLimit.Value - s.Var95Percent : null,
                DailyBreached: dailyLimit.HasValue && s.Var95Percent > dailyLimit.Value);
        }).ToList();

        return risk with { Guardrails = guardrails };
    }

    public async Task<PortfolioCorrelationDto> GetCorrelationAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var (_, members) = await LoadMemberInputsAsync(portfolioId, ct);
        return PortfolioAnalyticsCalculator.ComputeCorrelation(members);
    }

    // -------------------------------------------------------------------------
    // Loading helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads every portfolio (optionally broker-filtered, ordered <c>CreatedAt DESC</c> like the grid)
    /// together with its calculator inputs. Strategy metadata and trades are each bulk-loaded in ONE
    /// query across ALL portfolios, so the query count stays constant regardless of how many exist.
    /// Shared by the summaries grid and the monthly-returns matrix.
    /// </summary>
    private async Task<List<(Portfolio Portfolio, List<PortfolioMemberInput> Members)>> LoadPortfoliosWithMemberInputsAsync(
        string? broker, CancellationToken ct)
    {
        var query = db.Portfolios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(broker))
            query = query.Where(p => p.Broker == broker);

        var portfolios = await query
            .OrderByDescending(p => p.CreatedAt)
            .Include(p => p.Members)
            .ToListAsync(ct);

        if (portfolios.Count == 0)
            return [];

        var allStrategyIds = portfolios
            .SelectMany(p => p.Members.Select(m => m.StrategyId))
            .Distinct()
            .ToList();

        // Strategy name + source broker (for per-service decomposition), via the account.
        var stratInfo = await (
            from s in db.Strategies.AsNoTracking()
            where allStrategyIds.Contains(s.Id)
            join a in db.TradingAccounts.AsNoTracking() on s.TradingAccountId equals a.Id into accs
            from a in accs.DefaultIfEmpty()
            select new { s.Id, s.Name, Broker = a != null ? a.Broker : null })
            .ToListAsync(ct);

        var infoMap = stratInfo.ToDictionary(s => s.Id);

        var tradesByStrategy = (await db.StrategyTrades
                .AsNoTracking()
                .Where(t => allStrategyIds.Contains(t.StrategyId))
                .ToListAsync(ct))
            .GroupBy(t => t.StrategyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<StrategyTrade>)g.ToList());

        return portfolios
            .Select(p => (
                Portfolio: p,
                Members: p.Members
                    .Select(m =>
                    {
                        infoMap.TryGetValue(m.StrategyId, out var info);
                        return new PortfolioMemberInput(
                            m.StrategyId,
                            info?.Name ?? "(unknown)",
                            m.Weight,
                            tradesByStrategy.TryGetValue(m.StrategyId, out var trades) ? trades : Array.Empty<StrategyTrade>(),
                            info?.Broker);
                    })
                    .ToList()))
            .ToList();
    }

    /// <summary>Loads a portfolio's baseline + member inputs, bulk-loading all member trades in ONE query.</summary>
    private async Task<(decimal InitialCapital, List<PortfolioMemberInput> Members)> LoadMemberInputsAsync(
        Guid portfolioId, CancellationToken ct)
    {
        var portfolio = await db.Portfolios
            .AsNoTracking()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, ct)
            ?? throw new KeyNotFoundException($"Portfolio {portfolioId} not found.");

        var memberIds = portfolio.Members.Select(m => m.StrategyId).ToList();
        if (memberIds.Count == 0)
            return (portfolio.InitialCapital, []);

        // Strategy name + source broker (for per-service risk decomposition), via the account.
        var stratInfo = await (
            from s in db.Strategies.AsNoTracking()
            where memberIds.Contains(s.Id)
            join a in db.TradingAccounts.AsNoTracking() on s.TradingAccountId equals a.Id into accs
            from a in accs.DefaultIfEmpty()
            select new { s.Id, s.Name, Broker = a != null ? a.Broker : null }).ToListAsync(ct);
        var infoMap = stratInfo.ToDictionary(s => s.Id);

        var tradesByStrategy = (await db.StrategyTrades
                .AsNoTracking()
                .Where(t => memberIds.Contains(t.StrategyId))
                .ToListAsync(ct))
            .GroupBy(t => t.StrategyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<StrategyTrade>)g.ToList());

        var inputs = portfolio.Members
            .Select(m =>
            {
                infoMap.TryGetValue(m.StrategyId, out var info);
                return new PortfolioMemberInput(
                    m.StrategyId,
                    info?.Name ?? "(unknown)",
                    m.Weight,
                    tradesByStrategy.TryGetValue(m.StrategyId, out var trades) ? trades : Array.Empty<StrategyTrade>(),
                    info?.Broker);
            })
            .ToList();

        return (portfolio.InitialCapital, inputs);
    }

    /// <summary>Maps portfolios to DTOs, enriching each member with its source account (name + broker).</summary>
    private async Task<List<PortfolioDto>> BuildDtosAsync(IReadOnlyList<Portfolio> portfolios, CancellationToken ct)
    {
        var strategyIds = portfolios.SelectMany(p => p.Members.Select(m => m.StrategyId)).Distinct().ToList();

        var stratMap = (await db.Strategies
                .AsNoTracking()
                .Where(s => strategyIds.Contains(s.Id))
                .Select(s => new StratInfo(s.Id, s.Name, s.TradingAccountId))
                .ToListAsync(ct))
            .ToDictionary(s => s.Id);

        var accountIds = stratMap.Values
            .Where(s => s.TradingAccountId.HasValue)
            .Select(s => s.TradingAccountId!.Value)
            .Distinct()
            .ToList();

        var accMap = (await db.TradingAccounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => new AccInfo(a.Id, a.Name, a.Broker))
                .ToListAsync(ct))
            .ToDictionary(a => a.Id);

        return portfolios.Select(p => ToDto(p, stratMap, accMap)).ToList();
    }

    private static PortfolioDto ToDto(
        Portfolio p,
        IReadOnlyDictionary<Guid, StratInfo> stratMap,
        IReadOnlyDictionary<Guid, AccInfo> accMap)
    {
        var members = p.Members.Select(m =>
        {
            stratMap.TryGetValue(m.StrategyId, out var strat);

            Guid? accountId = null;
            string? accountName = null;
            string? broker = null;
            if (strat?.TradingAccountId is Guid aid && accMap.TryGetValue(aid, out var acc))
            {
                accountId = acc.Id;
                accountName = acc.Name;
                broker = acc.Broker;
            }

            return new PortfolioMemberDto(
                m.StrategyId,
                strat?.Name ?? "(unknown)",
                accountId,
                accountName,
                broker,
                m.Weight);
        }).ToList();

        return new PortfolioDto(
            p.Id,
            p.Name,
            p.Description,
            p.Broker,
            p.AccountType,
            p.InitialCapital,
            p.BaseCurrency,
            members.Count,
            p.CreatedAt,
            members);
    }

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
}
