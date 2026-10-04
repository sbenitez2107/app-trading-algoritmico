import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidateRunDto,
  FtmoGroupSimulationRequest,
} from '../../core/models/ftmo-group-simulation.model';
import { IMOX_RETESTER_LOT_GRID } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { FTMO_DEFAULT_BROKER } from '../broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component';

/** The account the screen opens on when it exists (design D9). Matched by name, case-insensitive. */
export const DEFAULT_ACCOUNT_NAME = 'SBDEMO2';

export interface CandidateFilter {
  symbol: string | null;
  search: string;
}

/** `na` = not applicable (no usable spec, so calibration is not meaningful). */
export type SpecFlag = 'yes' | 'no' | 'absent';
export type CalibrationFlag = 'yes' | 'no' | 'na' | 'absent';

/** One kind (Deploy or Evaluation) of a picker row. An absent kind is explicit, never blank or `0`. */
export interface CandidateKindVm {
  present: boolean;
  from: string | null;
  to: string | null;
  tradeCount: number | null;
  spec: SpecFlag;
  calibration: CalibrationFlag;
  needsFxBand: boolean;
}

export interface CandidateRowVm {
  strategyId: string;
  name: string;
  symbol: string | null;
  deploy: CandidateKindVm;
  evaluation: CandidateKindVm;
  needsFxBand: boolean;
  nameExistsOnOtherAccount: boolean;
}

/** Symbol is an exact match; the name search is a case-insensitive, trimmed substring. Both must hold. */
export function filterCandidates(
  candidates: readonly FtmoGroupCandidateDto[],
  filter: CandidateFilter,
): FtmoGroupCandidateDto[] {
  const needle = filter.search.trim().toLowerCase();
  return candidates.filter(
    (c) =>
      (filter.symbol === null || c.symbol === filter.symbol) &&
      (needle === '' || c.name.toLowerCase().includes(needle)),
  );
}

export function distinctSymbols(candidates: readonly FtmoGroupCandidateDto[]): string[] {
  const symbols = new Set<string>();
  for (const c of candidates) {
    if (c.symbol !== null) symbols.add(c.symbol);
  }
  return [...symbols].sort();
}

/**
 * A new row may be selected only while fewer than `maxMembers` are selected. A row that is already
 * selected can always be deselected. `maxMembers` is the server's cap, never a constant here.
 */
export function canSelect(
  selected: ReadonlySet<string>,
  strategyId: string,
  maxMembers: number,
): boolean {
  return selected.has(strategyId) || selected.size < maxMembers;
}

/** Toggles a row, ignoring an add beyond the cap. Returns a new set. */
export function toggleSelection(
  selected: ReadonlySet<string>,
  strategyId: string,
  maxMembers: number,
): ReadonlySet<string> {
  const next = new Set(selected);
  if (next.has(strategyId)) {
    next.delete(strategyId);
  } else if (canSelect(selected, strategyId, maxMembers)) {
    next.add(strategyId);
  }
  return next;
}

/** `yyyy-MM-dd` from an ISO date-time string, or null when the run has no trades. */
function dateOnly(value: string | null): string | null {
  return value === null ? null : value.slice(0, 10);
}

const ABSENT_KIND: CandidateKindVm = {
  present: false,
  from: null,
  to: null,
  tradeCount: null,
  spec: 'absent',
  calibration: 'absent',
  needsFxBand: false,
};

function toKindVm(run: FtmoGroupCandidateRunDto | null): CandidateKindVm {
  if (run === null) return ABSENT_KIND;
  return {
    present: true,
    from: dateOnly(run.firstOpen),
    to: dateOnly(run.lastClose),
    tradeCount: run.tradeCount,
    spec: run.hasInstrumentSpec ? 'yes' : 'no',
    calibration: !run.hasInstrumentSpec ? 'na' : run.isCalibrated ? 'yes' : 'no',
    needsFxBand: run.needsFxBand,
  };
}

export function toCandidateRowVm(c: FtmoGroupCandidateDto): CandidateRowVm {
  const deploy = toKindVm(c.deploy);
  const evaluation = toKindVm(c.evaluation);
  return {
    strategyId: c.strategyId,
    name: c.name,
    symbol: c.symbol,
    deploy,
    evaluation,
    needsFxBand: deploy.needsFxBand || evaluation.needsFxBand,
    nameExistsOnOtherAccount: c.nameExistsOnOtherAccount,
  };
}

/** The account named `SBDEMO2` when it exists, otherwise the first; null when there are none. */
export function pickDefaultAccountId(
  accounts: readonly { id: string; name: string }[],
): string | null {
  const preferred = accounts.find(
    (a) => a.name.trim().toLowerCase() === DEFAULT_ACCOUNT_NAME.toLowerCase(),
  );
  return (preferred ?? accounts[0])?.id ?? null;
}

/** The editable fields of the parameter form. `null` is an empty input; `0` is a legal value. */
export interface GroupFormValue {
  broker: string;
  initialCapital: number | null;
  targetRiskPerTrade: number | null;
  sizeDecimals: number | null;
  step: number | null;
  minLot: number | null;
  maxLots: number | null;
  fxLow: number | null;
  fxHigh: number | null;
}

/** Prefills (spec "The Parameter Form And Its Prefills"): the risk stays empty on purpose. */
export const DEFAULT_GROUP_FORM: GroupFormValue = {
  broker: FTMO_DEFAULT_BROKER,
  initialCapital: 10000,
  targetRiskPerTrade: null,
  sizeDecimals: IMOX_RETESTER_LOT_GRID.sizeDecimals,
  step: IMOX_RETESTER_LOT_GRID.step,
  minLot: IMOX_RETESTER_LOT_GRID.minLot,
  maxLots: IMOX_RETESTER_LOT_GRID.maxLots,
  fxLow: null,
  fxHigh: null,
};

