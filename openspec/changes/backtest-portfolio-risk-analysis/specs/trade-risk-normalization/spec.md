# Delta for Trade Risk Normalization

## MODIFIED Requirements

### Requirement: Already-Sized Output Refuses A Non-Unit Weight

`ResizedTradeSeries` carries its own `TargetRiskPerTrade` and MUST NOT be assignable to
`PortfolioMemberInput.Trades`, and MUST expose no conversion to a shape that is. That structural
guarantee is what slice 2a delivers and what its tests assert.

The first such consumer is the `backtest-net-series-bridge` capability's `BacktestNetSeries.Bridge`,
which owns the full obligation — the refusal rule, its three weight scenarios (`1.5`, `1`, `0`),
and its tests — as a consumer-independent guarantee (it holds for any future consumer of an
already-sized series, not only this slice's). That capability's "Already-Sized Output Refuses A
Non-Unit Weight" requirement is the single source of truth for this obligation; it is not
duplicated here, to avoid the two copies drifting apart.

(Previously: the obligation was recorded but not verifiable — no consumer existed in slice 2a.
Now discharged by `backtest-net-series-bridge` and asserted by its tests.)

#### Scenario: The obligation is discharged by the bridge capability
- GIVEN a `ResizedTradeSeries` and a `PortfolioStrategy.Weight != 1`
- WHEN `backtest-net-series-bridge`'s `Bridge` is asked to combine them
- THEN the combination is refused per that capability's non-unit-weight requirement; `Weight` is never multiplied into an already-sized net
