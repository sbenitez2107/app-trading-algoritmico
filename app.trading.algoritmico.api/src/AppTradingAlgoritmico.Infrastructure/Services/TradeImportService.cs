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

        // 3. Load strategies with magic numbers in a single query (N+1 prevention)
        var strategiesByMagic = await db.Strategies
            .Where(s => s.TradingAccountId == accountId && s.MagicNumber != null)
            .ToDictionaryAsync(s => s.MagicNumber!.Value, ct);

        // 4. Split parsed trades into matched (known magic) vs orphan buckets
        var matched = new List<(ParsedMtTradeDto Dto, Strategy Strategy)>();
        var orphanBuckets = new Dictionary<int, (string Hint, int Count)>();

        foreach (var t in parsed.Trades)
        {
            if (strategiesByMagic.TryGetValue(t.MagicNumber, out var strategy))
            {
                matched.Add((t, strategy));
            }
            else
            {
                if (orphanBuckets.TryGetValue(t.MagicNumber, out var bucket))
                    orphanBuckets[t.MagicNumber] = (bucket.Hint, bucket.Count + 1);
                else
                    orphanBuckets[t.MagicNumber] = (t.StrategyNameHint, 1);
            }
        }

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
            .Select(kv => new OrphanMagicNumberDto(kv.Key, kv.Value.Hint, kv.Value.Count))
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

        return new TradeImportResultDto(
            Imported: imported,
            Updated: updated,
            Skipped: 0,
            Orphans: orphans,
            Snapshot: snapshotDto);
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
