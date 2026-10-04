import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidateRunDto,
} from '../../core/models/ftmo-group-simulation.model';
import {
  canSelect,
  distinctSymbols,
  filterCandidates,
  pickDefaultAccountId,
  toCandidateRowVm,
  toggleSelection,
} from './ftmo-group-simulation.mappers';

function run(overrides: Partial<FtmoGroupCandidateRunDto> = {}): FtmoGroupCandidateRunDto {
  return {
    runId: 'run',
    symbol: 'XAUUSD',
    tradeCount: 10,
    firstOpen: '2024-01-02T08:00:00',
    lastClose: '2024-06-30T20:15:00',
    hasInstrumentSpec: true,
    isCalibrated: true,
    profitCurrency: 'USD',
    needsFxBand: false,
    sourceTimeZoneId: 'Etc/GMT-2',
    ...overrides,
  };
}

function candidate(
  strategyId: string,
  name: string,
  symbol: string | null,
  overrides: Partial<FtmoGroupCandidateDto> = {},
): FtmoGroupCandidateDto {
  return {
    strategyId,
    name,
    symbol,
    deploy: run(),
    evaluation: run(),
    nameExistsOnOtherAccount: false,
    ...overrides,
  };
}

const ALL = [
  candidate('1', 'Gold Breakout', 'XAUUSD'),
  candidate('2', 'Gold Reversal', 'XAUUSD'),
  candidate('3', 'Euro Trend', 'EURUSD'),
  candidate('4', 'Unnamed Symbol', null),
];

describe('picker filter and search', () => {
  it('noFilter_KeepsEveryCandidate', () => {
    expect(filterCandidates(ALL, { symbol: null, search: '' })).toHaveLength(4);
  });

  it('theSymbolFilter_KeepsOnlyThatSymbol', () => {
    expect(
      filterCandidates(ALL, { symbol: 'EURUSD', search: '' }).map((c) => c.strategyId),
    ).toEqual(['3']);
  });

  it('theNameSearch_IsCaseInsensitiveAndTrimmed', () => {
    expect(
      filterCandidates(ALL, { symbol: null, search: '  gOLD ' }).map((c) => c.strategyId),
    ).toEqual(['1', '2']);
  });

  it('symbolAndSearchCombine_WithAnd', () => {
    expect(
      filterCandidates(ALL, { symbol: 'XAUUSD', search: 'reversal' }).map((c) => c.strategyId),
    ).toEqual(['2']);
    expect(filterCandidates(ALL, { symbol: 'EURUSD', search: 'gold' })).toEqual([]);
  });

  it('distinctSymbols_AreSortedAndSkipNull', () => {
    expect(distinctSymbols(ALL)).toEqual(['EURUSD', 'XAUUSD']);
  });

  it('filteringNeverTouchesTheSelection_SoAHiddenSelectedMemberStaysSelected', () => {
    const selected: ReadonlySet<string> = new Set(['1']);
    const visible = filterCandidates(ALL, { symbol: 'EURUSD', search: '' });
    expect(visible.some((c) => c.strategyId === '1')).toBe(false);
    // the filter is a pure view over the list: the selection set is a separate value and is unchanged
    expect([...selected]).toEqual(['1']);
    expect(toggleSelection(selected, '3', 4).has('1')).toBe(true);
  });
});

describe('selection cap', () => {
  it('canSelect_StopsAtTheSuppliedMaxMembers', () => {
    const selected: ReadonlySet<string> = new Set(['1', '2']);
    expect(canSelect(selected, '3', 3)).toBe(true);
    expect(canSelect(new Set(['1', '2', '3']), '4', 3)).toBe(false);
  });

  it('theCapComesFromTheArgument_NotAConstant', () => {
    const selected: ReadonlySet<string> = new Set(['1', '2', '3', '4', '5']);
    expect(canSelect(selected, '6', 6)).toBe(true);
    expect(canSelect(new Set(['1']), '2', 1)).toBe(false);
  });

  it('aMaxMembersOfZero_AllowsNothing', () => {
    expect(canSelect(new Set(), '1', 0)).toBe(false);
  });

  it('anAlreadySelectedRow_StaysDeselectableAtTheCap', () => {
    expect(canSelect(new Set(['1', '2']), '1', 2)).toBe(true);
  });

  it('toggleSelection_AddsUntilTheCapThenIgnoresAndDeselectionFreesASlot', () => {
    let selected: ReadonlySet<string> = new Set();
    selected = toggleSelection(selected, '1', 2);
    selected = toggleSelection(selected, '2', 2);
    expect([...selected]).toEqual(['1', '2']);
    expect([...toggleSelection(selected, '3', 2)]).toEqual(['1', '2']);
    selected = toggleSelection(selected, '1', 2);
    expect([...selected]).toEqual(['2']);
    expect(canSelect(selected, '3', 2)).toBe(true);
  });
});

