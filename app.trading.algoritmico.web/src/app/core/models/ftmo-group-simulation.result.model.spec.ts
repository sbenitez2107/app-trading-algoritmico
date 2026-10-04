import { BacktestRunKind } from '../services/backtest.service';
import {
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
  FtmoGroupSimulationRequest,
} from './ftmo-group-simulation.model';
import { FtmoSimulationRefusal, FtmoSimulationStatus } from './ftmo-simulation.model';

/** Numbers copied from `Domain/Enums/FtmoGroupRefusal.cs` (declaration order, no explicit values). */
describe('FtmoGroupRefusal mirror', () => {
  it('invalidRequestIsZero_TheClrDefault', () => {
    expect(FtmoGroupRefusal.InvalidRequest).toBe(0);
  });

  it.each([
    ['SharedInputsRefused', FtmoGroupRefusal.SharedInputsRefused, 1],
    ['MemberNotFound', FtmoGroupRefusal.MemberNotFound, 2],
    ['MemberMissingKind', FtmoGroupRefusal.MemberMissingKind, 3],
    ['MemberRunRefused', FtmoGroupRefusal.MemberRunRefused, 4],
    ['MixedSourceTimeZones', FtmoGroupRefusal.MixedSourceTimeZones, 5],
    ['NoCommonWindow', FtmoGroupRefusal.NoCommonWindow, 6],
    ['MemberHasNoTradesInWindow', FtmoGroupRefusal.MemberHasNoTradesInWindow, 7],
  ])('%sMatchesTheBackendNumber', (_name, actual, expected) => {
    expect(actual).toBe(expected);
  });

  it('hasExactlyTheEightBackendMembers', () => {
    const numeric = Object.values(FtmoGroupRefusal).filter((v) => typeof v === 'number');
    expect(numeric).toHaveLength(8);
  });
});

/**
 * Typed fixtures with NO casts (PR1a lesson): every nested record of `FtmoGroupSimulationDto.cs` and
 * `FtmoGroupDiagnosticsDto.cs` is spelled out, so `tsc` fails when the mirror lacks a field.
 */
describe('ftmo group simulation response mirror', () => {
  it('requestCarriesEveryBodyField', () => {
    const request: FtmoGroupSimulationRequest = {
      memberStrategyIds: ['a'],
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 25,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 0,
      step: 0.01,
      minLot: 0.01,
      maxLots: 10,
    };
    expect(request.sizeDecimals).toBe(0);
  });

  it('envelopeKindResultsMembersAndDiagnosticsAreTyped', () => {
    const dto: FtmoGroupSimulationDto = {
      status: FtmoSimulationStatus.Evaluated,
      refusal: null,
      sharedRefusal: null,
      dailyLossLimitPct: 0.05,
      maxLossLimitPct: 0.1,
      members: [
        {
          strategyId: 'a',
          name: 'Alpha',
          memberOrder: 0,
          profitCurrency: 'EUR',
          sourceTimeZoneId: 'Europe/Berlin',
          fxLow: 1.05,
          fxHigh: 1.15,
        },
      ],
      duplicateIdsRemoved: [],
      duplicateNameWarnings: [{ name: 'Alpha', strategyIds: ['a', 'b'] }],
      unknownStrategyIds: [],
      disclosures: ['x'],
      kinds: [
        {
          kind: BacktestRunKind.Deploy,
          status: FtmoSimulationStatus.Refused,
          refusal: FtmoGroupRefusal.MemberRunRefused,
          memberRefusals: [
            {
              strategyId: 'a',
              name: 'Alpha',
              reason: FtmoGroupRefusal.MemberRunRefused,
              runReason: FtmoSimulationRefusal.RiskNotEstimable,
            },
          ],
          window: { start: '2020-01-01T00:00:00', end: '2021-01-01T00:00:00' },
          coverage: [
            {
              strategyId: 'a',
              name: 'Alpha',
              firstOpen: null,
              lastClose: null,
              inWindowTrades: 0,
            },
          ],
          run: null,
          diagnostics: {
            contributions: [
              {
                strategyId: 'a',
                name: 'Alpha',
                inWindowTrades: 1,
                scalableTrades: 1,
                netLow: 0,
                netHigh: 0,
                raisedToMinimum: 0,
                cappedAtMaximum: 0,
                unscalable: 0,
              },
            ],
            attribution: {
              decidingBreachStarts: 0,
              sharedCloseStarts: 0,
              unattributedStarts: 0,
              members: [
                {
                  strategyId: 'a',
                  name: 'Alpha',
                  phase1Starts: 0,
                  phase2Starts: 0,
                  fundedStarts: 0,
                  soleContributorStarts: 0,
                  sharedCloseStarts: 0,
                },
              ],
            },
            peak: { peakConcurrentOpen: 0, firstReachedSource: null, memberIdsAtPeak: [] },
          },
        },
      ],
    };
    expect(dto.kinds[0].memberRefusals[0].reason).toBe(FtmoGroupRefusal.MemberRunRefused);
  });
});
