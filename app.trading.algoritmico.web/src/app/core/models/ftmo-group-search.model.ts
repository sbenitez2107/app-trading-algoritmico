import { BacktestRunKind } from '../services/backtest.service';
import { FtmoGroupKindResultDto } from './ftmo-group-simulation.model';
import { FtmoSimulationRefusal } from './ftmo-simulation.model';

/**
 * Mirrors `FtmoGroupSearchDto.cs` (ftmo-group-search D7) field for field. `Guid` travels as a string.
 * Shares, headroom and the elimination ceiling are FRACTIONS on the wire (0.8 = 80%); they are
 * converted to percent once, in the mappers.
 */

/** Job status. `Unknown` is `0`, the CLR default: it must never read as `Completed`. */
export enum FtmoGroupSearchStatus {
  Unknown = 0,
  Running = 1,
  Completed = 2,
  StoppedAtBudget = 3,
  Cancelled = 4,
  Failed = 5,
}

/** The stage a running job reports. `Unknown` is `0`. */
export enum FtmoGroupSearchStage {
  Unknown = 0,
  Loading = 1,
  Eligibility = 2,
  Proxy = 3,
  Simulating = 4,
  Ranking = 5,
}

/** Which budget ended the full computation early. A cancel is NOT a stop reason. `Unknown` is `0`. */
export enum FtmoGroupSearchStopReason {
  Unknown = 0,
  None = 1,
  MaxFullSimulations = 2,
  WallClock = 3,
}

/** Why a strategy never entered the funnel. `Unknown` is `0`, never a real reason. */
export enum FtmoGroupSearchIneligibleReason {
  Unknown = 0,
  MissingKind = 1,
  SymbolRefused = 2,
  ZoneUnresolved = 3,
  ProjectionRefused = 4,
  ProjectionRowless = 5,
  IdenticalDeployEval = 6,
}

/**
 * Mirrors `FtmoGroupSearchRequest`, the POST body. Every backend scalar is nullable; the client always
 * sends every key, `null` where a value is absent, so `sizeDecimals = 0` is never confused with a missing
 * field.
 */
export interface FtmoGroupSearchRequest {
  tradingAccountId: string;
  minMembers: number;
  maxMembers: number;
  maxPerInstrument: number;
  includeIdenticalDeployEval: boolean;
  onePercentRule: boolean;
  eliminationCeiling: number | null;
  broker: string;
  initialCapital: number;
  targetRiskPerTrade: number;
  fxLow: number | null;
  fxHigh: number | null;
  sizeDecimals: number;
  step: number;
  minLot: number;
  maxLots: number;
  maxFullSimulations: number | null;
  maxWallClockSeconds: number | null;
}

/** Candidate counts through the funnel; `examined` is every candidate enumerated BEFORE any filtering. */
export interface FtmoGroupSearchFunnelDto {
  examined: number;
  removedByCap: number;
  removedByPairConflict: number;
  removedByOnePercentRule: number;
  removedNoCommonWindow: number;
  removedMemberHasNoTrades: number;
  shortlisted: number;
}

/** An immutable snapshot of a running job. */
export interface FtmoGroupSearchProgressDto {
  stage: FtmoGroupSearchStage;
  processed: number;
  total: number;
  elapsedMs: number;
  fullSimulationsDone: number;
  maxFullSimulations: number;
  funnel: FtmoGroupSearchFunnelDto;
}

export interface FtmoGroupSearchIneligibleDto {
  strategyId: string;
  name: string;
  reason: FtmoGroupSearchIneligibleReason;
  refusal: FtmoSimulationRefusal | null;
}

/** One kind's own limit usage, unblended; fractions of the allowance (0.8 = 80% used). */
export interface FtmoGroupSearchKindHeadroomDto {
  kind: BacktestRunKind;
  worstDailyUsed: number;
  worstMaxUsed: number;
  medianMaxUsed: number;
  headroom: number;
}

/** One ranked group. `withinCeiling` is a highlight, never a ranking key. */
export interface FtmoGroupSearchRowDto {
  rank: number;
  memberIds: string[];
  memberNames: string[];
  peakConcurrentOpen: number;
  identicalDeployEval: boolean;
  withinCeiling: boolean;
  headroom: number;
  kindHeadrooms: FtmoGroupSearchKindHeadroomDto[];
  kinds: FtmoGroupKindResultDto[];
  symbols: string[] | null;
}

/** One job in effect. `disclosures` is always present, including while running. */
export interface FtmoGroupSearchJobDto {
  jobId: string;
  status: FtmoGroupSearchStatus;
  progress: FtmoGroupSearchProgressDto;
  stopReason: FtmoGroupSearchStopReason;
  notComputed: number;
  ineligible: FtmoGroupSearchIneligibleDto[];
  rows: FtmoGroupSearchRowDto[];
  disclosures: string[];
  errorMessage: string | null;
  /** The request exactly as submitted (fractions stay fractions); a re-attached page rebuilds its link from it. */
  request: FtmoGroupSearchRequest;
}

/** The 202 body of `POST group-search`: the id plus the initial snapshot. */
export interface FtmoGroupSearchStartResponseDto {
  jobId: string;
  job: FtmoGroupSearchJobDto;
}
