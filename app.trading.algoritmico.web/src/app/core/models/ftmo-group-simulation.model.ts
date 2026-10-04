import { BacktestRunKind } from '../services/backtest.service';
import {
  FtmoMultiStartRunDto,
  FtmoSimulationRefusal,
  FtmoSimulationStatus,
} from './ftmo-simulation.model';

/**
 * Mirrors `FtmoGroupCandidatesDto.cs` (ftmo-group-simulation B3, design D8) field for field. `Guid` and
 * `DateTime` travel as strings. Nullability follows the C# records exactly.
 */

/**
 * One held run's facts. `hasInstrumentSpec` is true only for a USABLE spec; `isCalibrated` is only
 * meaningful with one. `firstOpen`/`lastClose` are null for a run with no trades.
 */
export interface FtmoGroupCandidateRunDto {
  runId: string;
  symbol: string | null;
  tradeCount: number;
  firstOpen: string | null;
  lastClose: string | null;
  hasInstrumentSpec: boolean;
  isCalibrated: boolean;
  profitCurrency: string | null;
  needsFxBand: boolean;
  sourceTimeZoneId: string | null;
}

/** One strategy of the account; `deploy`/`evaluation` are null when no run of that kind is held. */
export interface FtmoGroupCandidateDto {
  strategyId: string;
  name: string;
  symbol: string | null;
  deploy: FtmoGroupCandidateRunDto | null;
  evaluation: FtmoGroupCandidateRunDto | null;
  nameExistsOnOtherAccount: boolean;
}

/** The picker's data for ONE account; `maxMembers` is the server's member cap. */
export interface FtmoGroupCandidatesDto {
  tradingAccountId: string;
  maxMembers: number;
  candidates: FtmoGroupCandidateDto[];
}

/**
 * Mirrors `Domain/Enums/FtmoGroupRefusal.cs` with EXPLICIT numbers copied from C# (declaration order).
 * `InvalidRequest` is `0`, the CLR default, so it must never be read as absent.
 */
export enum FtmoGroupRefusal {
  InvalidRequest = 0,
  SharedInputsRefused = 1,
  MemberNotFound = 2,
  MemberMissingKind = 3,
  MemberRunRefused = 4,
  MixedSourceTimeZones = 5,
  NoCommonWindow = 6,
  MemberHasNoTradesInWindow = 7,
}

/** Mirrors `FtmoGroupSimulationRequest`, the POST body. Missing scalars are sent as `null`, never dropped. */
export interface FtmoGroupSimulationRequest {
  memberStrategyIds: string[];
  broker: string;
  initialCapital: number;
  targetRiskPerTrade: number;
  fxLow: number | null;
  fxHigh: number | null;
  sizeDecimals: number;
  step: number;
  minLot: number;
  maxLots: number;
}

/** Mirrors `FtmoGroupMemberDto`. */
export interface FtmoGroupMemberDto {
  strategyId: string;
  name: string;
  memberOrder: number;
  profitCurrency: string | null;
  sourceTimeZoneId: string | null;
  fxLow: number | null;
  fxHigh: number | null;
}

/** Mirrors `FtmoGroupNameWarningDto`. */
export interface FtmoGroupNameWarningDto {
  name: string;
  strategyIds: string[];
}

/** Mirrors `FtmoGroupMemberRefusalDto`. `runReason` is non-null only when `reason` is `MemberRunRefused`. */
export interface FtmoGroupMemberRefusalDto {
  strategyId: string;
  name: string;
  reason: FtmoGroupRefusal;
  runReason: FtmoSimulationRefusal | null;
}

/** Mirrors `FtmoGroupWindowDto`. */
export interface FtmoGroupWindowDto {
  start: string;
  end: string;
}

/** Mirrors `FtmoGroupMemberCoverageDto`. */
export interface FtmoGroupMemberCoverageDto {
  strategyId: string;
  name: string;
  firstOpen: string | null;
  lastClose: string | null;
  inWindowTrades: number;
}

/** Mirrors `FtmoGroupMemberContributionDto` (`FtmoGroupDiagnosticsDto.cs`). */
export interface FtmoGroupMemberContributionDto {
  strategyId: string;
  name: string;
  inWindowTrades: number;
  scalableTrades: number;
  netLow: number;
  netHigh: number;
  raisedToMinimum: number;
  cappedAtMaximum: number;
  unscalable: number;
}

/** Mirrors `FtmoGroupMemberAttributionDto`. */
export interface FtmoGroupMemberAttributionDto {
  strategyId: string;
  name: string;
  phase1Starts: number;
  phase2Starts: number;
  fundedStarts: number;
  soleContributorStarts: number;
  sharedCloseStarts: number;
}

/** Mirrors `FtmoGroupAttributionDto`. */
export interface FtmoGroupAttributionDto {
  decidingBreachStarts: number;
  sharedCloseStarts: number;
  unattributedStarts: number;
  members: FtmoGroupMemberAttributionDto[];
}

/** Mirrors `FtmoGroupPeakConcurrencyDto`. */
export interface FtmoGroupPeakConcurrencyDto {
  peakConcurrentOpen: number;
  firstReachedSource: string | null;
  memberIdsAtPeak: string[];
}

/** Mirrors `FtmoGroupDiagnosticsDto`. Present only on a successful kind. */
export interface FtmoGroupDiagnosticsDto {
  contributions: FtmoGroupMemberContributionDto[];
  attribution: FtmoGroupAttributionDto;
  peak: FtmoGroupPeakConcurrencyDto;
}

/** Mirrors `FtmoGroupKindResultDto` including the non-positional `Diagnostics` property. */
export interface FtmoGroupKindResultDto {
  kind: BacktestRunKind;
  status: FtmoSimulationStatus;
  refusal: FtmoGroupRefusal | null;
  memberRefusals: FtmoGroupMemberRefusalDto[];
  window: FtmoGroupWindowDto | null;
  coverage: FtmoGroupMemberCoverageDto[];
  run: FtmoMultiStartRunDto | null;
  diagnostics: FtmoGroupDiagnosticsDto | null;
}

/** Mirrors `FtmoGroupSimulationDto`, the response envelope. */
export interface FtmoGroupSimulationDto {
  status: FtmoSimulationStatus;
  refusal: FtmoGroupRefusal | null;
  sharedRefusal: FtmoSimulationRefusal | null;
  dailyLossLimitPct: number | null;
  maxLossLimitPct: number | null;
  members: FtmoGroupMemberDto[];
  duplicateIdsRemoved: string[];
  duplicateNameWarnings: FtmoGroupNameWarningDto[];
  unknownStrategyIds: string[];
  kinds: FtmoGroupKindResultDto[];
  disclosures: string[];
}
