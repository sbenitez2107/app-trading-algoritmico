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
