using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class StrategyService(
    AppDbContext db,
    ISqxParserService sqxParser,
    IHtmlReportParserService htmlParser) : IStrategyService
{
    public async Task<PagedResult<StrategyDto>> GetByStageAsync(
        Guid batchId, Guid stageId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.Strategies
            .AsNoTracking()
            .Where(x => x.BatchStageId == stageId && x.BatchStage!.BatchId == batchId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StrategyDto(
                x.Id, x.Name, x.Pseudocode,
                x.EntryIndicators, x.PriceIndicators, x.IndicatorParameters,
                x.Symbol, x.Timeframe, x.BacktestFrom, x.BacktestTo,
                x.TotalProfit, x.ProfitInPips, x.YearlyAvgProfit, x.YearlyAvgReturn, x.Cagr,
                x.NumberOfTrades, x.SharpeRatio, x.ProfitFactor, x.ReturnDrawdownRatio, x.WinningPercentage,
                x.Drawdown, x.DrawdownPercent, x.DailyAvgProfit, x.MonthlyAvgProfit, x.AverageTrade,
                x.AnnualReturnMaxDdRatio, x.RExpectancy, x.RExpectancyScore, x.StrQualityNumber, x.SqnScore,
                x.WinsLossesRatio, x.PayoutRatio, x.AverageBarsInTrade, x.Ahpr, x.ZScore, x.ZProbability,
                x.Expectancy, x.Deviation, x.Exposure, x.StagnationInDays, x.StagnationPercent,
                x.NumberOfWins, x.NumberOfLosses, x.NumberOfCancelled, x.GrossProfit, x.GrossLoss,
                x.AverageWin, x.AverageLoss, x.LargestWin, x.LargestLoss,
                x.MaxConsecutiveWins, x.MaxConsecutiveLosses, x.AverageConsecutiveWins, x.AverageConsecutiveLosses,
                x.AverageBarsInWins, x.AverageBarsInLosses,
                x.CreatedAt,
                x.MagicNumber,
                // Stage view does not surface live KPIs — strategies in the SQX pipeline
                // typically have no MT4 trades imported yet.
                0, null, null, null, null, null, null, null))
            .ToListAsync(ct);

        return new PagedResult<StrategyDto>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<StrategyDto>> GetByAccountAsync(
        Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var account = await db.TradingAccounts
            .AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.Id, a.InitialBalance })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"TradingAccount {accountId} not found.");

        var initialBalance = account.InitialBalance is decimal ib && ib > 0 ? ib : 100_000m;

        var query = db.Strategies
            .AsNoTracking()
            .Where(x => x.TradingAccountId == accountId);

        var totalCount = await query.CountAsync(ct);

        // Materialise the page of strategies first; we then load all their trades in
        // ONE query and run the in-memory analytics calculator per strategy.
        var pageStrategies = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var pageIds = pageStrategies.Select(s => s.Id).ToList();
        var tradesByStrategy = pageIds.Count == 0
            ? new Dictionary<Guid, List<Domain.Entities.StrategyTrade>>()
            : (await db.StrategyTrades
                .AsNoTracking()
                .Where(t => pageIds.Contains(t.StrategyId))
                .ToListAsync(ct))
              .GroupBy(t => t.StrategyId)
              .ToDictionary(g => g.Key, g => g.ToList());

        var items = pageStrategies
            .Select(x =>
            {
                var trades = tradesByStrategy.TryGetValue(x.Id, out var list) ? list : [];
                var live = trades.Count == 0
                    ? null
                    : StrategyAnalyticsCalculator.Compute(initialBalance, trades);

                return new StrategyDto(
                    x.Id, x.Name, x.Pseudocode,
                    x.EntryIndicators, x.PriceIndicators, x.IndicatorParameters,
                    x.Symbol, x.Timeframe, x.BacktestFrom, x.BacktestTo,
                    x.TotalProfit, x.ProfitInPips, x.YearlyAvgProfit, x.YearlyAvgReturn, x.Cagr,
                    x.NumberOfTrades, x.SharpeRatio, x.ProfitFactor, x.ReturnDrawdownRatio, x.WinningPercentage,
                    x.Drawdown, x.DrawdownPercent, x.DailyAvgProfit, x.MonthlyAvgProfit, x.AverageTrade,
                    x.AnnualReturnMaxDdRatio, x.RExpectancy, x.RExpectancyScore, x.StrQualityNumber, x.SqnScore,
                    x.WinsLossesRatio, x.PayoutRatio, x.AverageBarsInTrade, x.Ahpr, x.ZScore, x.ZProbability,
                    x.Expectancy, x.Deviation, x.Exposure, x.StagnationInDays, x.StagnationPercent,
                    x.NumberOfWins, x.NumberOfLosses, x.NumberOfCancelled, x.GrossProfit, x.GrossLoss,
                    x.AverageWin, x.AverageLoss, x.LargestWin, x.LargestLoss,
                    x.MaxConsecutiveWins, x.MaxConsecutiveLosses, x.AverageConsecutiveWins, x.AverageConsecutiveLosses,
                    x.AverageBarsInWins, x.AverageBarsInLosses,
                    x.CreatedAt,
                    x.MagicNumber,
                    LiveTradeCount: trades.Count,
                    LiveNetProfit: live?.NetProfit,
                    LiveWinRate: live?.WinRate,
                    LiveProfitFactor: live?.ProfitFactor,
                    LiveMaxDrawdownPercent: live?.MaxDrawdownPercent,
                    LiveReturnDrawdownRatio: live?.ReturnDrawdownRatio,
                    LiveSharpeRatio: live?.SharpeRatio,
                    LiveTotalReturn: live?.TotalReturn);
            })
            .ToList();

        return new PagedResult<StrategyDto>(items, totalCount, page, pageSize);
    }

    public async Task<StrategyDto> AddToAccountAsync(
        Guid accountId, string name, Stream sqxStream, Stream htmlStream, int? magicNumber = null, CancellationToken ct = default)
    {
        var accountExists = await db.TradingAccounts.AnyAsync(a => a.Id == accountId, ct);
        if (!accountExists)
            throw new KeyNotFoundException($"TradingAccount {accountId} not found.");

        var sqxMetadata = await sqxParser.ExtractStrategyMetadataAsync(sqxStream, ct);
        var report = await htmlParser.ParseAsync(htmlStream, ct)
            ?? throw new ArgumentException("Invalid SQX HTML report.");

        var entity = new Strategy
        {
            Name = name,
            Pseudocode = sqxMetadata?.Pseudocode,
            EntryIndicators = sqxMetadata?.EntryIndicators,
            PriceIndicators = sqxMetadata?.PriceIndicators,
            IndicatorParameters = sqxMetadata?.IndicatorParameters,
            TradingAccountId = accountId,
            BatchStageId = null,
            MagicNumber = magicNumber,
            Symbol = report.Symbol,
            Timeframe = report.Timeframe,
            BacktestFrom = report.BacktestFrom,
            BacktestTo = report.BacktestTo,
            CreatedAt = DateTime.UtcNow
        };

        StrategyKpiMapper.ApplyKpis(entity, report.Kpis);

        foreach (var mp in report.MonthlyPerformance)
        {
            entity.MonthlyPerformance.Add(new StrategyMonthlyPerformance
            {
                Year = mp.Year,
                Month = mp.Month,
                Profit = mp.Profit,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Strategies.Add(entity);
        await db.SaveChangesAsync(ct);

        return StrategyKpiMapper.ToDto(entity);
    }

    public async Task<StrategyDto> UpdateKpisAsync(Guid strategyId, UpdateStrategyKpisDto dto, CancellationToken ct = default)
    {
        var entity = await db.Strategies.FindAsync([strategyId], ct)
            ?? throw new KeyNotFoundException($"Strategy {strategyId} not found.");

        StrategyKpiMapper.ApplyKpis(entity, dto);

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return StrategyKpiMapper.ToDto(entity);
    }

    public async Task<StrategyDto> AssignMagicNumberAsync(
        Guid accountId, Guid strategyId, int magicNumber, CancellationToken ct = default)
    {
        var entity = await db.Strategies.FindAsync([strategyId], ct)
            ?? throw new KeyNotFoundException($"Strategy {strategyId} not found.");

        if (entity.TradingAccountId != accountId)
            throw new KeyNotFoundException(
                $"Strategy {strategyId} does not belong to TradingAccount {accountId}.");

        // Idempotent: same magic already assigned → no-op success
        if (entity.MagicNumber == magicNumber)
            return StrategyKpiMapper.ToDto(entity);

        // Anti-destructive: never overwrite an existing magic with a different one
        if (entity.MagicNumber is not null)
            throw new InvalidOperationException(
                $"Strategy already has magic number {entity.MagicNumber}. " +
                "Clear the existing magic number before assigning a new one.");

        // Conflict: another strategy in the same account already owns this magic
        var conflict = await db.Strategies
            .AnyAsync(s => s.TradingAccountId == accountId
                           && s.Id != strategyId
                           && s.MagicNumber == magicNumber, ct);
        if (conflict)
            throw new InvalidOperationException(
                $"Magic number {magicNumber} is already used by another strategy in this account.");

        entity.MagicNumber = magicNumber;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return StrategyKpiMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Strategies.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Strategy {id} not found.");

        db.Strategies.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<StrategyCommentDto>> GetCommentsAsync(Guid strategyId, CancellationToken ct = default)
    {
        var strategyExists = await db.Strategies.AnyAsync(s => s.Id == strategyId, ct);
        if (!strategyExists)
            throw new KeyNotFoundException($"Strategy {strategyId} not found.");

        var comments = await db.StrategyComments
            .AsNoTracking()
            .Where(c => c.StrategyId == strategyId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new StrategyCommentDto(c.Id, c.Content, c.CreatedAt, c.CreatedBy))
            .ToListAsync(ct);

        return comments;
    }

    public async Task<StrategyCommentDto> AddCommentAsync(
        Guid strategyId, string content, string? userId, CancellationToken ct = default)
    {
        var strategyExists = await db.Strategies.AnyAsync(s => s.Id == strategyId, ct);
        if (!strategyExists)
            throw new KeyNotFoundException($"Strategy {strategyId} not found.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty or whitespace.", nameof(content));

        var comment = new StrategyComment
        {
            StrategyId = strategyId,
            Content = content,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        db.StrategyComments.Add(comment);
        await db.SaveChangesAsync(ct);

        return new StrategyCommentDto(comment.Id, comment.Content, comment.CreatedAt, comment.CreatedBy);
    }
}
