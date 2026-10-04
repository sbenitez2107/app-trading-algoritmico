import { describe, expect, it } from 'vitest';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationRefusal } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { REFUSAL_LABELS } from '../broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers';
import {
  coverage,
  groupResult,
  groupWideRefusal,
  memberRefusal,
  refusedKind,
  successKind,
} from './ftmo-group-simulation.result.fixtures';
import { GROUP_REFUSAL_LABELS, toGroupResultVm } from './ftmo-group-simulation.result.mappers';

const DEPLOY = BacktestRunKind.Deploy;
const EVAL = BacktestRunKind.Evaluation;

describe('toGroupResultVm slots (F3.4.1)', () => {
  it('givesDeployAndEvaluationTheirOwnSlotsInAFixedOrder_NeverMerged', () => {
    const vm = toGroupResultVm(groupResult([successKind(EVAL), successKind(DEPLOY)]));
    expect(vm.slots.map((s) => s.kind)).toEqual([DEPLOY, EVAL]);
    expect(vm.slots.map((s) => s.state)).toEqual(['evaluated', 'evaluated']);
    expect(vm.slots[0].panel?.kind).toBe(DEPLOY);
    expect(vm.slots[1].panel?.kind).toBe(EVAL);
    expect(vm.groupRefusal).toBeNull();
  });

  it('neverShowsTheStart1Note_EvenWhenTheRunCarriesTheFlag', () => {
    const kind = successKind(DEPLOY);
    expect(kind.run?.start1DiffersFromSingleStartAnchor).toBe(true);
    const vm = toGroupResultVm(groupResult([kind, successKind(EVAL)]));
    expect(vm.slots.map((s) => s.panel?.start1Differs)).toEqual([false, false]);
  });

  it('aRefusedKindShowsItsRefusalWhileTheOtherRenders_AndAnAbsentKindIsANoRunSlot', () => {
    const refused = refusedKind(EVAL, FtmoGroupRefusal.NoCommonWindow);
    const vm = toGroupResultVm(groupResult([successKind(DEPLOY), refused]));
    expect(vm.slots[0].state).toBe('evaluated');
    expect(vm.slots[1].state).toBe('refused');
    expect(vm.slots[1].panel).toBeNull();

    const missing = toGroupResultVm(groupResult([successKind(DEPLOY)]));
    expect(missing.slots[1]).toMatchObject({ kind: EVAL, state: 'noRun', panel: null });
  });
});

