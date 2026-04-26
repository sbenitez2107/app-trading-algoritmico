import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface StrategyCommentDto {
  id: string;
  content: string;
  createdAt: string;
  createdBy: string | null;
}

export interface CreateStrategyCommentDto {
  content: string;
}

export interface MonthlyPerformanceDto {
  year: number;
  month: number;
  profit: number;
}

export interface StrategyDto {
  id: string;
  name: string;
  pseudocode: string | null;
  // Indicator metadata
  entryIndicators: string | null;
  priceIndicators: string | null;
  indicatorParameters: string | null;
  // Backtest metadata
  symbol: string | null;
  timeframe: string | null;
  backtestFrom: string | null;
  backtestTo: string | null;
  // Summary: top-left
  totalProfit: number | null;
  profitInPips: number | null;
  yearlyAvgProfit: number | null;
  yearlyAvgReturn: number | null;
  cagr: number | null;
  // Summary: grid
  numberOfTrades: number | null;
  sharpeRatio: number | null;
  profitFactor: number | null;
  returnDrawdownRatio: number | null;
  winningPercentage: number | null;
  drawdown: number | null;
  drawdownPercent: number | null;
  dailyAvgProfit: number | null;
  monthlyAvgProfit: number | null;
  averageTrade: number | null;
  annualReturnMaxDdRatio: number | null;
  rExpectancy: number | null;
  rExpectancyScore: number | null;
  strQualityNumber: number | null;
  sqnScore: number | null;
  // Stats: Strategy
  winsLossesRatio: number | null;
  payoutRatio: number | null;
  averageBarsInTrade: number | null;
  ahpr: number | null;
  zScore: number | null;
  zProbability: number | null;
  expectancy: number | null;
  deviation: number | null;
  exposure: number | null;
  stagnationInDays: number | null;
  stagnationPercent: number | null;
  // Stats: Trades
  numberOfWins: number | null;
  numberOfLosses: number | null;
  numberOfCancelled: number | null;
  grossProfit: number | null;
  grossLoss: number | null;
  averageWin: number | null;
  averageLoss: number | null;
  largestWin: number | null;
  largestLoss: number | null;
  maxConsecutiveWins: number | null;
  maxConsecutiveLosses: number | null;
  averageConsecutiveWins: number | null;
  averageConsecutiveLosses: number | null;
  averageBarsInWins: number | null;
  averageBarsInLosses: number | null;
  magicNumber: number | null;
  createdAt: string;
  // Live KPIs aggregated from imported MT4 trades. All `live*` fields are null
  // when the strategy has no trades yet — UI shows `—` in those cells.
  liveTradeCount: number;
  liveNetProfit: number | null;
  liveWinRate: number | null;
  liveProfitFactor: number | null;
  liveMaxDrawdownPercent: number | null;
  liveReturnDrawdownRatio: number | null;
  liveSharpeRatio: number | null;
  liveTotalReturn: number | null;
}

export type UpdateStrategyKpisDto = Partial<
  Omit<
    StrategyDto,
    | 'id'
    | 'name'
    | 'pseudocode'
    | 'entryIndicators'
    | 'priceIndicators'
    | 'indicatorParameters'
    | 'symbol'
    | 'timeframe'
    | 'backtestFrom'
    | 'backtestTo'
    | 'createdAt'
  >
>;

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StrategyAnalyticsDto {
  // Context
  initialBalance: number;
  firstTradeAt: string | null;
  lastTradeAt: string | null;
  daysSpanned: number;
  // Trade counts
  tradeCount: number;
  closedCount: number;
  openCount: number;
  winCount: number;
  lossCount: number;
  breakevenCount: number;
  // Money sums
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalTaxes: number;
  netProfit: number;
  grossProfit: number;
  grossLoss: number;
  // Per-trade aggregates
  averageTrade: number;
  averageWin: number;
  averageLoss: number;
  largestWin: number;
  largestLoss: number;
  standardDeviation: number;
  // Ratios
  winRate: number;
  profitFactor: number;
  payoutRatio: number;
  winsLossesRatio: number;
  expectancy: number;
  rExpectancy: number;
  // Streaks
  maxConsecutiveWins: number;
  maxConsecutiveLosses: number;
  averageConsecutiveWins: number;
  averageConsecutiveLosses: number;
  // Returns
  totalReturn: number;
  cagr: number;
  yearlyAvgProfit: number;
  monthlyAvgProfit: number;
  dailyAvgProfit: number;
  ahpr: number;
  // Drawdown / risk-adjusted
  maxDrawdownAmount: number;
  maxDrawdownPercent: number;
  returnDrawdownRatio: number;
  annualReturnMaxDdRatio: number;
  stagnationInDays: number;
  sharpeRatio: number;
  sqn: number;
  // Other
  exposure: number;
  zScore: number;
  zProbability: number;
}

