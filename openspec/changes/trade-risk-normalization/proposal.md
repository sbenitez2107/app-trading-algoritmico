# Proposal: Trade Risk Normalization (slice 2a)

## Intent

Slice 1 stores backtest trades and an exact per-symbol point value. Nothing yet answers
*"how many dollars did this trade actually risk?"* — the quantity every later engine slice
(correlation, breach probability, selector) divides by. Measured spread on a 2-decimal export is
$174-202 against a $200 target, so risk must be **read**, never assumed. Only ~27% of trades close
at SL where risk is measurable; the other ~73% must be **bounded, and labelled as bounded**. R goes
first because it is the one engine input verifiable today against data we already hold.

## Scope

### In Scope
- `TradeRiskNormalizer` — per-trade risk basis: `Measured` (SL) | `Bounded` | `Unbounded` | `Unavailable`, each with a `[low, high]` interval and `R = Profit / risk`.
- Risked-amount estimation `Â` **from the run's own SL-closed trades**, by intersecting the per-trade feasible intervals implied by lot quantization — not from the $200 config. Empty intersection = model refuted = refuse, don't emit.
- `TradeResizer` — resize to a target risk on the lot grid (`SizeDecimals`, `MinLot`, `MaxLots=10`), reporting the **achieved** risk dispersion the grid re-introduces.
- `ResizedTradeSeries` — self-describing output carrying `TargetRiskPerTrade` + `LotGrid`, a distinct type that cannot be assigned into `PortfolioMemberInput`.
- Per-run risk profile on the existing backtests read surface (endpoint + expandable row): measured/bounded/unbounded counts, `Â`, R median and spread, resize preview.

### Out of Scope
Correlation · breach probability · Darwinex Zero VaR · prescriptive selector · any change to
`PortfolioStrategy.Weight`, `StrategyTrade`, `TradeImportService`, `PortfolioMemberInput` or the
analytics calculators · persisting R · OOS-window wiring · 1-decimal exports (D15 already bars import).

## Capabilities

### New Capabilities
- `trade-risk-normalization`: risk basis and its provenance, quantization-inversion bounds, lot-grid resizing, the already-sized series contract, and the per-run risk read surface.

### Modified Capabilities
- None. Computation is additive; the new capability owns its own read surface.

## Approach

Risk cancels point value: for an SL trade `SLdist·pv = risk/q`, so both `Â` and every imputed
interval depend only on `Size`, the grid, and measured SL risks. Sizing is `q = round(Â/(SLdist·pv), d)`,
so `risk = Â·q/u` with `u ∈ [q ± step/2]` — inverting the rounding gives a per-trade interval, and
intersecting it over the 90 measured SL trades of `ListOfTrades_XAUUSD_H1_IST.csv` pins `Â` and
**falsifies the sizing model if the intersection is empty**. Round-trip property: resizing to `Â` on
the same grid must reproduce the original sizes exactly.

**Contract decision.** Resized output does NOT enter the existing calculators in this cut.
Rejected permanently: synthesizing `StrategyTrade` (entity misuse — forces `Ticket`, `StrategyId`,
`BaseEntity`, live `Commission`/`Swap`/`Taxes`, and puts backtest rows one `SaveChanges` from live
data). Chosen direction for the *consuming* slice: generalise `PortfolioMemberInput.Trades` to a
projection both entities satisfy. Deferred deliberately — that projection's shape is dictated by
what scoring needs, and nothing here consumes it; doing it now touches a shipped feature for zero
present benefit. This slice fixes the half we can validate: the source shape.

**Placement.** `Infrastructure/Services` (`SymbolPointValueCalibrator` precedent, public static,
stateless); result records in `Application/DTOs/Backtests`. `AnalyticsSeries` is `internal` with
zero `InternalsVisibleTo`, but nothing here needs it.

