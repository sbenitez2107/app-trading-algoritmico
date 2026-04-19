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
  createdAt: string;
}

export type UpdateStrategyKpisDto = Partial<
  Omit<
    StrategyDto,
    | 'id'
    | 'name'
    | 'pseudocode'
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

  addToAccount(accountId: string, name: string, sqx: File, html: File): Observable<StrategyDto> {
    const form = new FormData();
    form.append('name', name);
    form.append('sqxFile', sqx);
    form.append('htmlFile', html);
    return this.http.post<StrategyDto>(
      `${this.apiUrl}/api/trading-accounts/${accountId}/strategies`,
      form,
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/strategies/${id}`);
  }

  getComments(id: string): Observable<StrategyCommentDto[]> {
    return this.http.get<StrategyCommentDto[]>(`${this.apiUrl}/api/strategies/${id}/comments`);
  }

  addComment(id: string, content: string): Observable<StrategyCommentDto> {
    const body: CreateStrategyCommentDto = { content };
    return this.http.post<StrategyCommentDto>(`${this.apiUrl}/api/strategies/${id}/comments`, body);
  }
}
