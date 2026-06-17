import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export enum AccountType {
  Demo = 0,
  Live = 1,
}

export enum FundingService {
  Other = 0,
  Ftmo = 1,
  Axi = 2,
  DarwinexZero = 3,
}

export enum DrawdownModel {
  Static = 0,
  Trailing = 1,
}

export interface PortfolioMemberDto {
  strategyId: string;
  strategyName: string;
  accountId?: string;
  accountName?: string;
  broker?: string;
  weight: number;
}

export interface PortfolioDto {
  id: string;
  name: string;
  description?: string;
  broker: string;
  accountType: AccountType;
  initialCapital: number;
  baseCurrency: string;
  memberCount: number;
  createdAt: string;
  members: PortfolioMemberDto[];
}

export interface CreatePortfolioDto {
  name: string;
  description?: string;
  broker: string;
  accountType: AccountType;
  initialCapital: number;
  baseCurrency?: string;
  members?: AddPortfolioMemberDto[];
}

export interface UpdatePortfolioDto {
  name: string;
  description?: string;
  initialCapital: number;
  baseCurrency?: string;
}

export interface AddPortfolioMemberDto {
  strategyId: string;
  weight: number;
}

export interface PortfolioMemberContributionDto {
  strategyId: string;
  strategyName: string;
  rawWeight: number;
  normalizedWeight: number;
  tradeCount: number;
  netProfit: number;
  weightedNetProfit: number;
  contributionPercent: number;
}

export interface PortfolioAnalyticsDto {
  initialCapital: number;
  firstTradeAt?: string;
  lastTradeAt?: string;
  daysSpanned: number;
  memberCount: number;
  tradeCount: number;
  winCount: number;
  lossCount: number;
  breakevenCount: number;
  netProfit: number;
  grossProfit: number;
  grossLoss: number;
  averageTrade: number;
  averageWin: number;
  averageLoss: number;
  largestWin: number;
  largestLoss: number;
  standardDeviation: number;
  winRate: number;
  profitFactor: number;
  payoutRatio: number;
  winsLossesRatio: number;
  expectancy: number;
  rExpectancy: number;
  ahpr: number;
  maxConsecutiveWins: number;
  maxConsecutiveLosses: number;
  averageConsecutiveWins: number;
  averageConsecutiveLosses: number;
  totalReturn: number;
  cagr: number;
  dailyAvgProfit: number;
  monthlyAvgProfit: number;
  yearlyAvgProfit: number;
  maxDrawdownAmount: number;
  maxDrawdownPercent: number;
  returnDrawdownRatio: number;
  annualReturnMaxDdRatio: number;
  stagnationInDays: number;
  sharpeRatio: number;
  sqn: number;
  exposure: number;
  zScore: number;
  zProbability: number;
  finalEquity: number;
  members: PortfolioMemberContributionDto[];
  bySymbol: SymbolBreakdownDto[];
}

export interface SymbolBreakdownDto {
  symbol: string;
  netProfit: number;
  returnPercent: number;
  tradeCount: number;
}

export interface MonthlyReturnDto {
  year: number;
  month: number;
  equityStart: number;
  equityEnd: number;
  profit: number;
  returnPercent: number;
  tradeCount: number;
}

export interface PortfolioEquityPointDto {
  date: string;
  equity: number;
  drawdown: number;
  drawdownPercent: number;
}

export interface ServiceRiskDto {
  service: string;
  strategyCount: number;
  netProfit: number;
  var95: number;
  var95Percent: number;
}

export interface ServiceGuardrailDto {
  service: string;
  fundingService: FundingService;
  configured: boolean;
  verified: boolean;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel?: DrawdownModel;
  serviceVar95Percent: number;
  dailyHeadroomPct?: number;
  dailyBreached: boolean;
}

export interface PortfolioRiskDto {
  initialCapital: number;
  method: string;
  windowDays: number;
  observationDays: number;
  var95: number;
  var95Percent: number;
  var99: number;
  var99Percent: number;
  worstDay: number;
  bestDay: number;
  maxDrawdownPercent: number;
  byService: ServiceRiskDto[];
  guardrails: ServiceGuardrailDto[];
}

export interface PortfolioCorrelationDto {
  labels: string[];
  matrix: number[][];
  observationDays: number;
  averageCorrelation: number;
}