function isFinitePresent(value: number | null): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

/** Present and strictly positive. Never a truthiness check, so `0` is judged by its value. */
function isPositive(value: number | null): value is number {
  return isFinitePresent(value) && value > 0;
}

/**
 * Run is possible with at least one member, no run in flight and every required value usable. The FX band is
 * NOT required: a missing band is the backend's own `FxRateNotDeclared` refusal. `sizeDecimals = 0` is legal.
 */
export function canRunGroup(form: GroupFormValue, memberCount: number, running: boolean): boolean {
  return (
    !running &&
    memberCount > 0 &&
    form.broker.trim() !== '' &&
    isPositive(form.initialCapital) &&
    isPositive(form.targetRiskPerTrade) &&
    isFinitePresent(form.sizeDecimals) &&
    form.sizeDecimals >= 0 &&
    isPositive(form.step) &&
    isPositive(form.minLot) &&
    isPositive(form.maxLots)
  );
}

/** FX inputs show only while a selected member's held run settles in a non-USD currency (either kind). */
export function needsFxInputs(
  candidates: readonly FtmoGroupCandidateDto[],
  selected: ReadonlySet<string>,
): boolean {
  return candidates.some(
    (c) =>
      selected.has(c.strategyId) &&
      ((c.deploy?.needsFxBand ?? false) || (c.evaluation?.needsFxBand ?? false)),
  );
}

/** The POST body, or null when the form cannot run. Hidden FX values are sent as `null`. */
export function toGroupRequest(
  form: GroupFormValue,
  memberIds: readonly string[],
  showFx: boolean,
): FtmoGroupSimulationRequest | null {
  const ids = [...new Set(memberIds)];
  if (!canRunGroup(form, ids.length, false)) return null;
  return {
    memberStrategyIds: ids,
    broker: form.broker,
    initialCapital: form.initialCapital!,
    targetRiskPerTrade: form.targetRiskPerTrade!,
    fxLow: showFx ? form.fxLow : null,
    fxHigh: showFx ? form.fxHigh : null,
    sizeDecimals: form.sizeDecimals!,
    step: form.step!,
    minLot: form.minLot!,
    maxLots: form.maxLots!,
  };
}

/**
 * Units (F3a RELIABILITY-001): the backend's `dailyLossLimitPct` is a FRACTION in (0, 1] (`0.05` = 5%), the
 * same unit as `FtmoSimulationInputs`. It is converted to a percent exactly once, in `toWorstCaseReadout`;
 * every field of the readout view model (`pct`, `academyPct`, `dailyLimitPct`) is a percent on a 0-100 scale.
 */
/** The academy group criterion, as a percent. */
export const ACADEMY_GROUP_CRITERION_PCT = 1;
/** The FTMO daily loss limit used before the first run echoes its own, as a FRACTION like the DTO. */
export const FTMO_DEFAULT_DAILY_LOSS_LIMIT_FRACTION = 0.05;

/** A fraction as a percent, rounded so that `0.07` reads `7` and not `7.000000000000001`. */
function fractionToPct(fraction: number): number {
  return Number((fraction * 100).toFixed(6));
}

export interface ReadoutLine {
  amount: number;
  pct: number;
  amountText: string;
  pctText: string;
  exceedsAcademy: boolean;
  exceedsDaily: boolean;
}

export interface ObservedPeakVm {
  kind: BacktestRunKind;
  peak: number;
  line: ReadoutLine;
}

export interface WorstCaseReadoutVm {
  memberCount: number;
  worst: ReadoutLine;
  academyPct: number;
  dailyLimitPct: number;
  observed: ObservedPeakVm[];
}

function readoutLine(
  amount: number,
  capital: number,
  academyPct: number,
  dailyPct: number,
): ReadoutLine {
  const pct = (amount * 100) / capital;
  return {
    amount,
    pct,
    amountText: String(Number(amount.toFixed(2))),
    pctText: pct.toFixed(2),
    exceedsAcademy: pct > academyPct,
    exceedsDaily: pct > dailyPct,
  };
}

/**
 * `k x risk` against the academy criterion and the daily limit (echoed fraction, else 0.05, shown as a percent), computed on the client from
 * `k` and the typed risk. `observed` carries the diagnostics' `peak x risk` per successful kind, labelled with
 * its kind (no cross-kind figure). Informational only: it never blocks Run. Null when it cannot be computed.
 */
export function toWorstCaseReadout(
  memberCount: number,
  risk: number | null,
  capital: number | null,
  echoedDailyLossLimitFraction: number | null,
  peaks: readonly { kind: BacktestRunKind; peak: number }[],
): WorstCaseReadoutVm | null {
  if (memberCount < 1 || !isPositive(risk) || !isPositive(capital)) return null;
  const dailyLimitPct = fractionToPct(
    echoedDailyLossLimitFraction ?? FTMO_DEFAULT_DAILY_LOSS_LIMIT_FRACTION,
  );
  const academyPct = ACADEMY_GROUP_CRITERION_PCT;
  return {
    memberCount,
    worst: readoutLine(memberCount * risk, capital, academyPct, dailyLimitPct),
    academyPct,
    dailyLimitPct,
    observed: peaks.map((p) => ({
      kind: p.kind,
      peak: p.peak,
      line: readoutLine(p.peak * risk, capital, academyPct, dailyLimitPct),
    })),
  };
}
