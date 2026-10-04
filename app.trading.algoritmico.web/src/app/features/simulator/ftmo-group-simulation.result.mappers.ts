import {
  FtmoGroupKindResultDto,
  FtmoGroupMemberCoverageDto,
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
} from '../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationStatus } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import {
  FtmoNotModelledItem,
  FtmoRunPanelVm,
  REFUSAL_LABELS,
  labelFor,
  toNotModelledItems,
  toRunPanelVm,
} from '../broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers';

const RESULT = 'SIMULATOR.FTMO_GROUP.RESULT.';
const NOT_REPORTED_KEY = 'FTMO_SIMULATION.NOT_REPORTED';

/**
 * One label per `FtmoGroupRefusal`, each with its own text (spec "Each refusal has a distinct message"). The
 * lookup is by exact value through `labelFor`, so `InvalidRequest = 0` is never read as absent.
 */
export const GROUP_REFUSAL_LABELS: Record<FtmoGroupRefusal, string> = {
  [FtmoGroupRefusal.InvalidRequest]: `${RESULT}REFUSAL.INVALID_REQUEST`,
  [FtmoGroupRefusal.SharedInputsRefused]: `${RESULT}REFUSAL.SHARED_INPUTS_REFUSED`,
  [FtmoGroupRefusal.MemberNotFound]: `${RESULT}REFUSAL.MEMBER_NOT_FOUND`,
  [FtmoGroupRefusal.MemberMissingKind]: `${RESULT}REFUSAL.MEMBER_MISSING_KIND`,
  [FtmoGroupRefusal.MemberRunRefused]: `${RESULT}REFUSAL.MEMBER_RUN_REFUSED`,
  [FtmoGroupRefusal.MixedSourceTimeZones]: `${RESULT}REFUSAL.MIXED_SOURCE_TIME_ZONES`,
  [FtmoGroupRefusal.NoCommonWindow]: `${RESULT}REFUSAL.NO_COMMON_WINDOW`,
  [FtmoGroupRefusal.MemberHasNoTradesInWindow]: `${RESULT}REFUSAL.MEMBER_HAS_NO_TRADES_IN_WINDOW`,
};

/** A translation key plus the raw enum value that fills the UNKNOWN_VALUE `{{value}}` placeholder. */
export interface TextRef {
  key: string;
  value: number | null;
}

export interface MemberReasonVm {
  strategyId: string;
  name: string;
  reason: TextRef;
}

export interface CoverageRowVm {
  strategyId: string;
  name: string;
  firstOpen: string | null;
  lastClose: string | null;
  inWindowTrades: number;
  /** True when the member's range extends beyond the shared window. */
  wider: boolean;
}

/** `evaluated` renders the reused panel, `refused` this kind's refusal, `noRun` a kind the response omitted. */
export type GroupKindState = 'evaluated' | 'refused' | 'noRun';

export interface GroupKindSlotVm {
  kind: BacktestRunKind;
  kindLabelKey: string;
  state: GroupKindState;
  refusal: TextRef | null;
  members: MemberReasonVm[];
  window: { start: string; end: string } | null;
  coverage: CoverageRowVm[];
  shortened: boolean;
  panel: FtmoRunPanelVm | null;
}

export interface GroupWideRefusalVm {
  label: TextRef;
  shared: TextRef | null;
  unknownIds: string[];
  zones: { name: string; zone: string | null }[];
}

export interface NameWarningVm {
  name: string;
  ids: string[];
}

export interface GroupResultVm {
  groupRefusal: GroupWideRefusalVm | null;
  slots: GroupKindSlotVm[];
  nameWarnings: NameWarningVm[];
  notModelled: FtmoNotModelledItem[];
  /** The reused result disclosure and `NotModelled` list: only when some panel shows findings. */
  showRunNotes: boolean;
}

const KIND_ORDER: readonly BacktestRunKind[] = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

function kindLabelKey(kind: BacktestRunKind): string {
  return kind === BacktestRunKind.Deploy
    ? 'FTMO_SIMULATION.KIND.DEPLOY'
    : 'FTMO_SIMULATION.KIND.EVALUATION';
}

function groupRefusalRef(value: FtmoGroupRefusal | null): TextRef {
  const label = labelFor(GROUP_REFUSAL_LABELS, value);
  if (label === null) return { key: NOT_REPORTED_KEY, value: null };
  return { key: label.key, value: label.value ?? null };
}

