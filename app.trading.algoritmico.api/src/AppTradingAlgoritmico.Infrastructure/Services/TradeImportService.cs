using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class TradeImportService(
    AppDbContext db,
    IMtStatementParserService parser) : ITradeImportService
{
    public async Task<TradeImportResultDto> ImportAsync(Guid accountId, Stream html, CancellationToken ct)
    {
        // 1. Load account — throws KeyNotFoundException when not found
        var account = await db.TradingAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null)
            throw new KeyNotFoundException($"TradingAccount {accountId} not found.");

        // 2. Parse HTML statement — throws ArgumentException when parser returns null
        var parsed = await parser.ParseAsync(html, ct);
        if (parsed is null)
            throw new ArgumentException("Could not parse MT4 statement.", nameof(html));

        // 3. Load all strategies for the account in a single query (N+1 prevention).
        // We need both the magic-indexed dictionary (for direct match) AND the full list
        // (for auto-assign by name when a magic is unknown).
        var accountStrategies = await db.Strategies
            .Where(s => s.TradingAccountId == accountId)
            .ToListAsync(ct);

        var strategiesByMagic = accountStrategies
            .Where(s => s.MagicNumber != null)
            .ToDictionary(s => s.MagicNumber!.Value);

        // 4. Split parsed trades into matched (known magic) vs orphan buckets.
        // Orphan bucket also tracks the underlying ParsedMtTradeDto list so we can
        // promote the bucket to "matched" if auto-assign succeeds.
        var matched = new List<(ParsedMtTradeDto Dto, Strategy Strategy)>();
        var orphanBuckets = new Dictionary<int, (string Hint, List<ParsedMtTradeDto> Trades)>();

        foreach (var t in parsed.Trades)
        {
            if (strategiesByMagic.TryGetValue(t.MagicNumber, out var strategy))
            {
                matched.Add((t, strategy));
            }
            else
            {
                if (orphanBuckets.TryGetValue(t.MagicNumber, out var bucket))
                    bucket.Trades.Add(t);
                else
                    orphanBuckets[t.MagicNumber] = (t.StrategyNameHint, new List<ParsedMtTradeDto> { t });
            }
        }

        // 4.5 Auto-assign by Strategy.Name: for each orphan magic, try to find a strategy
        // in the same account with Name matching the hint (case-insensitive, trimmed) AND
        // no MagicNumber assigned yet. Multiple matches → keep as orphan (ambiguous).
        // Existing magic on the candidate → keep as orphan (non-destructive — never overwrite).
        var autoAssigned = new List<AutoAssignedStrategyDto>();
        var resolvedOrphans = new List<int>();

        foreach (var (magic, bucket) in orphanBuckets)
        {
            var hint = bucket.Hint?.Trim();
            if (string.IsNullOrEmpty(hint))
                continue;

            var candidates = accountStrategies
                .Where(s => s.MagicNumber == null
                            && s.Name.Trim().Equals(hint, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count != 1)
                continue;

            var candidate = candidates[0];
            candidate.MagicNumber = magic;
            strategiesByMagic[magic] = candidate;

            foreach (var dto in bucket.Trades)
                matched.Add((dto, candidate));

            autoAssigned.Add(new AutoAssignedStrategyDto(
                StrategyId: candidate.Id,
                StrategyName: candidate.Name,
                MagicNumber: magic,
                TradeCount: bucket.Trades.Count));

            resolvedOrphans.Add(magic);
        }

        foreach (var magic in resolvedOrphans)
            orphanBuckets.Remove(magic);

        // 5. Pre-load existing rows to avoid N+1 on upsert
        var strategyIds = matched.Select(m => m.Strategy.Id).Distinct().ToList();
        var tickets = matched.Select(m => m.Dto.Ticket).Distinct().ToList();

        Dictionary<(Guid StrategyId, long Ticket), StrategyTrade> existing;
        if (strategyIds.Count > 0 && tickets.Count > 0)
        {
            existing = await db.StrategyTrades
                .Where(t => strategyIds.Contains(t.StrategyId) && tickets.Contains(t.Ticket))
                .ToDictionaryAsync(t => (t.StrategyId, t.Ticket), ct);
        }
        else
        {
            existing = new Dictionary<(Guid, long), StrategyTrade>();
        }

        // 6. Upsert matched trades
        int imported = 0, updated = 0;
        foreach (var (dto, strategy) in matched)
        {
            if (existing.TryGetValue((strategy.Id, dto.Ticket), out var entity))
            {
                // Update mutable fields — invariant fields (OpenTime, OpenPrice, SL, TP, Type, Size, Item) are not overwritten
                entity.CloseTime = dto.CloseTime;
                entity.ClosePrice = dto.ClosePrice;
                entity.CloseReason = dto.CloseReason;
                entity.Swap = dto.Swap;
                entity.Commission = dto.Commission;
                entity.Taxes = dto.Taxes;
                entity.Profit = dto.Profit;
                entity.IsOpen = dto.IsOpen;
                updated++;
            }
            else
            {
                db.StrategyTrades.Add(new StrategyTrade
                {
                    Id = Guid.NewGuid(),
                    StrategyId = strategy.Id,
                    Ticket = dto.Ticket,
                    OpenTime = dto.OpenTime,
                    CloseTime = dto.CloseTime,
                    Type = dto.Type,
                    Size = dto.Size,
                    Item = dto.Item,
                    OpenPrice = dto.OpenPrice,
                    ClosePrice = dto.ClosePrice,
                    StopLoss = dto.StopLoss,
                    TakeProfit = dto.TakeProfit,
                    Commission = dto.Commission,
                    Taxes = dto.Taxes,
                    Swap = dto.Swap,
                    Profit = dto.Profit,
                    CloseReason = dto.CloseReason,
                    IsOpen = dto.IsOpen,
                });
                imported++;
            }
        }

        // 7. Snapshot — always written once per import, never upserted
        // Currency fallback: prefer parser currency, fall back to account currency
        var currency = !string.IsNullOrWhiteSpace(parsed.Summary.Currency)
            ? parsed.Summary.Currency
            : (account.Currency ?? string.Empty);

        var snapshot = new AccountEquitySnapshot
        {
            Id = Guid.NewGuid(),
            TradingAccountId = accountId,
            ReportTime = parsed.Summary.ReportTime,
            Balance = parsed.Summary.Balance,
            Equity = parsed.Summary.Equity,
            FloatingPnL = parsed.Summary.FloatingPnL,
            Margin = parsed.Summary.Margin,
            FreeMargin = parsed.Summary.FreeMargin,
            ClosedTradePnL = parsed.Summary.ClosedTradePnL,
            Currency = currency,
        };
        db.AccountEquitySnapshots.Add(snapshot);

        // 8. Single SaveChanges — all writes in one transaction
        await db.SaveChangesAsync(ct);

        // 9. Build and return result DTO
        var orphans = orphanBuckets
            .Select(kv => new OrphanMagicNumberDto(kv.Key, kv.Value.Hint, kv.Value.Trades.Count))
            .ToList()
            .AsReadOnly();

        var snapshotDto = new SnapshotDto(
            ReportTime: snapshot.ReportTime,
            Balance: snapshot.Balance,
            Equity: snapshot.Equity,
            FloatingPnL: snapshot.FloatingPnL,
            Margin: snapshot.Margin,
            FreeMargin: snapshot.FreeMargin,
            ClosedTradePnL: snapshot.ClosedTradePnL,
            Currency: snapshot.Currency);

        var availableStrategies = accountStrategies
            .OrderBy(s => s.Name)
            .Select(s => new AvailableStrategyDto(s.Id, s.Name, s.MagicNumber))
            .ToList()
            .AsReadOnly();

        return new TradeImportResultDto(
            Imported: imported,
            Updated: updated,
            Skipped: 0,
            Orphans: orphans,
            AutoAssigned: autoAssigned.AsReadOnly(),
            AvailableStrategies: availableStrategies,
            Snapshot: snapshotDto);
    }

    public async Task<StrategyTradeSummaryDto> GetSummaryByStrategyAsync(
        Guid strategyId, CancellationToken ct)
    {
        // Single-query aggregate. EF translates conditional Sums into CASE WHEN.
        var stats = await db.StrategyTrades
            .AsNoTracking()
            .Where(t => t.StrategyId == strategyId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TradeCount = g.Count(),
                ClosedCount = g.Sum(t => !t.IsOpen ? 1 : 0),
                WinCount = g.Sum(t => !t.IsOpen && t.Profit > 0 ? 1 : 0),
                LossCount = g.Sum(t => !t.IsOpen && t.Profit < 0 ? 1 : 0),
                BreakevenCount = g.Sum(t => !t.IsOpen && t.Profit == 0 ? 1 : 0),
                TotalProfit = g.Sum(t => t.Profit),
                TotalCommission = g.Sum(t => t.Commission),
                TotalSwap = g.Sum(t => t.Swap),
                TotalTaxes = g.Sum(t => t.Taxes),
            })
            .FirstOrDefaultAsync(ct);

        if (stats is null)
        {
            return new StrategyTradeSummaryDto(
                TradeCount: 0,
                ClosedCount: 0,
                WinCount: 0,
                LossCount: 0,
                BreakevenCount: 0,
                WinRate: 0m,
                TotalProfit: 0m,
                TotalCommission: 0m,
                TotalSwap: 0m,
                TotalTaxes: 0m,
                NetProfit: 0m);
        }

        var winRate = stats.ClosedCount > 0
            ? (decimal)stats.WinCount / stats.ClosedCount
            : 0m;

        var netProfit = stats.TotalProfit
            + stats.TotalCommission
            + stats.TotalSwap
            + stats.TotalTaxes;

        return new StrategyTradeSummaryDto(
            TradeCount: stats.TradeCount,
            ClosedCount: stats.ClosedCount,
            WinCount: stats.WinCount,
            LossCount: stats.LossCount,
            BreakevenCount: stats.BreakevenCount,
            WinRate: winRate,
            TotalProfit: stats.TotalProfit,
            TotalCommission: stats.TotalCommission,
            TotalSwap: stats.TotalSwap,
            TotalTaxes: stats.TotalTaxes,
            NetProfit: netProfit);
    }

    public async Task<PagedResult<StrategyTradeDto>> GetByStrategyAsync(
        Guid strategyId,
        TradeStatusFilter status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.StrategyTrades
            .AsNoTracking()
            .Where(t => t.StrategyId == strategyId);

        query = status switch
        {
            TradeStatusFilter.Open => query.Where(t => t.IsOpen),
            TradeStatusFilter.Closed => query.Where(t => !t.IsOpen),
            _ => query,
        };

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.IsOpen)          // open first (true > false)
            .ThenByDescending(t => t.CloseTime)
            .ThenByDescending(t => t.OpenTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new StrategyTradeDto(
                t.Id,
                t.Ticket,
                t.OpenTime,
                t.CloseTime,
                t.Type,
                t.Size,
                t.Item,
                t.OpenPrice,
                t.ClosePrice,
                t.StopLoss,
                t.TakeProfit,
                t.Commission,
                t.Taxes,
                t.Swap,
                t.Profit,
                t.CloseReason,
                t.IsOpen))
            .ToListAsync(ct);

        return new PagedResult<StrategyTradeDto>(items, total, page, pageSize);
    }
}
