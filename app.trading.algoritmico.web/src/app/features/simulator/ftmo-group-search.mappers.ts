import {
  FtmoGroupSearchIneligibleReason,
  FtmoGroupSearchRequest,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStage,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from '../../core/models/ftmo-group-search.model';
import { FtmoGroupKindResultDto } from '../../core/models/ftmo-group-simulation.model';
import { FtmoChainOutcome } from '../../core/models/ftmo-simulation.model';
import { DEFAULT_GROUP_FORM } from './ftmo-group-simulation.mappers';

/**
 * Presentation helpers of the FTMO group search. Shares, headroom and the ceiling are FRACTIONS on the
 * wire; they are converted to percent ONCE, here, and compared on raw fractions.
 */

/** A fraction (0.045) to a percent (4.5). Rounded to 4 decimals to drop binary noise (0.07 -> 7). */
export function fractionToPercent(fraction: number): number {
  return Number((fraction * 100).toFixed(4));
}

/** A percent typed by the user (5) to the fraction the wire and the comparisons use (0.05). */
export function percentToFraction(percent: number): number {
  return Number((percent / 100).toFixed(6));
}

const BREACH_OUTCOMES: readonly FtmoChainOutcome[] = [
  FtmoChainOutcome.Phase1Breached,
  FtmoChainOutcome.Phase2Breached,
  FtmoChainOutcome.FundedBreached,
];

/**
 * The kind's breach share: (Phase1Breached + Phase2Breached + FundedBreached) / StartCount, a fraction.
 * `null` for a refused kind or an empty summary, never `0`.
 */
export function breachShare(kind: FtmoGroupKindResultDto): number | null {
  const summary = kind.run?.summary;
  if (!summary || summary.startCount <= 0) return null;
  const breached = summary.outcomes
    .filter((o) => BREACH_OUTCOMES.includes(o.outcome))
    .reduce((sum, o) => sum + o.count, 0);
  return breached / summary.startCount;
}

/**
 * Whether the WORSE kind's breach share is at or under the ceiling (both raw fractions). A row with no
 * kind, or with a kind that has no share (refused), is never within it.
 */
export function isWithinCeiling(row: FtmoGroupSearchRowDto, ceilingFraction: number): boolean {
  if (row.kinds.length === 0) return false;
  let worst = 0;
  for (const kind of row.kinds) {
    const share = breachShare(kind);
    if (share === null) return false;
    if (share > worst) worst = share;
  }
  return worst <= ceilingFraction;
}

/** A median that may be absent: `null` is explicit, a real `0` stays a value. */
export type MedianVm = { kind: 'absent' } | { kind: 'value'; value: number };

export function medianVm(value: number | null): MedianVm {
  return value === null ? { kind: 'absent' } : { kind: 'value', value };
}

/** An i18n key plus the raw wire value, which is non-null only when the value is not a known member. */
export interface EnumKey {
  key: string;
  raw: number | null;
}

function enumKey(prefix: string, value: number, known: ReadonlyMap<number, string>): EnumKey {
  const suffix = known.get(value);
  return suffix === undefined
    ? { key: `${prefix}.UNKNOWN`, raw: value }
    : { key: `${prefix}.${suffix}`, raw: null };
}

const STATUS_KEYS: ReadonlyMap<number, string> = new Map([
  [FtmoGroupSearchStatus.Running, 'RUNNING'],
  [FtmoGroupSearchStatus.Completed, 'COMPLETED'],
  [FtmoGroupSearchStatus.StoppedAtBudget, 'STOPPED_AT_BUDGET'],
  [FtmoGroupSearchStatus.Cancelled, 'CANCELLED'],
  [FtmoGroupSearchStatus.Failed, 'FAILED'],
]);

const STAGE_KEYS: ReadonlyMap<number, string> = new Map([
  [FtmoGroupSearchStage.Loading, 'LOADING'],
  [FtmoGroupSearchStage.Eligibility, 'ELIGIBILITY'],
  [FtmoGroupSearchStage.Proxy, 'PROXY'],
  [FtmoGroupSearchStage.Simulating, 'SIMULATING'],
  [FtmoGroupSearchStage.Ranking, 'RANKING'],
]);

const STOP_REASON_KEYS: ReadonlyMap<number, string> = new Map([
  [FtmoGroupSearchStopReason.None, 'NONE'],
  [FtmoGroupSearchStopReason.MaxFullSimulations, 'MAX_FULL_SIMULATIONS'],
  [FtmoGroupSearchStopReason.WallClock, 'WALL_CLOCK'],
]);

const INELIGIBLE_KEYS: ReadonlyMap<number, string> = new Map([
  [FtmoGroupSearchIneligibleReason.MissingKind, 'MISSING_KIND'],
  [FtmoGroupSearchIneligibleReason.SymbolRefused, 'SYMBOL_REFUSED'],
  [FtmoGroupSearchIneligibleReason.ZoneUnresolved, 'ZONE_UNRESOLVED'],
  [FtmoGroupSearchIneligibleReason.ProjectionRefused, 'PROJECTION_REFUSED'],
  [FtmoGroupSearchIneligibleReason.ProjectionRowless, 'PROJECTION_ROWLESS'],
  [FtmoGroupSearchIneligibleReason.IdenticalDeployEval, 'IDENTICAL_DEPLOY_EVAL'],
]);

/** Exact-value mapping: `Unknown` (0) and any unmapped value map to `UNKNOWN` carrying the raw number. */
export function statusKey(status: number): EnumKey {
  return enumKey('SIMULATOR.FTMO_SEARCH.STATUS', status, STATUS_KEYS);
}

export function stageKey(stage: number): EnumKey {
  return enumKey('SIMULATOR.FTMO_SEARCH.STAGE', stage, STAGE_KEYS);
}

export function stopReasonKey(reason: number): EnumKey {
  return enumKey('SIMULATOR.FTMO_SEARCH.STOP_REASON', reason, STOP_REASON_KEYS);
}

export function ineligibleReasonKey(reason: number): EnumKey {
  return enumKey('SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON', reason, INELIGIBLE_KEYS);
}

/** Smallest group the search builds. */
export const MIN_GROUP_SIZE = 2;
/** Mirrors `FtmoGroupSearchLimits.MaxFullSimulationsCeiling` / `MaxWallClockSecondsCeiling` (2b validation). */
export const MAX_FULL_SIMULATIONS_CEILING = 500;
export const MAX_WALL_CLOCK_SECONDS_CEILING = 3600;

/**
 * The editable fields of the search form. `null` is an empty input, `0` a real value. The ceiling is typed
 * as a PERCENT and converted to a fraction once, in `toSearchRequest`. Broker and the lot grid are not
 * editable: they come from the group page constants.
 */
export interface SearchFormValue {
  minMembers: number | null;
  maxMembers: number | null;
  maxPerInstrument: number | null;
  excludeIdentical: boolean;
  onePercentRule: boolean;
  ceilingPercent: number | null;
  initialCapital: number | null;
  targetRiskPerTrade: number | null;
  fxLow: number | null;
  fxHigh: number | null;
  maxFullSimulations: number | null;
  maxWallClockSeconds: number | null;
}

export const DEFAULT_SEARCH_FORM: SearchFormValue = {
  minMembers: 2,
  maxMembers: 4,
  maxPerInstrument: 2,
  excludeIdentical: true,
  onePercentRule: false,
  ceilingPercent: 5,
  initialCapital: 10000,
  targetRiskPerTrade: null,
  fxLow: null,
  fxHigh: null,
  maxFullSimulations: null,
  maxWallClockSeconds: null,
};

function present(value: number | null): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

function integerIn(value: number | null, min: number, max: number): boolean {
  return present(value) && Number.isInteger(value) && value >= min && value <= max;
}

/** Both bounds are integers in [2, maxMembers] and min <= max. `maxMembers` is the server's cap. */
export function sizeBoundsValid(form: SearchFormValue, maxMembers: number): boolean {
  return (
    integerIn(form.minMembers, MIN_GROUP_SIZE, maxMembers) &&
    integerIn(form.maxMembers, MIN_GROUP_SIZE, maxMembers) &&
    form.minMembers! <= form.maxMembers!
  );
}

/**
 * The FX pair, mirroring `FtmoGroupSearchController`: both empty, or both present with
 * `0 < fxLow <= fxHigh`. Explicit null checks: `0` is a value (an invalid low), not an empty input.
 */
export function fxBandValid(form: SearchFormValue): boolean {
  const lowEmpty = form.fxLow === null || form.fxLow === undefined;
  const highEmpty = form.fxHigh === null || form.fxHigh === undefined;
  if (lowEmpty && highEmpty) return true;
  return present(form.fxLow) && present(form.fxHigh) && form.fxLow > 0 && form.fxHigh >= form.fxLow;
}

/**
 * Start needs a valid risk and capital, valid bounds, no running job; optional values only when present.
 * The FX pair is validated only when the FX inputs are shown (hidden values are never sent).
 */
export function canStartSearch(
  form: SearchFormValue,
  maxMembers: number,
  running: boolean,
  showFx = false,
): boolean {
  return (
    !running &&
    (!showFx || fxBandValid(form)) &&
    present(form.targetRiskPerTrade) &&
    form.targetRiskPerTrade > 0 &&
    present(form.initialCapital) &&
    form.initialCapital > 0 &&
    sizeBoundsValid(form, maxMembers) &&
    present(form.maxPerInstrument) &&
    Number.isInteger(form.maxPerInstrument) &&
    form.maxPerInstrument >= 1 &&
    (form.ceilingPercent === null ||
      (present(form.ceilingPercent) && form.ceilingPercent > 0 && form.ceilingPercent <= 100)) &&
    (form.maxFullSimulations === null ||
      integerIn(form.maxFullSimulations, 1, MAX_FULL_SIMULATIONS_CEILING)) &&
    (form.maxWallClockSeconds === null ||
      integerIn(form.maxWallClockSeconds, 1, MAX_WALL_CLOCK_SECONDS_CEILING))
  );
}

/** The POST body, or null when the form cannot start. Hidden FX values are sent as `null`. */
export function toSearchRequest(
  accountId: string | null,
  form: SearchFormValue,
  maxMembers: number,
  showFx: boolean,
): FtmoGroupSearchRequest | null {
  if (accountId === null || !canStartSearch(form, maxMembers, false, showFx)) return null;
  return {
    tradingAccountId: accountId,
    minMembers: form.minMembers!,
    maxMembers: form.maxMembers!,
    maxPerInstrument: form.maxPerInstrument!,
    includeIdenticalDeployEval: !form.excludeIdentical,
    onePercentRule: form.onePercentRule,
    eliminationCeiling:
      form.ceilingPercent === null ? null : percentToFraction(form.ceilingPercent),
    broker: DEFAULT_GROUP_FORM.broker,
    initialCapital: form.initialCapital!,
    targetRiskPerTrade: form.targetRiskPerTrade!,
    fxLow: showFx ? form.fxLow : null,
    fxHigh: showFx ? form.fxHigh : null,
    sizeDecimals: DEFAULT_GROUP_FORM.sizeDecimals!,
    step: DEFAULT_GROUP_FORM.step!,
    minLot: DEFAULT_GROUP_FORM.minLot!,
    maxLots: DEFAULT_GROUP_FORM.maxLots!,
    maxFullSimulations: form.maxFullSimulations,
    maxWallClockSeconds: form.maxWallClockSeconds,
  };
}

/** Exact-value status checks: `Unknown` (0) is neither running nor completed, and it ends polling. */
export function isRunningStatus(status: number): boolean {
  return status === FtmoGroupSearchStatus.Running;
}

export function isTerminalStatus(status: number): boolean {
  return status !== FtmoGroupSearchStatus.Running;
}

/** processed / total as 0..100; `0` while the total is unknown. */
export function progressPercent(processed: number, total: number): number {
  if (total <= 0) return 0;
  return Math.min(100, Math.max(0, (processed * 100) / total));
}

/** `m:ss`, minutes unbounded. */
export function formatElapsed(elapsedMs: number): string {
  const totalSeconds = Math.max(0, Math.floor(elapsedMs / 1000));
  const seconds = totalSeconds % 60;
  return `${Math.floor(totalSeconds / 60)}:${seconds < 10 ? '0' : ''}${seconds}`;
}

/** The service yields either a `FTMO_SIMULATION` key (`ERRORS.*`) or a full `SIMULATOR.*` key. */
export function searchErrorKey(key: string): string {
  return key.startsWith('SIMULATOR.') ? key : `FTMO_SIMULATION.${key}`;
}

interface FxFlagged {
  deploy: { needsFxBand: boolean } | null;
  evaluation: { needsFxBand: boolean } | null;
}

/** Whether any strategy of the pool settles in a non-USD currency (either kind). */
export function poolNeedsFx(candidates: readonly FxFlagged[]): boolean {
  return candidates.some(
    (c) => (c.deploy?.needsFxBand ?? false) || (c.evaluation?.needsFxBand ?? false),
  );
}