export interface BrokerRiskLimitsDto {
  id: string;
  broker: string;
  fundingService: FundingService;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel: DrawdownModel;
  verified: boolean;
}

export interface UpsertBrokerRiskLimitsDto {
  broker: string;
  fundingService: FundingService;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel: DrawdownModel;
  verified: boolean;
}

export interface StrategyCandidateDto {
  id: string;
  name: string;
  symbol?: string;
  timeframe?: string;
  magicNumber?: number;
  accountId: string;
  accountName: string;
  broker: string;
  // SQX (Backtest)
  totalProfit?: number;
  numberOfTrades?: number;
  sharpeRatio?: number;
  profitFactor?: number;
  winningPercentage?: number;
  drawdown?: number;
  // MT4 (Live)
  liveTradeCount: number;
  liveNetProfit?: number;
  liveTotalReturn?: number;
  liveWinRate?: number;
  liveProfitFactor?: number;
  liveMaxDrawdownPercent?: number;
  liveSharpeRatio?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class PortfolioService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly base = `${this.apiUrl}/api/portfolios`;

  getAll(broker: string, page = 1, pageSize = 50): Observable<PagedResult<PortfolioDto>> {
    const params = new HttpParams()
      .set('broker', broker)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<PortfolioDto>>(this.base, { params });
  }

  getById(id: string): Observable<PortfolioDto> {
    return this.http.get<PortfolioDto>(`${this.base}/${id}`);
  }

  create(dto: CreatePortfolioDto): Observable<PortfolioDto> {
    return this.http.post<PortfolioDto>(this.base, dto);
  }

  update(id: string, dto: UpdatePortfolioDto): Observable<PortfolioDto> {
    return this.http.put<PortfolioDto>(`${this.base}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  addMember(portfolioId: string, dto: AddPortfolioMemberDto): Observable<PortfolioDto> {
    return this.http.post<PortfolioDto>(`${this.base}/${portfolioId}/members`, dto);
  }

  updateMemberWeight(
    portfolioId: string,
    strategyId: string,
    weight: number,
  ): Observable<PortfolioDto> {
    return this.http.put<PortfolioDto>(`${this.base}/${portfolioId}/members/${strategyId}`, {
      weight,
    });
  }

  removeMember(portfolioId: string, strategyId: string): Observable<PortfolioDto> {
    return this.http.delete<PortfolioDto>(`${this.base}/${portfolioId}/members/${strategyId}`);
  }

  getAnalytics(portfolioId: string): Observable<PortfolioAnalyticsDto> {
    return this.http.get<PortfolioAnalyticsDto>(`${this.base}/${portfolioId}/analytics`);
  }

  getMonthlyReturns(portfolioId: string): Observable<MonthlyReturnDto[]> {
    return this.http.get<MonthlyReturnDto[]>(`${this.base}/${portfolioId}/monthly-returns`);
  }

  getEquityCurve(portfolioId: string): Observable<PortfolioEquityPointDto[]> {
    return this.http.get<PortfolioEquityPointDto[]>(`${this.base}/${portfolioId}/equity-curve`);
  }

  getRisk(portfolioId: string): Observable<PortfolioRiskDto> {
    return this.http.get<PortfolioRiskDto>(`${this.base}/${portfolioId}/risk`);
  }

  getCorrelation(portfolioId: string): Observable<PortfolioCorrelationDto> {
    return this.http.get<PortfolioCorrelationDto>(`${this.base}/${portfolioId}/correlation`);
  }

  getRiskLimits(): Observable<BrokerRiskLimitsDto[]> {
    return this.http.get<BrokerRiskLimitsDto[]>(`${this.apiUrl}/api/risk-limits`);
  }

  upsertRiskLimits(dto: UpsertBrokerRiskLimitsDto): Observable<BrokerRiskLimitsDto> {
    return this.http.put<BrokerRiskLimitsDto>(`${this.apiUrl}/api/risk-limits`, dto);
  }

  /** Strategies eligible to join a portfolio of the given broker + account type (Demo/Live). */
  getCandidates(broker: string, accountType: AccountType): Observable<StrategyCandidateDto[]> {
    const params = new HttpParams()
      .set('broker', broker)
      .set('accountType', accountType.toString());
    return this.http.get<StrategyCandidateDto[]>(`${this.apiUrl}/api/strategies/candidates`, {
      params,
    });
  }
}
