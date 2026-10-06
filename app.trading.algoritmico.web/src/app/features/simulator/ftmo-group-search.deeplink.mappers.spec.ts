import { describe, expect, it } from 'vitest';
import { searchRow } from './ftmo-group-search.fixtures';
import {
  DeepLinkParams,
  applyDeepLink,
  buildDeepLink,
  readDeepLink,
} from './ftmo-group-search.deeplink.mappers';
import { DEFAULT_GROUP_FORM } from './ftmo-group-simulation.mappers';

const IDS = ['s1', 's2', 's3', 's4', 's5'];

function link(over: Partial<DeepLinkParams> = {}): DeepLinkParams {
  return {
    account: 'acc',
    members: 's1,s2',
    risk: '0.5',
    capital: '25000',
    fxLow: null,
    fxHigh: null,
    ...over,
  };
}

describe('buildDeepLink', () => {
  const base = { accountId: 'acc', initialCapital: 25000, targetRiskPerTrade: 0.5 };

  it('buildsThePathAndTheQueryWithMembersInAscendingOrder', () => {
    const row = searchRow({ memberIds: ['s3', 's1', 's2'] });
    const target = buildDeepLink(row, { ...base, fxLow: null, fxHigh: null });
    expect(target.path).toEqual(['/simulator', 'ftmo']);
    expect(target.queryParams).toEqual({
      account: 'acc',
      members: 's1,s2,s3',
      risk: 0.5,
      capital: 25000,
    });
  });

  it('includesTheFxBandOnlyWhenSet_AndNeverTheBrokerOrLotGrid', () => {
    const target = buildDeepLink(searchRow({ memberIds: ['s1'] }), {
      ...base,
      fxLow: 1.05,
      fxHigh: 1.12,
    });
    expect(target.queryParams).toEqual({
      account: 'acc',
      members: 's1',
      risk: 0.5,
      capital: 25000,
      fxLow: 1.05,
      fxHigh: 1.12,
    });
    expect(Object.keys(target.queryParams)).not.toContain('broker');
    expect(Object.keys(target.queryParams)).not.toContain('step');
  });

  it('aBlankFormValueOmitsItsParameter_ButARealZeroIsKept', () => {
    const row = searchRow({ memberIds: ['s1'] });
    const blank = buildDeepLink(row, {
      ...base,
      initialCapital: null,
      targetRiskPerTrade: null,
      fxLow: null,
      fxHigh: null,
    });
    expect(blank.queryParams).toEqual({ account: 'acc', members: 's1' });
  });

  it('aRiskOfZeroOrANullFxIsHandledWithoutTruthiness', () => {
    const target = buildDeepLink(searchRow({ memberIds: ['s1'] }), {
      ...base,
      targetRiskPerTrade: 0,
      fxLow: 0,
      fxHigh: null,
    });
    expect(target.queryParams['risk']).toBe(0);
    expect(target.queryParams['fxLow']).toBe(0);
    expect('fxHigh' in target.queryParams).toBe(false);
  });
});

describe('readDeepLink', () => {
  it('returnsNullWhenThereAreNoParams', () => {
    expect(readDeepLink({ get: () => null })).toBeNull();
  });

  it('readsEveryKey', () => {
    const map = new Map([
      ['account', 'acc'],
      ['members', 's1'],
      ['risk', '1'],
    ]);
    expect(readDeepLink({ get: (k) => map.get(k) ?? null })).toEqual({
      account: 'acc',
      members: 's1',
      risk: '1',
      capital: null,
      fxLow: null,
      fxHigh: null,
    });
  });
});

