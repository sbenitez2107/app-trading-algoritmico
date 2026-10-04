import { describe, expect, it } from 'vitest';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import {
  THREE_MEMBERS,
  contribution,
  diagnosticsFixture,
  groupResult,
  memberAttribution,
  refusedKind,
  successKind,
} from './ftmo-group-simulation.result.fixtures';
import { toDiagnosticsVm, toGroupResultVm } from './ftmo-group-simulation.result.mappers';

const DEPLOY = BacktestRunKind.Deploy;
const EVAL = BacktestRunKind.Evaluation;

describe('diagnostics view model (F4.1)', () => {
  it('mapsEveryContributionColumnForEveryMember_NetAsMoneyText', () => {
    const vm = toDiagnosticsVm(DEPLOY, diagnosticsFixture(), THREE_MEMBERS);
    expect(vm.kindLabelKey).toBe('FTMO_SIMULATION.KIND.DEPLOY');
    expect(vm.contributions).toEqual([
      {
        strategyId: 'a',
        name: 'Alpha',
        inWindowTrades: 20,
        scalableTrades: 18,
        netLowText: '150.5',
        netHighText: '210.25',
        raisedToMinimum: 2,
        cappedAtMaximum: 1,
        unscalable: 2,
      },
      expect.objectContaining({ name: 'Beta', netLowText: '-42.5', netHighText: '0' }),
      expect.objectContaining({
        name: 'Gamma',
        raisedToMinimum: 0,
        cappedAtMaximum: 0,
        unscalable: 0,
      }),
    ]);
  });

  it('aNetIsMoney_NeverScaledAsAFraction', () => {
    const vm = toDiagnosticsVm(
      DEPLOY,
      diagnosticsFixture({
        contributions: [contribution('a', 'Alpha', { netLow: 0.05, netHigh: 1234.567 })],
      }),
      THREE_MEMBERS,
    );
    expect(vm.contributions[0].netLowText).toBe('0.05');
    expect(vm.contributions[0].netHighText).toBe('1234.57');
  });

  it('resolvesNamesThroughMembers_FallingBackToTheDtoNameThenTheId', () => {
    const diag = diagnosticsFixture({
      contributions: [contribution('a', 'Stale name'), contribution('zz', 'Own name')],
      peak: {
        peakConcurrentOpen: 2,
        firstReachedSource: null,
        memberIdsAtPeak: ['b', 'unknown-id'],
      },
    });
    const vm = toDiagnosticsVm(DEPLOY, diag, THREE_MEMBERS);
    expect(vm.contributions.map((c) => c.name)).toEqual(['Alpha', 'Own name']);
    expect(vm.peak.members).toEqual([
      { strategyId: 'b', name: 'Beta' },
      { strategyId: 'unknown-id', name: 'unknown-id' },
    ]);
  });

  it('computesEachShareFromItsCountOverTheDecidingBreachStarts_OneDecimal', () => {
    const vm = toDiagnosticsVm(DEPLOY, diagnosticsFixture(), THREE_MEMBERS);
    const alpha = vm.attribution.rows[0];
    expect(alpha.phase1).toEqual({ count: 4, share: 40 });
    expect(alpha.phase2).toEqual({ count: 1, share: 10 });
    expect(alpha.funded).toEqual({ count: 1, share: 10 });
    expect(alpha.sole).toEqual({ count: 4, share: 40 });
    expect(alpha.tied).toEqual({ count: 2, share: 20 });
    expect(vm.attribution.tied).toEqual({ count: 2, share: 20 });
    expect(vm.attribution.unattributed).toEqual({ count: 0, share: 0 });
    expect(vm.attribution.decidingBreachStarts).toBe(10);

    const thirds = diagnosticsFixture();
    thirds.attribution.decidingBreachStarts = 3;
    thirds.attribution.members = [memberAttribution('a', 'Alpha', { soleContributorStarts: 1 })];
    expect(toDiagnosticsVm(DEPLOY, thirds, THREE_MEMBERS).attribution.rows[0].sole.share).toBe(
      33.3,
    );
  });

  it('aZeroCountWithAPositiveDenominatorIsAZeroShare_NotAbsent', () => {
    const vm = toDiagnosticsVm(DEPLOY, diagnosticsFixture(), THREE_MEMBERS);
    const gamma = vm.attribution.rows[2];
    expect(gamma.phase1).toEqual({ count: 0, share: 0 });
    expect(gamma.tied).toEqual({ count: 0, share: 0 });
  });

  it('aZeroDenominatorRendersTheSharesAbsent_ButKeepsTheCounts', () => {
    const diag = diagnosticsFixture();
    diag.attribution = {
      decidingBreachStarts: 0,
      sharedCloseStarts: 0,
      unattributedStarts: 0,
      members: [
        memberAttribution('a', 'Alpha', {
          phase1Starts: 0,
          phase2Starts: 0,
          fundedStarts: 0,
          soleContributorStarts: 0,
          sharedCloseStarts: 0,
        }),
      ],
    };
    const vm = toDiagnosticsVm(DEPLOY, diag, THREE_MEMBERS);
    expect(vm.attribution.rows[0].phase1).toEqual({ count: 0, share: null });
    expect(vm.attribution.tied).toEqual({ count: 0, share: null });
    expect(vm.attribution.unattributed).toEqual({ count: 0, share: null });
  });

  it('aMissingValueIsExplicitlyAbsent_NeverZero_AndZeroStaysZero', () => {
    const diag = diagnosticsFixture();
    const broken = {
      ...diag.contributions[0],
      inWindowTrades: null,
      netLow: null,
      netHigh: undefined,
      unscalable: 0,
    } as unknown as (typeof diag.contributions)[number];
    diag.contributions = [broken];
    const row = toDiagnosticsVm(DEPLOY, diag, THREE_MEMBERS).contributions[0];
    expect(row.inWindowTrades).toBeNull();
    expect(row.netLowText).toBeNull();
    expect(row.netHighText).toBeNull();
    expect(row.unscalable).toBe(0);
  });

  it('aMissingAttributionCountIsAbsent_NeverAZeroShare', () => {
    const diag = diagnosticsFixture();
    diag.attribution.members = [
      {
        ...memberAttribution('a', 'Alpha'),
        phase1Starts: null,
      } as unknown as (typeof diag.attribution.members)[number],
    ];
    const row = toDiagnosticsVm(DEPLOY, diag, THREE_MEMBERS).attribution.rows[0];
    expect(row.phase1).toEqual({ count: null, share: null });
    expect(row.phase2.count).toBe(1);
  });

  it('aPeakOfZeroIsKept_WithNoMembersAndNoInstant', () => {
    const vm = toDiagnosticsVm(
      DEPLOY,
      diagnosticsFixture({
        peak: { peakConcurrentOpen: 0, firstReachedSource: null, memberIdsAtPeak: [] },
      }),
      THREE_MEMBERS,
    );
    expect(vm.peak).toEqual({ peak: 0, firstReached: null, members: [] });
  });

  it('peakNamesTheMembersAtThePeak_AndShowsTheInstantWithoutSeconds', () => {
    const vm = toDiagnosticsVm(DEPLOY, diagnosticsFixture(), THREE_MEMBERS);
    expect(vm.peak.peak).toBe(3);
    expect(vm.peak.members.map((m) => m.name)).toEqual(['Alpha', 'Beta', 'Gamma']);
    expect(vm.peak.firstReached).toBe('2020-05-04 10:30');
  });

  it('theSlotCarriesDiagnosticsOnlyForASuccessfulKind', () => {
    const vm = toGroupResultVm(
      groupResult(
        [
          successKind(DEPLOY, diagnosticsFixture()),
          refusedKind(EVAL, FtmoGroupRefusal.NoCommonWindow),
        ],
        { members: THREE_MEMBERS },
      ),
    );
    expect(vm.slots[0].diagnostics?.kindLabelKey).toBe('FTMO_SIMULATION.KIND.DEPLOY');
    expect(vm.slots[1].diagnostics).toBeNull();
  });

  it('aSuccessfulKindWithoutDiagnosticsHasNoPanel', () => {
    const vm = toGroupResultVm(groupResult([successKind(DEPLOY)]));
    expect(vm.slots[0].diagnostics).toBeNull();
  });
});
