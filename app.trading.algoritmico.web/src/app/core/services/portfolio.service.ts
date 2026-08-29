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

/**
 * Discriminates how a broker's risk limits are modeled. `LossLimits` (FTMO/Axi/Other) keeps
 * today's breach-style fields; `VarTarget` (Darwinex Zero) models a monthly VaR-target rulebook
 * with NO breach semantics — missing the target rescales leverage, it does not breach the account.
 */
export enum GuardrailKind {
  LossLimits = 0,
  VarTarget = 1,
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
  /** Worst drawdown produced INSIDE the month — the peak resets on the 1st. 0 for an up-only month. */
  maxDrawdownPercent: number;
  /** Deepest distance below the ALL-TIME equity peak during the month (peak carried across months). */
  underwaterPercent: number;
  winCount: number;
  lossCount: number;
}

export interface PortfolioEquityPointDto {
  date: string;
  equity: number;
  drawdown: number;
  drawdownPercent: number;
}

/** One point on a member's contribution curve — cumulative WEIGHTED net P/L, not standalone equity. */
export interface PortfolioContributionPointDto {
  date: string;
  contribution: number;
}

/**
 * One member's contribution curve. Every net is scaled by the member's normalized portfolio
 * weight, so the contributions of all members sum to the combined curve's gain over initial
 * capital. This is NOT the strategy's standalone equity curve.
 */
export interface PortfolioMemberEquityCurveDto {
  strategyId: string;
  strategyName: string;
  /** RAW SQX-style size multiplier actually applied (1 = full size, 2 = double), not a share. */
  rawWeight: number;
  finalContribution: number;
  points: PortfolioContributionPointDto[];
}

export interface ServiceRiskDto {
  service: string;
  strategyCount: number;
  netProfit: number;
  var95: number;
  var95Percent: number;
  /** 30-calendar-day rolling-window VaR95 estimate — guardrail-agnostic, computed for every service. */
  monthlyVarInsufficientHistory: boolean;
  monthlyVarObservationDays: number;
  monthlyVarOverlappingWindows: number;
  monthlyVarIndependentWindows: number;
  monthlyVar95?: number;
  monthlyVar95Percent?: number;
}

/**
 * Monthly VaR-target readout for a `VarTarget` guardrail. Every field is either the user-sourced
 * band (targetVarPct/varFloorPct) or DERIVED analytics output — never guardrail configuration.
 * No breach/headroom fields (`funding-guardrails` spec).
 */
export interface VarTargetReadoutDto {
  targetVarPct?: number;
  varFloorPct?: number;
  /** DERIVED, not stored — echoes the calculator's 30-day horizon constant. */
  horizonDays: number;
  insufficientHistory: boolean;
  observationDays: number;
  overlappingWindows: number;
  independentWindows: number;
  monthlyVar95?: number;
  monthlyVar95Percent?: number;
  /** TargetVar / StrategyVar (KB §3). Undefined when the estimate is absent, insufficient, or zero. */
  impliedMultiplier?: number;
}

export interface LossLimitsGuardrailDto {
  service: string;
  fundingService: FundingService;
  kind: GuardrailKind.LossLimits;
  configured: boolean;
  verified: boolean;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel?: DrawdownModel;
  serviceVar95Percent: number;
  dailyHeadroomPct?: number;
  dailyBreached: boolean;
  varTarget: null;
}

export interface VarTargetGuardrailDto {
  service: string;
  fundingService: FundingService;
  kind: GuardrailKind.VarTarget;
  configured: boolean;
  verified: boolean;
  dailyLossLimitPct: null;
  maxLossLimitPct: null;
  profitTargetPct: null;
  drawdownModel: null;
  serviceVar95Percent: number;
  dailyHeadroomPct: null;
  dailyBreached: boolean;
  varTarget: VarTargetReadoutDto;
}

/** Discriminated union on `kind` — the field set is only valid for its own kind (backend-enforced). */
export type ServiceGuardrailDto = LossLimitsGuardrailDto | VarTargetGuardrailDto;

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
  kind: GuardrailKind;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel: DrawdownModel;
  targetVarPct?: number;
  varFloorPct?: number;
  verified: boolean;
}