describe('per-member reasons (F3.4.2)', () => {
  const MEMBER_LEVEL = [
    FtmoSimulationRefusal.RiskNotEstimable,
    FtmoSimulationRefusal.PointValueNotCalibrated,
    FtmoSimulationRefusal.InstrumentSpecMissing,
    FtmoSimulationRefusal.FxRateNotDeclared,
    FtmoSimulationRefusal.InvalidFxBand,
  ];

  it('listsEachFailingMemberByNameWithItsOwnReasonKey', () => {
    const members = [
      ...MEMBER_LEVEL.map((r, i) =>
        memberRefusal(`m${i}`, `Member ${i}`, FtmoGroupRefusal.MemberRunRefused, r),
      ),
      memberRefusal('x', 'Mx', FtmoGroupRefusal.MemberMissingKind),
      memberRefusal('y', 'My', FtmoGroupRefusal.MemberHasNoTradesInWindow),
    ];
    const vm = toGroupResultVm(
      groupResult([refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, members)]),
    );
    const rows = vm.slots[0].members;
    expect(rows.map((r) => r.name)).toEqual([
      'Member 0',
      'Member 1',
      'Member 2',
      'Member 3',
      'Member 4',
      'Mx',
      'My',
    ]);
    expect(new Set(rows.map((r) => r.reason.key)).size).toBe(7);
    expect(rows[0].reason.key).toBe(REFUSAL_LABELS[FtmoSimulationRefusal.RiskNotEstimable]);
    expect(rows[5].reason.key).toBe(GROUP_REFUSAL_LABELS[FtmoGroupRefusal.MemberMissingKind]);
    expect(rows[6].reason.key).toBe(
      GROUP_REFUSAL_LABELS[FtmoGroupRefusal.MemberHasNoTradesInWindow],
    );
  });

  it('aMemberRunRefusedWithoutAnInnerReasonFallsBackToTheGenericMemberKey', () => {
    const vm = toGroupResultVm(
      groupResult([
        refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, [
          memberRefusal('a', 'Alpha', FtmoGroupRefusal.MemberRunRefused, null),
        ]),
      ]),
    );
    expect(vm.slots[0].members[0].reason).toEqual({
      key: GROUP_REFUSAL_LABELS[FtmoGroupRefusal.MemberRunRefused],
      value: null,
    });
  });

  it('theInnerRunReasonZeroRendersItsLabel_NotTreatedAsAbsent', () => {
    const vm = toGroupResultVm(
      groupResult([
        refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, [
          memberRefusal(
            'a',
            'Alpha',
            FtmoGroupRefusal.MemberRunRefused,
            FtmoSimulationRefusal.InvalidRequest,
          ),
        ]),
      ]),
    );
    expect(vm.slots[0].members[0].reason.key).toBe(
      REFUSAL_LABELS[FtmoSimulationRefusal.InvalidRequest],
    );
  });

  it('aSymbolLevelRefusalAppearsInBothSlots_AndAMixedCauseListKeepsEachOwnReason', () => {
    const symbol = memberRefusal(
      'b',
      'Beta',
      FtmoGroupRefusal.MemberRunRefused,
      FtmoSimulationRefusal.InstrumentSpecMissing,
    );
    const mixed = [
      symbol,
      memberRefusal(
        'c',
        'Gamma',
        FtmoGroupRefusal.MemberRunRefused,
        FtmoSimulationRefusal.RiskNotEstimable,
      ),
    ];
    const vm = toGroupResultVm(
      groupResult([
        refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, mixed),
        refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [symbol]),
      ]),
    );
    expect(vm.slots[0].members.map((m) => m.reason.key)).toEqual([
      REFUSAL_LABELS[FtmoSimulationRefusal.InstrumentSpecMissing],
      REFUSAL_LABELS[FtmoSimulationRefusal.RiskNotEstimable],
    ]);
    expect(vm.slots[1].members.map((m) => m.name)).toEqual(['Beta']);
  });

  it('noCommonWindowShowsCoverageAndBlamesNoMember', () => {
    const vm = toGroupResultVm(
      groupResult([
        refusedKind(
          DEPLOY,
          FtmoGroupRefusal.NoCommonWindow,
          [],
          [coverage('a', 'Alpha'), coverage('b', 'Beta', { inWindowTrades: 0 })],
        ),
      ]),
    );
    const slot = vm.slots[0];
    expect(slot.refusal?.key).toBe(GROUP_REFUSAL_LABELS[FtmoGroupRefusal.NoCommonWindow]);
    expect(slot.members).toEqual([]);
    expect(slot.coverage.map((c) => c.name)).toEqual(['Alpha', 'Beta']);
    expect(slot.coverage[1].inWindowTrades).toBe(0);
  });
});

describe('group-wide refusal (F3.4.3)', () => {
  it('invalidRequestZeroIsPresent_AndHasItsOwnLabel', () => {
    expect(FtmoGroupRefusal.InvalidRequest).toBe(0);
    const vm = toGroupResultVm(groupWideRefusal(FtmoGroupRefusal.InvalidRequest));
    expect(vm.groupRefusal).not.toBeNull();
    expect(vm.groupRefusal?.label.key).toBe(GROUP_REFUSAL_LABELS[FtmoGroupRefusal.InvalidRequest]);
    expect(vm.slots).toEqual([]);
  });

  it('anUnknownValueMapsToUnknownCarryingTheRawValue', () => {
    const vm = toGroupResultVm(groupWideRefusal(99 as FtmoGroupRefusal));
    expect(vm.groupRefusal?.label).toEqual({ key: 'FTMO_SIMULATION.UNKNOWN_VALUE', value: 99 });
  });

  it('sharedInputsRefusedCarriesItsInnerReason_IncludingZero', () => {
    const vm = toGroupResultVm(
      groupWideRefusal(FtmoGroupRefusal.SharedInputsRefused, {
        sharedRefusal: FtmoSimulationRefusal.LimitsNotConfigured,
      }),
    );
    expect(vm.groupRefusal?.shared?.key).toBe(
      REFUSAL_LABELS[FtmoSimulationRefusal.LimitsNotConfigured],
    );
    const zero = toGroupResultVm(
      groupWideRefusal(FtmoGroupRefusal.SharedInputsRefused, {
        sharedRefusal: FtmoSimulationRefusal.InvalidRequest,
      }),
    );
    expect(zero.groupRefusal?.shared?.key).toBe(
      REFUSAL_LABELS[FtmoSimulationRefusal.InvalidRequest],
    );
    expect(
      toGroupResultVm(groupWideRefusal(FtmoGroupRefusal.InvalidRequest)).groupRefusal?.shared,
    ).toBeNull();
  });

  it('memberNotFoundListsTheUnknownIds', () => {
    const vm = toGroupResultVm(
      groupWideRefusal(FtmoGroupRefusal.MemberNotFound, { unknownStrategyIds: ['u1', 'u2'] }),
    );
    expect(vm.groupRefusal?.unknownIds).toEqual(['u1', 'u2']);
  });

  it('mixedSourceTimeZonesListsEachMemberWithItsZone', () => {
    const vm = toGroupResultVm(groupWideRefusal(FtmoGroupRefusal.MixedSourceTimeZones));
    expect(vm.groupRefusal?.zones).toEqual([
      { name: 'Alpha', zone: 'Europe/Berlin' },
      { name: 'Beta', zone: 'America/New_York' },
    ]);
  });

  it('everyGroupRefusalAndEveryMemberReasonHasItsOwnKey_NoTwoShareText', () => {
    const groupKeys = Object.values(GROUP_REFUSAL_LABELS);
    expect(groupKeys.length).toBe(8);
    const all = [...groupKeys, ...Object.values(REFUSAL_LABELS)];
    expect(new Set(all).size).toBe(all.length);
  });
});

