import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidateRunDto,
  FtmoGroupCandidatesDto,
} from './ftmo-group-simulation.model';

/**
 * Typed fixtures with NO casts (PR1a lesson): `tsc` fails when the mirror lacks a field the backend
 * record has, or when a fixture carries one the mirror does not declare. Field-for-field with
 * `FtmoGroupCandidatesDto.cs`.
 */
describe('ftmo group candidates model mirror', () => {
  const run: FtmoGroupCandidateRunDto = {
    runId: 'r1',
    symbol: 'XAUUSD',
    tradeCount: 0,
    firstOpen: null,
    lastClose: null,
    hasInstrumentSpec: false,
    isCalibrated: false,
    profitCurrency: null,
    needsFxBand: false,
    sourceTimeZoneId: null,
  };

  const candidate: FtmoGroupCandidateDto = {
    strategyId: 's1',
    name: 'Alpha',
    symbol: null,
    deploy: run,
    evaluation: null,
    nameExistsOnOtherAccount: false,
  };

  const response: FtmoGroupCandidatesDto = {
    tradingAccountId: 'a1',
    maxMembers: 4,
    candidates: [candidate],
  };

  it('aFullyTypedFixture_CarriesEveryBackendField', () => {
    expect(Object.keys(response).sort()).toEqual(['candidates', 'maxMembers', 'tradingAccountId']);
    expect(Object.keys(candidate).sort()).toEqual([
      'deploy',
      'evaluation',
      'name',
      'nameExistsOnOtherAccount',
      'strategyId',
      'symbol',
    ]);
    expect(Object.keys(run).sort()).toEqual([
      'firstOpen',
      'hasInstrumentSpec',
      'isCalibrated',
      'lastClose',
      'needsFxBand',
      'profitCurrency',
      'runId',
      'sourceTimeZoneId',
      'symbol',
      'tradeCount',
    ]);
  });

  it('zeroAndFalseValues_AreKeptAsLegalValues', () => {
    expect(run.tradeCount).toBe(0);
    expect(run.hasInstrumentSpec).toBe(false);
    expect(response.candidates[0].evaluation).toBeNull();
  });
});