describe('applyDeepLink', () => {
  const run = (p: DeepLinkParams, accountKnown = true, max = 4) =>
    applyDeepLink(p, IDS, max, accountKnown, DEFAULT_GROUP_FORM);

  it('preselectsMembersAndTheNumbers_WithoutNotices', () => {
    const r = run(link({ fxLow: '1.05', fxHigh: '1.2' }));
    expect(r.selected).toEqual(['s1', 's2']);
    expect(r.form.targetRiskPerTrade).toBe(0.5);
    expect(r.form.initialCapital).toBe(25000);
    expect(r.form.fxLow).toBe(1.05);
    expect(r.form.fxHigh).toBe(1.2);
    expect(r.notices).toEqual([]);
  });

  it('dropsUnknownAndMalformedIdsWithANoticeNamingTheCount', () => {
    const r = run(link({ members: 's1,zzz,s2,,  ,nope' }));
    expect(r.selected).toEqual(['s1', 's2']);
    expect(r.notices).toEqual([{ key: 'UNKNOWN_MEMBERS', params: { count: 2 } }]);
  });

  it('repeatedIdsCountOnce_AndDoNotCountAsDropped', () => {
    const r = run(link({ members: 's1,s1,s2,s2,zzz,zzz' }));
    expect(r.selected).toEqual(['s1', 's2']);
    expect(r.notices).toEqual([{ key: 'UNKNOWN_MEMBERS', params: { count: 1 } }]);
  });

  it('truncatesToTheCapInLinkOrder_WithACapNotice', () => {
    const r = run(link({ members: 's5,s4,s3,s2,s1' }), true, 3);
    expect(r.selected).toEqual(['s5', 's4', 's3']);
    expect(r.notices).toEqual([{ key: 'OVER_CAP', params: { max: 3 } }]);
  });

  it('theCapCountsOnlyValidIds', () => {
    const r = run(link({ members: 'zzz,s1,s2,s3' }), true, 3);
    expect(r.selected).toEqual(['s1', 's2', 's3']);
    expect(r.notices.map((n) => n.key)).toEqual(['UNKNOWN_MEMBERS']);
  });

  it('anUnknownOrMissingAccountDropsTheMembersWithANotice_ButKeepsTheNumbers', () => {
    const r = run(link(), false);
    expect(r.selected).toEqual([]);
    expect(r.notices.map((n) => n.key)).toEqual(['ACCOUNT_FALLBACK']);
    expect(r.form.targetRiskPerTrade).toBe(0.5);
  });

  it.each(['-5', 'abc', '0', '', 'Infinity'])('anInvalidRiskOf_%s_LeavesTheFieldEmpty', (risk) => {
    const r = run(link({ risk }));
    expect(r.form.targetRiskPerTrade).toBeNull();
    expect(r.notices.map((n) => n.key)).toEqual(['INVALID_VALUES']);
  });

  it('anInvalidCapitalKeepsTheDefault_WithANotice', () => {
    const r = run(link({ capital: '-1' }));
    expect(r.form.initialCapital).toBe(DEFAULT_GROUP_FORM.initialCapital);
    expect(r.notices.map((n) => n.key)).toEqual(['INVALID_VALUES']);
  });

  it('anInconsistentFxBandIsIgnored_WithANotice', () => {
    const r = run(link({ fxLow: '1.2', fxHigh: '1.05' }));
    expect(r.form.fxLow).toBeNull();
    expect(r.form.fxHigh).toBeNull();
    expect(r.notices.map((n) => n.key)).toEqual(['INVALID_VALUES']);
  });

  it('anAbsentRiskIsNotANotice', () => {
    const r = run(link({ risk: null }));
    expect(r.form.targetRiskPerTrade).toBeNull();
    expect(r.notices).toEqual([]);
  });

  it('emptyMembersLeaveNoSelection', () => {
    expect(run(link({ members: '' })).selected).toEqual([]);
    expect(run(link({ members: null })).selected).toEqual([]);
  });

  it('idsAreComparedExactly_NotByTruthiness', () => {
    const r = applyDeepLink(link({ members: '0,1' }), ['0', '1'], 4, true, DEFAULT_GROUP_FORM);
    expect(r.selected).toEqual(['0', '1']);
  });
});
