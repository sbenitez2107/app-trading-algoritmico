import {
  FtmoGroupSearchIneligibleDto,
  FtmoGroupSearchRowDto,
} from '../../core/models/ftmo-group-search.model';
import {
  FtmoGroupKindResultDto,
  FtmoGroupRefusal,
} from '../../core/models/ftmo-group-simulation.model';
import { FtmoChainOutcome, FtmoSimulationStatus } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { labelFor } from '../broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers';
import {
  EnumKey,
  MedianVm,
  breachShare,
  fractionToPercent,
  ineligibleReasonKey,
  isWithinCeiling,
  medianVm,
} from './ftmo-group-search.mappers';
import { GROUP_REFUSAL_LABELS, TextRef } from './ftmo-group-simulation.result.mappers';

/**
 * Row view-models of the ranked table (slice 3c). Every share and headroom value is a FRACTION on the wire
 * and becomes a percent exactly once, here; the ceiling is compared on raw fractions. A value that is not
 * reported is `null` (rendered as "not reported"), never `0`; a real `0` stays `0`.
 */

/** Default elimination ceiling shown in the table, as a percent. */
export const DEFAULT_CEILING_PERCENT = 5;

const NOT_REPORTED_KEY = 'FTMO_SIMULATION.NOT_REPORTED';

const KIND_ORDER: readonly BacktestRunKind[] = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

export type TableKindState = 'evaluated' | 'refused' | 'noRun';

export interface KindCellsVm {
  kind: BacktestRunKind;
  kindLabelKey: string;
  state: TableKindState;
  refusal: TextRef | null;
  breach: number | null;
  worstDaily: number | null;
  worstDrawdown: number | null;
  medianDrawdown: number | null;
  fundedNoBreach: number | null;
  medianDays: MedianVm;
}

export interface TableRowVm {
  rank: number;
  memberNames: string[];
  symbols: string[];
  peak: number;
  identical: boolean;
  withinCeiling: boolean;
  kinds: KindCellsVm[];
  source: FtmoGroupSearchRowDto;
}

export interface IneligibleCountVm {
  reason: EnumKey;
  count: number;
}

function percentOrNull(fraction: number | null | undefined): number | null {
  return fraction === null || fraction === undefined ? null : fractionToPercent(fraction);
}

/** FundedNoBreachAtEndOfData / StartCount, a fraction; `null` when the summary or the outcome row is absent. */
function fundedNoBreachShare(kind: FtmoGroupKindResultDto): number | null {
  const summary = kind.run?.summary;
  if (!summary || summary.startCount <= 0) return null;
  const outcome = summary.outcomes.find(
    (o) => o.outcome === FtmoChainOutcome.FundedNoBreachAtEndOfData,
  );
  return outcome === undefined ? null : outcome.count / summary.startCount;
}

function refusalRef(value: FtmoGroupRefusal | null): TextRef {
  const label = labelFor(GROUP_REFUSAL_LABELS, value);
  return label === null
    ? { key: NOT_REPORTED_KEY, value: null }
    : { key: label.key, value: label.value ?? null };
}

function kindLabelKey(kind: BacktestRunKind): string {
  return kind === BacktestRunKind.Deploy
    ? 'FTMO_SIMULATION.KIND.DEPLOY'
    : 'FTMO_SIMULATION.KIND.EVALUATION';
}

function toKindCells(
  kind: BacktestRunKind,
  result: FtmoGroupKindResultDto | undefined,
  row: FtmoGroupSearchRowDto,
): KindCellsVm {
  const empty = {
    kind,
    kindLabelKey: kindLabelKey(kind),
    refusal: null,
    breach: null,
    worstDaily: null,
    worstDrawdown: null,
    medianDrawdown: null,
    fundedNoBreach: null,
    medianDays: medianVm(null),
  };
  if (result === undefined) return { ...empty, state: 'noRun' };
  if (result.status === FtmoSimulationStatus.Refused || result.refusal !== null) {
    return { ...empty, state: 'refused', refusal: refusalRef(result.refusal) };
  }
  const headroom = row.kindHeadrooms.find((h) => h.kind === kind);
  const share = breachShare(result);
  return {
    ...empty,
    state: 'evaluated',
    breach: percentOrNull(share),
    worstDaily: percentOrNull(headroom?.worstDailyUsed),
    worstDrawdown: percentOrNull(headroom?.worstMaxUsed),
    medianDrawdown: percentOrNull(headroom?.medianMaxUsed),
    fundedNoBreach: percentOrNull(fundedNoBreachShare(result)),
    medianDays: medianVm(result.run?.summary?.daysToBothTargets.median ?? null),
  };
}

/**
 * The rows to show, in the backend order (never re-sorted). `withinCeiling` is recomputed from the raw
 * fractions against `ceilingFraction` (`null` highlights nothing); `onlyWithin` filters the VIEW without
 * touching the source rows.
 */
export function toTableRows(
  rows: readonly FtmoGroupSearchRowDto[],
  ceilingFraction: number | null,
  onlyWithin: boolean,
): TableRowVm[] {
  const vms = rows.map((row) => ({
    rank: row.rank,
    memberNames: [...row.memberNames],
    symbols: row.symbols === null ? [] : [...row.symbols],
    peak: row.peakConcurrentOpen,
    identical: row.identicalDeployEval,
    withinCeiling: ceilingFraction !== null && isWithinCeiling(row, ceilingFraction),
    kinds: KIND_ORDER.map((kind) =>
      toKindCells(
        kind,
        row.kinds.find((k) => k.kind === kind),
        row,
      ),
    ),
    source: row,
  }));
  return onlyWithin ? vms.filter((r) => r.withinCeiling) : vms;
}

/** Excluded strategies counted by reason, in first-seen order. `Unknown` keeps its raw value. */
export function ineligibleCounts(
  list: readonly FtmoGroupSearchIneligibleDto[],
): IneligibleCountVm[] {
  const out = new Map<number, IneligibleCountVm>();
  for (const item of list) {
    const found = out.get(item.reason);
    if (found) found.count += 1;
    else out.set(item.reason, { reason: ineligibleReasonKey(item.reason), count: 1 });
  }
  return Array.from(out.values());
}