describe('window and coverage (F3.4.4)', () => {
  it('showsTheWindowAsDaysAndACoverageRowPerMember', () => {
    const vm = toGroupResultVm(groupResult([successKind(DEPLOY)]));
    const slot = vm.slots[0];
    expect(slot.window).toEqual({ start: '2020-02-01', end: '2021-06-30' });
    expect(slot.coverage).toEqual([
      {
        strategyId: 'a',
        name: 'Alpha',
        firstOpen: '2020-02-01',
        lastClose: '2021-06-30',
        inWindowTrades: 7,
        wider: false,
      },
      expect.objectContaining({ name: 'Beta', wider: false }),
    ]);
    expect(slot.shortened).toBe(false);
  });

  it('flagsTheShortenedWindowWhenAMemberRangeIsWider_OnEitherEnd', () => {
    const kind = successKind(DEPLOY);
    kind.coverage = [
      coverage('a', 'Alpha', { firstOpen: '2019-01-01T00:00:00' }),
      coverage('b', 'Beta', { lastClose: '2022-01-01T00:00:00' }),
      coverage('c', 'Gamma'),
    ];
    const slot = toGroupResultVm(groupResult([kind])).slots[0];
    expect(slot.coverage.map((c) => c.wider)).toEqual([true, true, false]);
    expect(slot.shortened).toBe(true);
  });

  it('keepsAnEmptyMemberAbsent_NeverZeroOrADate', () => {
    const kind = successKind(DEPLOY);
    kind.coverage = [
      coverage('a', 'Alpha', { firstOpen: null, lastClose: null, inWindowTrades: 0 }),
    ];
    const row = toGroupResultVm(groupResult([kind])).slots[0].coverage[0];
    expect(row.firstOpen).toBeNull();
    expect(row.lastClose).toBeNull();
    expect(row.inWindowTrades).toBe(0);
    expect(row.wider).toBe(false);
  });
});

describe('disclosure inputs (F3.4.5)', () => {
  it('nameWarningsCarryTheSharedNameAndBothIds', () => {
    const vm = toGroupResultVm(
      groupResult([successKind(DEPLOY)], {
        duplicateNameWarnings: [{ name: 'Alpha', strategyIds: ['a', 'a2'] }],
      }),
    );
    expect(vm.nameWarnings).toEqual([{ name: 'Alpha', ids: ['a', 'a2'] }]);
  });

  it('notModelledIsOneDeduplicatedListForTheWholeResult', () => {
    const vm = toGroupResultVm(groupResult([successKind(DEPLOY), successKind(EVAL)]));
    expect(vm.notModelled.map((i) => i.value)).toEqual([
      'Swap',
      'FtmoCommission',
      'IntradayEquity',
    ]);
  });

  it('aGroupWideRefusalCarriesNoRunNotes', () => {
    const vm = toGroupResultVm(groupWideRefusal(FtmoGroupRefusal.InvalidRequest));
    expect(vm.notModelled).toEqual([]);
    expect(vm.showRunNotes).toBe(false);
    expect(toGroupResultVm(groupResult([successKind(DEPLOY)])).showRunNotes).toBe(true);
  });
});
