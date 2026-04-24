using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Infrastructure.Services;

internal static class StrategyKpiMapper
{
    public static void ApplyKpis(Strategy entity, UpdateStrategyKpisDto dto)
    {
        if (dto.TotalProfit.HasValue) entity.TotalProfit = dto.TotalProfit;
        if (dto.ProfitInPips.HasValue) entity.ProfitInPips = dto.ProfitInPips;
        if (dto.YearlyAvgProfit.HasValue) entity.YearlyAvgProfit = dto.YearlyAvgProfit;
        if (dto.YearlyAvgReturn.HasValue) entity.YearlyAvgReturn = dto.YearlyAvgReturn;
        if (dto.Cagr.HasValue) entity.Cagr = dto.Cagr;

        if (dto.NumberOfTrades.HasValue) entity.NumberOfTrades = dto.NumberOfTrades;
        if (dto.SharpeRatio.HasValue) entity.SharpeRatio = dto.SharpeRatio;
        if (dto.ProfitFactor.HasValue) entity.ProfitFactor = dto.ProfitFactor;
        if (dto.ReturnDrawdownRatio.HasValue) entity.ReturnDrawdownRatio = dto.ReturnDrawdownRatio;
        if (dto.WinningPercentage.HasValue) entity.WinningPercentage = dto.WinningPercentage;
        if (dto.Drawdown.HasValue) entity.Drawdown = dto.Drawdown;
        if (dto.DrawdownPercent.HasValue) entity.DrawdownPercent = dto.DrawdownPercent;
        if (dto.DailyAvgProfit.HasValue) entity.DailyAvgProfit = dto.DailyAvgProfit;
        if (dto.MonthlyAvgProfit.HasValue) entity.MonthlyAvgProfit = dto.MonthlyAvgProfit;
        if (dto.AverageTrade.HasValue) entity.AverageTrade = dto.AverageTrade;
        if (dto.AnnualReturnMaxDdRatio.HasValue) entity.AnnualReturnMaxDdRatio = dto.AnnualReturnMaxDdRatio;
        if (dto.RExpectancy.HasValue) entity.RExpectancy = dto.RExpectancy;
        if (dto.RExpectancyScore.HasValue) entity.RExpectancyScore = dto.RExpectancyScore;
        if (dto.StrQualityNumber.HasValue) entity.StrQualityNumber = dto.StrQualityNumber;
        if (dto.SqnScore.HasValue) entity.SqnScore = dto.SqnScore;

        if (dto.WinsLossesRatio.HasValue) entity.WinsLossesRatio = dto.WinsLossesRatio;
        if (dto.PayoutRatio.HasValue) entity.PayoutRatio = dto.PayoutRatio;
        if (dto.AverageBarsInTrade.HasValue) entity.AverageBarsInTrade = dto.AverageBarsInTrade;
        if (dto.Ahpr.HasValue) entity.Ahpr = dto.Ahpr;
        if (dto.ZScore.HasValue) entity.ZScore = dto.ZScore;
        if (dto.ZProbability.HasValue) entity.ZProbability = dto.ZProbability;
        if (dto.Expectancy.HasValue) entity.Expectancy = dto.Expectancy;
        if (dto.Deviation.HasValue) entity.Deviation = dto.Deviation;
        if (dto.Exposure.HasValue) entity.Exposure = dto.Exposure;
        if (dto.StagnationInDays.HasValue) entity.StagnationInDays = dto.StagnationInDays;
        if (dto.StagnationPercent.HasValue) entity.StagnationPercent = dto.StagnationPercent;

        if (dto.NumberOfWins.HasValue) entity.NumberOfWins = dto.NumberOfWins;
        if (dto.NumberOfLosses.HasValue) entity.NumberOfLosses = dto.NumberOfLosses;
        if (dto.NumberOfCancelled.HasValue) entity.NumberOfCancelled = dto.NumberOfCancelled;
        if (dto.GrossProfit.HasValue) entity.GrossProfit = dto.GrossProfit;
        if (dto.GrossLoss.HasValue) entity.GrossLoss = dto.GrossLoss;
        if (dto.AverageWin.HasValue) entity.AverageWin = dto.AverageWin;
        if (dto.AverageLoss.HasValue) entity.AverageLoss = dto.AverageLoss;
        if (dto.LargestWin.HasValue) entity.LargestWin = dto.LargestWin;
        if (dto.LargestLoss.HasValue) entity.LargestLoss = dto.LargestLoss;
        if (dto.MaxConsecutiveWins.HasValue) entity.MaxConsecutiveWins = dto.MaxConsecutiveWins;
        if (dto.MaxConsecutiveLosses.HasValue) entity.MaxConsecutiveLosses = dto.MaxConsecutiveLosses;
        if (dto.AverageConsecutiveWins.HasValue) entity.AverageConsecutiveWins = dto.AverageConsecutiveWins;
        if (dto.AverageConsecutiveLosses.HasValue) entity.AverageConsecutiveLosses = dto.AverageConsecutiveLosses;
        if (dto.AverageBarsInWins.HasValue) entity.AverageBarsInWins = dto.AverageBarsInWins;
        if (dto.AverageBarsInLosses.HasValue) entity.AverageBarsInLosses = dto.AverageBarsInLosses;
    }

    public static StrategyDto ToDto(Strategy e) => new(
        e.Id, e.Name, e.Pseudocode,
        e.EntryIndicators, e.PriceIndicators, e.IndicatorParameters,
        e.Symbol, e.Timeframe, e.BacktestFrom, e.BacktestTo,
        e.TotalProfit, e.ProfitInPips, e.YearlyAvgProfit, e.YearlyAvgReturn, e.Cagr,
        e.NumberOfTrades, e.SharpeRatio, e.ProfitFactor, e.ReturnDrawdownRatio, e.WinningPercentage,
        e.Drawdown, e.DrawdownPercent, e.DailyAvgProfit, e.MonthlyAvgProfit, e.AverageTrade,
        e.AnnualReturnMaxDdRatio, e.RExpectancy, e.RExpectancyScore, e.StrQualityNumber, e.SqnScore,
        e.WinsLossesRatio, e.PayoutRatio, e.AverageBarsInTrade, e.Ahpr, e.ZScore, e.ZProbability,
        e.Expectancy, e.Deviation, e.Exposure, e.StagnationInDays, e.StagnationPercent,
        e.NumberOfWins, e.NumberOfLosses, e.NumberOfCancelled, e.GrossProfit, e.GrossLoss,
        e.AverageWin, e.AverageLoss, e.LargestWin, e.LargestLoss,
        e.MaxConsecutiveWins, e.MaxConsecutiveLosses, e.AverageConsecutiveWins, e.AverageConsecutiveLosses,
        e.AverageBarsInWins, e.AverageBarsInLosses,
        e.CreatedAt,
        e.MagicNumber);
}
