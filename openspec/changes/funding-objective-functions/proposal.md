# Proposal: Funding Objective Functions (Darwinex Zero + FTMO)

## Intent

**Problem.** The app stores each destination's rulebook (`funding-guardrails`) and computes generic
analytics (`PortfolioAnalyticsCalculator`), but nothing answers the only question that decides where
a portfolio goes: *how would this group score at this destination?* `MaxLossLimitPct` (FTMO's 10%) is
stored and echoed but **never evaluated anywhere**. Darwinex Zero's Rating — the fully-weighted
objective in `SERVICE_Darwinex_Zero.md` §1 — is not computed at all.

**Why now.** The predecessor `funding-guardrail-shape` just landed the typed rulebook this needs, and
its Question D already settled the layering. Axi entry is ~Nov 2026 and blocked on
`axi-stage-tracking`, so Darwinex Zero and FTMO are the two destinations that can be scored today.

**Success.** Per portfolio, per destination, an honest per-destination readout: a Darwinex Rating
proxy usable to rank the user's own portfolios, and an FTMO 2-Step Swing closed-trade breach
simulation — each carrying its own non-droppable disclosure.

## Scope

### In Scope

1. **Darwinex Zero Rating proxy** — 22% current calendar-month return + 67% current-plus-preceding-5
   calendar months (**geometrically chained**, `Darwinex_Zero_Risk_Model.md` §8) + 11% windowed max
   drawdown. Three weighted components exposed; **no bare `Score`/`Rating` scalar**.
2. **6-calendar-month windowed max drawdown** — new additive `AnalyticsSeries` helper. A **single
   `asOf`-anchored window**, not a rolling family: the Rating is evaluated as of a date.
3. **FTMO 2-Step Swing breach simulation** — daily loss and static total drawdown, with limits read
   from the persisted `Ftmo` `LossLimits` row, never hardcoded.
4. **Contract + endpoint** — Application `IFundingObjectiveScorer`, Infrastructure implementation,
   `GET api/portfolios/{id}/objective-function?service=`.

### Out of Scope

- Axi Select scoring (needs the current-stage pointer from `axi-stage-tracking`).
- Own capital as a destination.
- FTMO **1-Step** — `OneStep`/`Trailing` rows return an explicit unsupported state, never a
  trailing-drawdown simulation.
- **Frontend.** Backend-only; `portfolio-detail` / `risk-limits-modal` are a future home.
- Renaming `DailyBreached` / `DailyHeadroomPct` (see Decision 3).
- The Rating's **track-record bonus** as a number, and any mapping to a 0-100 scale or the vendor's
  75 threshold — both `NOT FOUND`, so both are structurally marked unknown/non-comparable.

## Capabilities

### New Capabilities
- `funding-objective-functions`: per-destination scoring/simulation for Darwinex Zero and FTMO, its
  claim boundaries, and the disambiguation of the existing VaR-derived daily flag.

### Modified Capabilities
- None. Question A is closed: `backtest-portfolio-analytics:251-255` scopes its MUST-NOT-RANK to
  *that* capability's one-group Purpose. The new spec carries a **coexistence requirement** — this
  capability evaluates one caller-named portfolio per call and never loops inside the one-group
  contract. No delta to `backtest-portfolio-analytics`; no delta to `funding-guardrails`.

## Approach

Application-layer `IFundingObjectiveScorer`, implemented in Infrastructure, **consuming**
`PortfolioAnalyticsCalculator` / `AnalyticsSeries` output — return, drawdown and VaR are never
recomputed. **Two structurally distinct DTOs**, because the destinations are not commensurable: a
weighted rating on an uncalibratable scale versus a pass/breach verdict. Every caveat is a **computed
property** on the DTO, reusing the `BreachBasis` precedent (predecessor design D2/D3): a computed
member cannot be omitted at a call site, defaulted wrongly, or contradict the numbers beside it.

| Marker (computed) | On | States |
|---|---|---|
| `RatingComparability = OwnPortfoliosOrdinalOnly` | Darwinex DTO | Scale bounds and points mapping `NOT FOUND`; never vendor-comparable, never 0-100, never vs 75 |
| `TrackRecordBonus = Unknown` | Darwinex DTO | No DARWIN calibration-start date exists. **Never a silent `0`** — a zero reads as "no bonus earned", a different and false claim |
| `DrawdownSamplingBasis = DailyCloseLowerBound` | Darwinex DTO | Darwinex samples every 30s (`Darwinex_Zero_Risk_Model.md` §5.1); a daily-close figure **systematically understates**. Own property — `BreachBasis` is breach-scoped, not drawdown-scoped |
| `EvaluationBasis = ClosedTradeDailyAggregate` | FTMO DTO | May say *"would not have breached on closed-trade daily aggregates"*; **may never say "would have passed"** |
| `DayBoundaryBasis = StoredTimestampDate` | FTMO DTO | FTMO resets 00:00 CE(S)T against the previous day-end **balance**; `BuildDailyNetSeries` buckets on the raw `(CloseTime ?? OpenTime).Date` with **no timezone conversion**. New finding, not in the exploration |

Explicitly **not** reused: `PortfolioService.GetRiskAsync`'s `DailyBreached` /
`DailyHeadroomPct`. Verified at source — they compare a **95th-percentile statistical VaR estimate**
to the limit. That is a risk-posture statement, not a breach; unusable for a breach simulation. The
FTMO simulation is built on `AnalyticsSeries.BuildDailyNetSeries`.

### Rejected alternatives

| Rejected | Reason |
|---|---|
| One generic `Score` / unified DTO | Predecessor Question D forbids it, and the destinations are incompatible in kind |
| Reuse the existing `DailyBreached` predicate | Answers a different question (VaR tail vs single-day loss) |
| Hardcode FTMO 5% / 10% | `funding-guardrails`: the app owns the rulebook **shape**, the operator owns every **number**. Unconfigured ⇒ explicit not-configured state, never a defaulted limit |
| Rolling family of 6-month drawdown windows | The Rating is an as-of figure; a rolling family is unused surface |
| Documenting the caveats in XML docs / UI copy | Droppable at a call site. Computed properties are not |
| Batch/multi-portfolio ranking endpoint | Would create the very ranking loop the coexistence note guarantees against; N calls compose client-side |

### Decisions the exploration left open

**1. Window boundary → CALENDAR month.** The vendor defines the component over *"current +
preceding 5 calendar months"*, and the other two components (22%, 67%) are already calendar-month
based. A fixed-180-day window would make the drawdown component describe a **different period** than
the return components, so the three weighted parts would no longer sum over one window — a
correctness defect, not a cosmetic one. Implementation cost is trivially small
(`asOf.FirstOfMonth().AddMonths(-5)`), so simplicity does not compete. Consequence accepted: the
current month is partial, exactly as it is for the vendor (Rating updates hourly).

**2. Endpoint shape → ONE parameterized endpoint, agreed, with one refinement.** Same id, same member
load, same analytics — two endpoints duplicate the load path and controller wiring for no contract
benefit. Refinement over the exploration: `service` is **required** and typed as the existing
`FundingService` enum, and `Axi`/`Other` are rejected with **400**. Two independently-nullable
payloads whose both-null state is reachable would let an unsupported destination read as "computed,
nothing to report". The response echoes the requested service; exactly one payload is non-null.

**3. Rename `DailyBreached` / `DailyHeadroomPct` → NO, not here.** The rename is right in principle,
but: it is a DTO contract change reaching `portfolio.service.ts`, `portfolio-detail`,
`risk-limits-modal` and their specs, dragging the frontend into a backend-only change; and the
predecessor design chose the computed-`BreachBasis` shape **specifically** to avoid touching those
two fields (D3: *"Wrapping `DailyHeadroomPct`/`DailyBreached` would break `PortfolioServiceRiskTests`"*).
Bundling it here breaks the slicing and the fence for a clarity gain that deserves its own visible
cost. **How the two stay distinguishable:** (a) they never co-occur — the posture fields live on
`ServiceGuardrailDto` under `/risk`, the verdict lives on `FtmoBreachSimulationDto` under
`/objective-function`, so there is no field-name collision at any call site; (b) the new DTO's
computed `EvaluationBasis` names its own series, making the difference structural; (c) the new spec
carries a **disambiguation requirement** stating that the VaR-derived flag is a risk-posture
indicator and MUST NOT be read as a breach verdict; (d) a doc-comment-only clarification is added to
the existing field — zero contract impact. Follow-up change `rename-var-posture-fields` is proposed
in Open Questions.

## Affected Areas

| Layer / Area | Impact | Description |
|---|---|---|
| `Domain/Enums/` | New | `RatingComparability`, `TrackRecordBonusState`, `DrawdownSamplingBasis`, `FtmoEvaluationBasis`, `FtmoDayBoundaryBasis` (siblings of `BreachBasis`) |
| `Domain/Entities`, EF configs, DbContext | **None** | No schema, no entity, no persisted field |
| `Application/Interfaces/IFundingObjectiveScorer.cs` | New | `Task<FundingObjectiveDto> ScoreAsync(Guid portfolioId, FundingService service, DateOnly asOf, CancellationToken)` |
| `Application/DTOs/Portfolios/FundingObjectiveDto.cs` | New | Envelope + `DarwinexZeroRatingProxyDto` + `FtmoBreachSimulationDto`, all markers computed |
| `Infrastructure/Services/FundingObjectiveScorer.cs` | New | Loads member inputs + the `BrokerRiskLimits` row, consumes the calculator, composes |
| `Infrastructure/Services/AnalyticsSeries.cs` | Modified (additive) | `MaxDrawdownPercentInWindow(dense series, windowStart, windowEndInclusive)` |
| `WebAPI/Controllers/PortfoliosController.cs` | Modified | `GET {id:guid}/objective-function`; 400 on unsupported service |
| `WebAPI` DI registration | Modified | One line |
| Angular (`app.trading.algoritmico.web`) | **None** | Out of scope |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Rating proxy read as vendor-comparable | High | Computed `RatingComparability`; no bare scalar; spec requirement forbids the 75 comparison |
| Track-record bonus silently `0` | Med | Non-nullable computed `Unknown` state; RED test asserts it is never `0` |
| FTMO output read as "would have passed" | High | Computed `EvaluationBasis`; spec fixes the permitted wording |
| **Non-determinism from `DateTime.UtcNow` inside the scorer** | Med | `asOf` is an **injected parameter**, never read from the clock inside the computation — otherwise the determinism requirement inherited from `backtest-portfolio-analytics` is violated and tests cannot be byte-identical |
| **`BuildMonthlyReturns` skips months with no trades** | Med | Verified at source: it groups only months that *have* events. The 6-month window must be built over **calendar** months with zero-activity months present, or the 67% chain silently spans the wrong period. Dedicated RED test |
| FTMO day-boundary timezone mismatch | Med | Computed `DayBoundaryBasis`; disclosed, never silently corrected |
| Review budget exceeded | High | Two chained slices (below) |
| Inflating the daily-close drawdown by an assumed factor | Low | Forbidden — no factor is published; `INDEX.md` §5 |

## Migration and Rollback

**No migration is needed.** This change adds no entity, column, EF configuration or seed data; every
input is already persisted (`StrategyTrade`, `BrokerRiskLimits`). There is no data to transform and
none to unwind.

**Rollback** = revert the slice's commits. The new endpoint is purely additive and has no frontend
consumer, so reverting removes an unused route; `AnalyticsSeries` gains a new method that no existing
caller invokes, so removing it cannot regress a shipped path. No API/web co-ordination required.
Slice 2 is revertible independently of slice 1.

## Testing Approach (strict TDD, RED first)

`dotnet test` from `app.trading.algoritmico.api`. Per work unit, RED → GREEN → refactor.

**Slice 1** — (1) windowed drawdown: single window, peak re-seeded at window start, zero-activity
month inside the window, series shorter than the window, all-rising window returns 0;
(2) geometric chaining ≠ summed returns; (3) calendar window excludes month −6 and includes a
zero-trade month; (4) `TrackRecordBonus` is `Unknown`, never `0`; (5) `RatingComparability` and
`DrawdownSamplingBasis` are non-null for every Darwinex result; (6) identical inputs + identical
`asOf` ⇒ byte-identical output.

**Slice 2** — (7) daily loss breach detected from the closed-trade daily series where the VaR-based
predicate does not fire (pins that the two are different questions); (8) static 10% from
`initialCapital`, never ratcheting; (9) missing/null `MaxLossLimitPct` ⇒ not-configured, never a
defaulted 5/10; (10) `OneStep`/`Trailing` row ⇒ unsupported; (11) `EvaluationBasis` and
`DayBoundaryBasis` always present; (12) controller returns 400 for `Axi`/`Other`.

**Regression fence — expected byte-identical, no edits:** `PortfolioAnalyticsCalculatorTests`,
`PortfolioAnalyticsCalculatorLiveOutputRegressionTests`, `PortfolioAnalyticsPrivateCoreTests`,
`BacktestPortfolioAnalyticsAdapterTests`, `BrokerRiskLimitsTests`, `RiskLimitsServiceTests`,
`PortfolioServiceRiskTests`. Backend **607/607, 0 warnings**; frontend **393/393** untouched (no web
file changes). Every change is additive: a new `AnalyticsSeries` method, new DTO/enum files, a new
interface, a new route.

## Sizing and Delivery

My estimate is **higher than the exploration's**: ~370 production + ~460 strict-TDD test lines ≈
**830 changed lines**. The exploration's 250-400 is production-only.

**Agreed: two chained slices, in the exploration's order.**

| Slice | Content | Est. |
|---|---|---|
| 1 | Windowed drawdown helper + Darwinex Rating proxy + `IFundingObjectiveScorer` + endpoint accepting `DarwinexZero` only | ~430 |
| 2 | FTMO 2-Step simulation + widen the endpoint's accepted service | ~400 |

Slice 1 includes the endpoint so it is deliverable end-to-end. **Why this order:** slice 1 front-loads
the higher-uncertainty work — whether a Rating proxy that can never be vendor-compared is useful at
all — so that question is answered before slice 2 is written. Tradeoff acknowledged: the reverse
order would ship the more certain FTMO value first. Slice 1 is still ~430, marginally over budget;
`size:exception` or a further split of the helper is a delivery decision for `sdd-tasks`.

## Dependencies

- Predecessor `funding-guardrail-shape` must be committed (its working-tree changes are still
  uncommitted at the time of writing).
- Blocks nothing. Blocked by nothing.

## Success Criteria

- [ ] A Darwinex Zero readout returns the three weighted components, a non-null comparability marker,
      a non-null drawdown-sampling marker, and `TrackRecordBonus = Unknown`.
- [ ] No API surface exposes a Darwinex `Score`/`Rating` scalar, a 0-100 scale, or a comparison to 75.
- [ ] An FTMO 2-Step readout returns a closed-trade daily-aggregate verdict with `EvaluationBasis`
      and `DayBoundaryBasis` set, using operator-configured limits.
- [ ] An unconfigured FTMO guardrail yields not-configured, never a defaulted 5%/10%.
- [ ] Identical inputs and `asOf` return byte-identical output.
- [ ] Backend 607/607 + new tests, 0 warnings; frontend 393/393 unchanged.
- [ ] No migration file is created.

## Assumptions (flagged)

1. `asOf` defaults to the request date at the **controller**, not inside the computation.
2. "Current calendar month" is taken in the timestamps' own (unconverted) timezone — consistent with
   `BuildDailyNetSeries`, and disclosed rather than corrected.
3. Ranking across portfolios is the caller's composition of N single-portfolio calls.
4. The 11% drawdown component enters the weighted sum as the app's daily-close figure, uncorrected;
   the direction of the error is disclosed, never a factor applied.
5. Exactly one `Ftmo` `BrokerRiskLimits` row exists (predecessor design A4).
6. `initialCapital` is the FTMO "Initial Simulated Capital" for the static 10% base.

## Proposal question round

I could not reach the user directly (sub-agent). These need a human answer; the assumptions above
stand until they are corrected.

1. **Is a Rating proxy that can never be compared to the vendor's 75 threshold worth shipping**, or
   would you rather it were withheld until the points mapping is obtained from Darwinex directly?
   This is the single biggest overbuild risk in the change.
2. Should the Darwinex readout be **suppressed entirely when the window holds fewer than 6 calendar
   months** of history (mirroring the existing 90-day insufficient-history state), or reported with
   an insufficient-history marker?
3. `rename-var-posture-fields` as a **follow-up change** that renames `DailyBreached` /
   `DailyHeadroomPct` and updates the Angular consumers — approve, defer, or drop?
4. For FTMO, is the **first day of the series** treated as day 1 against `initialCapital` (per KB §2),
   even though the series begins at the first trade rather than at account opening?
5. Is `GET api/portfolios/{id}/objective-function` the name you want, or should it read
   `funding-objective`?

> Deviation noted: the phase's 450-word artifact budget was exceeded deliberately — the launch brief
> required rejected alternatives, three resolved decisions with reasoning, affected modules by layer,
> rollback, testing approach and flagged assumptions.
