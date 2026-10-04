import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidateRunDto,
} from '../../core/models/ftmo-group-simulation.model';

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