export interface UpsertBrokerRiskLimitsDto {
  broker: string;
  fundingService: FundingService;
  kind: GuardrailKind;
  dailyLossLimitPct?: number;
  maxLossLimitPct?: number;
  profitTargetPct?: number;
  drawdownModel: DrawdownModel;
  targetVarPct?: number;
  varFloorPct?: number;
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

/**
 * A single trade combined across all member strategies of a portfolio.
 * Mirrors StrategyTradeDto plus the owning strategy's id + name so the grid
 * can show which strategy each trade belongs to.
 */
export interface PortfolioTradeDto {
  id: string;
  strategyId: string;
  strategyName: string;
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
  closeReason: string | null;
  isOpen: boolean;
}

/**
 * Flat summary row for the portfolios list grid.
 * Returned by GET /api/portfolios/summary?broker=<broker> as a plain array.
 * Fraction fields (totalReturn, winRate, cagr, maxDrawdownPercent, exposure)
 * are in [0..1] — multiply by 100 before displaying as percentages.
 */
export interface PortfolioSummaryDto {
  id: string;
  name: string;
  broker: string;
  accountType: AccountType;
  initialCapital: number;
  baseCurrency: string;
  memberCount: number;
  createdAt: string;
  finalEquity: number;
  netProfit: number;
  totalReturn: number; // fraction, e.g. 0.0677 = 6.77%
  returnDrawdownRatio: number;
  profitFactor: number;
  sharpeRatio: number;
  cagr: number; // fraction
  maxDrawdownPercent: number; // fraction
  sqn: number;
  exposure: number; // fraction
  tradeCount: number;
  winCount: number;
  lossCount: number;
  winRate: number; // fraction
  monthlyAvgProfit: number;
  dailyAvgProfit: number;
}

/**
 * Monthly returns of one portfolio, as returned by
 * GET /api/portfolios/monthly-returns?broker=<broker> for every portfolio at once.
 * `returnPercent` inside each entry is a fraction (0.0107 = 1.07%).
 */
export interface PortfolioMonthlyReturnsDto {
  portfolioId: string;
  name: string;
  memberCount: number;
  returns: MonthlyReturnDto[];
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

  /** Summary rows for the portfolios list grid — fuses header + analytics KPIs. */
  getSummaries(broker: string): Observable<PortfolioSummaryDto[]> {
    const params = new HttpParams().set('broker', broker);
    return this.http.get<PortfolioSummaryDto[]>(`${this.base}/summary`, { params });
  }

  /**
   * Monthly returns for every portfolio of the broker in one roundtrip.
   * Feeds both the monthly-returns matrix view and the per-row tooltip in the list.
   */
  getMonthlyReturnsByBroker(broker: string): Observable<PortfolioMonthlyReturnsDto[]> {
    const params = new HttpParams().set('broker', broker);
    return this.http.get<PortfolioMonthlyReturnsDto[]>(`${this.base}/monthly-returns`, { params });
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

  /** All trades combined across the portfolio's member strategies, paged + filterable by status. */
  getTradesByPortfolio(
    portfolioId: string,
    status: 'open' | 'closed' | 'all' = 'all',
    page = 1,
    pageSize = 50,
  ): Observable<PagedResult<PortfolioTradeDto>> {
    const params = new HttpParams()
      .set('status', status)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<PortfolioTradeDto>>(`${this.base}/${portfolioId}/trades`, {
      params,
    });
  }

  getMonthlyReturns(portfolioId: string): Observable<MonthlyReturnDto[]> {
    return this.http.get<MonthlyReturnDto[]>(`${this.base}/${portfolioId}/monthly-returns`);
  }

  getEquityCurve(portfolioId: string): Observable<PortfolioEquityPointDto[]> {
    return this.http.get<PortfolioEquityPointDto[]>(`${this.base}/${portfolioId}/equity-curve`);
  }

  getMemberEquityCurves(portfolioId: string): Observable<PortfolioMemberEquityCurveDto[]> {
    return this.http.get<PortfolioMemberEquityCurveDto[]>(
      `${this.base}/${portfolioId}/member-equity-curves`,
    );
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