function runReasonRef(value: number): TextRef {
  const label = labelFor(REFUSAL_LABELS, value);
  return label === null
    ? { key: NOT_REPORTED_KEY, value: null }
    : { key: label.key, value: label.value ?? null };
}

function dateOnly(value: string | null): string | null {
  return value === null ? null : value.slice(0, 10);
}

/** True when `candidate` falls outside `[start, end]`; an unparsable instant never claims "wider". */
function outside(candidate: string | null, bound: string, side: 'before' | 'after'): boolean {
  if (candidate === null) return false;
  const c = Date.parse(candidate);
  const b = Date.parse(bound);
  if (Number.isNaN(c) || Number.isNaN(b)) return false;
  return side === 'before' ? c < b : c > b;
}

function toCoverageRow(
  row: FtmoGroupMemberCoverageDto,
  window: FtmoGroupKindResultDto['window'],
): CoverageRowVm {
  const wider =
    window !== null &&
    (outside(row.firstOpen, window.start, 'before') || outside(row.lastClose, window.end, 'after'));
  return {
    strategyId: row.strategyId,
    name: row.name,
    firstOpen: dateOnly(row.firstOpen),
    lastClose: dateOnly(row.lastClose),
    inWindowTrades: row.inWindowTrades,
    wider,
  };
}

function toSlot(
  kind: BacktestRunKind,
  result: FtmoGroupKindResultDto | undefined,
): GroupKindSlotVm {
  const base = {
    kind,
    kindLabelKey: kindLabelKey(kind),
    refusal: null,
    members: [],
    window: null,
    coverage: [],
    shortened: false,
    panel: null,
  };
  if (result === undefined) return { ...base, state: 'noRun' };

  const window = result.window;
  const coverage = result.coverage.map((c) => toCoverageRow(c, window));
  const common = {
    ...base,
    window:
      window === null ? null : { start: window.start.slice(0, 10), end: window.end.slice(0, 10) },
    coverage,
    shortened: coverage.some((c) => c.wider),
  };

  const refused = result.status === FtmoSimulationStatus.Refused || result.refusal !== null;
  if (refused) {
    return {
      ...common,
      state: 'refused',
      refusal: groupRefusalRef(result.refusal),
      members: result.memberRefusals.map((m) => ({
        strategyId: m.strategyId,
        name: m.name,
        // A MemberRunRefused row carries the shipped per-run reason; every other row is a group reason.
        reason:
          m.reason === FtmoGroupRefusal.MemberRunRefused && m.runReason !== null
            ? runReasonRef(m.runReason)
            : groupRefusalRef(m.reason),
      })),
    };
  }

  if (result.run === null) return { ...common, state: 'noRun' };

  // Group option (a): the group has no single-start anchor, so the reused panel must never show the note.
  const panel = toRunPanelVm({ ...result.run, start1DiffersFromSingleStartAnchor: false });
  return { ...common, state: 'evaluated', panel };
}

/** Maps the group envelope to the view model of the result area. Presence is tested with `!== null`. */
export function toGroupResultVm(dto: FtmoGroupSimulationDto): GroupResultVm {
  const nameWarnings = dto.duplicateNameWarnings.map((w) => ({
    name: w.name,
    ids: [...w.strategyIds],
  }));

  if (dto.status === FtmoSimulationStatus.Refused || dto.refusal !== null) {
    return {
      groupRefusal: {
        label: groupRefusalRef(dto.refusal),
        shared: dto.sharedRefusal === null ? null : runReasonRef(dto.sharedRefusal),
        unknownIds: [...dto.unknownStrategyIds],
        zones:
          dto.refusal === FtmoGroupRefusal.MixedSourceTimeZones
            ? dto.members.map((m) => ({ name: m.name, zone: m.sourceTimeZoneId }))
            : [],
      },
      slots: [],
      nameWarnings,
      notModelled: [],
      showRunNotes: false,
    };
  }

  const slots = KIND_ORDER.map((kind) =>
    toSlot(
      kind,
      dto.kinds.find((k) => k.kind === kind),
    ),
  );
  const runs = dto.kinds.flatMap((k) => (k.run === null ? [] : [k.run]));
  return {
    groupRefusal: null,
    slots,
    nameWarnings,
    notModelled: toNotModelledItems({ strategyId: '', runs }),
    showRunNotes: slots.some((s) => s.state === 'evaluated'),
  };
}