**OOS wiring: NOT here.** `BacktestRunKind.Deploy` documents itself as answering "sizing,
R-normalization... never anything out-of-sample". Wire `TryGetOosWindow` in **slice 2b (portfolio
scoring / breach probability)**, where the boundary first changes a number a decision depends on.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Infrastructure/Services/TradeRiskNormalizer.cs` | New | Risk basis, `Â` intersection, R |
| `Infrastructure/Services/TradeResizer.cs` | New | Lot-grid resize + achieved-risk report |
| `Application/DTOs/Backtests/` | New | `NormalizedTrade`, `ResizedTradeSeries`, `LotGrid`, `RunRiskProfileDto` |
| `Infrastructure/Services/BacktestReadService.cs` + `BacktestsController` | Modified | One read endpoint |
| `features/sqx/backtests/backtests-list` + i18n | Modified | Expandable risk-profile row |
| Migrations / `StrategyTrade` / portfolios | **Untouched** | No schema change; R is computed on demand |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Two scaling mechanisms coexist — `Weight` applied on top of already-sized data | **High if unguarded** | `ResizedTradeSeries` is a distinct type carrying `TargetRiskPerTrade`+`LotGrid`; not assignable to `IReadOnlyList<StrategyTrade>`; spec requirement that a consumer MUST refuse `Weight != 1` on an already-sized series |
| An imputed interval is read as a measurement | **High** | Basis enum on every trade; intervals, not points; UI shows measured/bounded counts; a bounded trade never reports a bare number |
| `Â` unconstrained (run with zero SL closes) | Med | `Unavailable`, hard refusal — never fall back to the $200 config |
| Rounding rule is not round-half (floor, or a different capital base) | Med | Empty intersection refutes it loudly; both rules tested against the fixture, the supported one named in test output |
| Linear P/L rescaling assumes spread/commission proportional to volume | Med | Stated as an assumption in the spec, not buried; exact for SQX volume-proportional costs |
| Another unwired calculator (the `OosWindow` precedent) | Med | The read surface is in scope precisely to prevent it |
| Two PRs (~600 lines) exceeds the 400-line budget | High | Chain: PR1 calculators + fixture validation, PR2 read surface + UI |

## Rollback Plan

No migration, no persisted R, no change to any existing calculator or entity. Revert the commits —
there is no data to unwind and no consumer to repair. Reverting PR2 alone leaves PR1 dormant but harmless.

## Dependencies

Slice 1 (archived): `BacktestRuns`/`BacktestTrades`, `RealizedRisk` non-defaulted, D5 single-sample-type
guard, D15 excluding 1-decimal exports. Committed fixture `ListOfTrades_XAUUSD_H1_IST.csv` (329 trades,
90 SL-closed, $174-202). IMOX money management: Fixed Amount $200, `Maximum Lots = 10`, 2 size decimals.

## Success Criteria

- [ ] `Â`'s feasible interval is non-empty on the fixture, and the test output names the rounding rule the data supports
- [ ] Round-trip: resizing to `Â` on the same grid reproduces all 329 original sizes
- [ ] Coverage reported: % of the 90 measured SL risks contained by the interval their `Size` alone implies
- [ ] A run with zero SL closes refuses to normalise instead of assuming $200
- [ ] Backend and frontend suites green with **zero** changes to portfolio or live-trade tests
- [ ] The risk profile is visible per run without opening a database

## What This Slice Cannot Claim

Not that non-SL risk is measured (it is bounded). Not that portfolio analytics use R (nothing
consumes it yet). Not that the resized series is broker-executable (slippage, spread, swap and
commission are unmodelled beyond linear volume scaling). Not that R is stable — recalibration and
new runs can move `Â`. Not anything about 1-decimal exports.

## Proposal Question Round

Answers would sharpen the spec; assumptions used in their absence are stated.

1. Is the resize target always the IMOX $200, or operator-supplied per simulation? *(assumed: parameter, defaulting to `Â`)*
2. If `Â` disagrees with the configured $200, trust the data and flag, or refuse the run? *(assumed: trust `Â`, surface the discrepancy)*
3. Zero SL closes — hard refusal, or configured fallback with a loud warning? *(assumed: hard refusal)*
4. Is the lot grid a constant (2 dec / 0.01 / 10) or per-symbol configurable? *(assumed: constant this cut)*
5. Is the per-run UI needed now, or is a validated calculator plus test evidence enough? *(this decides 1 PR vs 2; assumed: needed, to avoid a third unwired API)*