export interface MonthlyReturnDto {
  year: number;
  month: number;
  equityStart: number;
  equityEnd: number;
  profit: number;
  /** Compounding return — `profit / equityStart` (decimal, e.g. 0.05 = 5%). */
  returnPercent: number;
  tradeCount: number;
}

export interface StrategyTradeSummaryDto {
  tradeCount: number;
  closedCount: number;
  winCount: number;
  lossCount: number;
  breakevenCount: number;
  /** 0..1 — undefined when there are no closed trades. */
  winRate: number;
  totalProfit: number;
  totalCommission: number;
  totalSwap: number;
  totalTaxes: number;
  /** totalProfit + totalCommission + totalSwap + totalTaxes — true cash impact. */
  netProfit: number;
}

export interface StrategyTradeDto {
  id: string;
  ticket: number;
  openTime: string;
  closeTime: string | null;
  type: string;
  size: number;
  item: string;
  openPrice: number;
  closePrice: number | null;
  stopLoss: number;
  takeProfit: number;
  commission: number;
  taxes: number;
  swap: number;
  profit: number;
  /** Raw MT4 close-reason suffix in uppercase: "SL", "TP", "TS", etc. Null for open trades. */
  closeReason: string | null;
  isOpen: boolean;
}

@Injectable({ providedIn: 'root' })
export class StrategyService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  getByStage(
    batchId: string,
    stageId: string,
    page = 1,
    pageSize = 50,
  ): Observable<PagedResult<StrategyDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<StrategyDto>>(
      `${this.apiUrl}/api/batches/${batchId}/stages/${stageId}/strategies`,
      { params },
    );
  }

  updateKpis(strategyId: string, dto: UpdateStrategyKpisDto): Observable<StrategyDto> {
    return this.http.patch<StrategyDto>(`${this.apiUrl}/api/strategies/${strategyId}`, dto);
  }

  getByAccount(accountId: string, page = 1, pageSize = 20): Observable<PagedResult<StrategyDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<StrategyDto>>(
      `${this.apiUrl}/api/trading-accounts/${accountId}/strategies`,
      { params },
    );
  }

  addToAccount(
    accountId: string,
    name: string,
    sqx: File,
    html: File,
    magicNumber: number | null = null,
  ): Observable<StrategyDto> {
    const form = new FormData();
    form.append('name', name);
    form.append('sqxFile', sqx);
    form.append('htmlFile', html);
    if (magicNumber !== null) {
      form.append('magicNumber', magicNumber.toString());
    }
    return this.http.post<StrategyDto>(
      `${this.apiUrl}/api/trading-accounts/${accountId}/strategies`,
      form,
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/strategies/${id}`);
  }

  assignMagicNumber(
    accountId: string,
    strategyId: string,
    magicNumber: number,
  ): Observable<StrategyDto> {
    return this.http.post<StrategyDto>(
      `${this.apiUrl}/api/trading-accounts/${accountId}/strategies/${strategyId}/magic-number`,
      { magicNumber },
    );
  }

  getTradesByStrategy(
    strategyId: string,
    status: 'open' | 'closed' | 'all' = 'all',
    page = 1,
    pageSize = 50,
  ): Observable<PagedResult<StrategyTradeDto>> {
    const params = new HttpParams()
      .set('status', status)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<StrategyTradeDto>>(
      `${this.apiUrl}/api/strategies/${strategyId}/trades`,
      { params },
    );
  }

  getTradesSummaryByStrategy(strategyId: string): Observable<StrategyTradeSummaryDto> {
    return this.http.get<StrategyTradeSummaryDto>(
      `${this.apiUrl}/api/strategies/${strategyId}/trades/summary`,
    );
  }

  getAnalyticsByStrategy(strategyId: string): Observable<StrategyAnalyticsDto> {
    return this.http.get<StrategyAnalyticsDto>(
      `${this.apiUrl}/api/strategies/${strategyId}/analytics`,
    );
  }

  getMonthlyReturnsByStrategy(strategyId: string): Observable<MonthlyReturnDto[]> {
    return this.http.get<MonthlyReturnDto[]>(
      `${this.apiUrl}/api/strategies/${strategyId}/monthly-returns`,
    );
  }

  getComments(id: string): Observable<StrategyCommentDto[]> {
    return this.http.get<StrategyCommentDto[]>(`${this.apiUrl}/api/strategies/${id}/comments`);
  }

  addComment(id: string, content: string): Observable<StrategyCommentDto> {
    const body: CreateStrategyCommentDto = { content };
    return this.http.post<StrategyCommentDto>(`${this.apiUrl}/api/strategies/${id}/comments`, body);
  }
}