describe('candidate row view model', () => {
  it('aPresentKind_ShowsItsDateRangeAsDatesOnly', () => {
    const vm = toCandidateRowVm(candidate('1', 'A', 'XAUUSD'));
    expect(vm.deploy).toMatchObject({
      present: true,
      from: '2024-01-02',
      to: '2024-06-30',
      tradeCount: 10,
    });
  });

  it('anAbsentKind_IsExplicitlyAbsent_NotBlankOrZero', () => {
    const vm = toCandidateRowVm(candidate('1', 'A', 'XAUUSD', { evaluation: null }));
    expect(vm.evaluation).toEqual({
      present: false,
      from: null,
      to: null,
      tradeCount: null,
      spec: 'absent',
      calibration: 'absent',
      needsFxBand: false,
    });
  });

  it('aRunWithZeroTrades_KeepsTradeCountZeroAndHasNoRange', () => {
    const vm = toCandidateRowVm(
      candidate('1', 'A', 'XAUUSD', {
        deploy: run({ tradeCount: 0, firstOpen: null, lastClose: null }),
      }),
    );
    expect(vm.deploy.present).toBe(true);
    expect(vm.deploy.tradeCount).toBe(0);
    expect(vm.deploy.from).toBeNull();
    expect(vm.deploy.to).toBeNull();
  });

  it('aMissingSpec_IsFlaggedNoAndCalibrationIsNotApplicable', () => {
    const vm = toCandidateRowVm(
      candidate('1', 'A', 'XAUUSD', {
        deploy: run({ hasInstrumentSpec: false, isCalibrated: false }),
      }),
    );
    expect(vm.deploy.spec).toBe('no');
    expect(vm.deploy.calibration).toBe('na');
  });

  it('aSpecWithoutCalibration_FlagsCalibrationNo_AndBothYesWhenComplete', () => {
    const uncal = toCandidateRowVm(
      candidate('1', 'A', 'XAUUSD', { deploy: run({ isCalibrated: false }) }),
    );
    expect(uncal.deploy.spec).toBe('yes');
    expect(uncal.deploy.calibration).toBe('no');
    const ok = toCandidateRowVm(candidate('1', 'A', 'XAUUSD'));
    expect(ok.deploy.spec).toBe('yes');
    expect(ok.deploy.calibration).toBe('yes');
  });

  it('needsFxBand_IsPerKindAndSummarisedForTheRow', () => {
    const vm = toCandidateRowVm(
      candidate('1', 'A', 'EURUSD', { evaluation: run({ needsFxBand: true }) }),
    );
    expect(vm.deploy.needsFxBand).toBe(false);
    expect(vm.evaluation.needsFxBand).toBe(true);
    expect(vm.needsFxBand).toBe(true);
  });

  it('theOtherAccountFlagAndSymbolPassThrough_WithANullSymbolStayingNull', () => {
    const vm = toCandidateRowVm(candidate('9', 'Dup', null, { nameExistsOnOtherAccount: true }));
    expect(vm.nameExistsOnOtherAccount).toBe(true);
    expect(vm.symbol).toBeNull();
    expect(vm.strategyId).toBe('9');
  });

  it('aCandidateLackingKindSpecAndCalibration_IsStillSelectable', () => {
    // flags inform, never block
    const c = candidate('1', 'A', 'XAUUSD', {
      deploy: run({ hasInstrumentSpec: false, isCalibrated: false }),
      evaluation: null,
    });
    expect(canSelect(new Set(), toCandidateRowVm(c).strategyId, 2)).toBe(true);
  });
});

describe('default account', () => {
  it('prefersTheAccountNamedSBDEMO2', () => {
    expect(
      pickDefaultAccountId([
        { id: 'a', name: 'Other' },
        { id: 'b', name: 'SBDEMO2' },
      ]),
    ).toBe('b');
  });

  it('theNameMatchIsCaseInsensitive', () => {
    expect(
      pickDefaultAccountId([
        { id: 'a', name: 'Other' },
        { id: 'b', name: 'sbdemo2' },
      ]),
    ).toBe('b');
  });

  it('fallsBackToTheFirstAccount_AndToNullWhenThereAreNone', () => {
    expect(
      pickDefaultAccountId([
        { id: 'a', name: 'One' },
        { id: 'b', name: 'Two' },
      ]),
    ).toBe('a');
    expect(pickDefaultAccountId([])).toBeNull();
  });
});
