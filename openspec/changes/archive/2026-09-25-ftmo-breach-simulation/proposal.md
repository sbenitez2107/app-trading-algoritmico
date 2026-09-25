# Proposal: FTMO 2-Step Swing breach simulation (roadmap layer 1)

> Built on `explore.md` in this folder. Account size 10k (user decision, Engram #2747).
> **This layer ELIMINATES; it never certifies.** See Non-Goals and D1.

## Intent

The user plans to buy a 10k FTMO 2-Step Swing challenge. Today no surface answers "would this
strategy have blown the account?" The only FTMO readout, `DailyBreached`
(`PortfolioService.cs:458-459`), compares VaR95 to the daily limit — it discards exactly the tail
where a breach lives, and its `ClosedTradeLowerBound` label misdescribes it.

Closed trades and missing swap both make a strategy look safer than reality. So a closed-trade
breach is strong evidence to discard a strategy; the absence of one says nothing.

## Scope

### In Scope
1. Per-strategy, per-segment simulation over the resized backtest series of: daily loss (previous
   CE(S)T midnight balance − 5% of initial; day 1 = initial) and static max loss (initial − 10%),
   evaluated after **every close in chronological order** (running intraday balance), not on daily totals.
2. A three-state finding per limit (D1) with the first breach date, the amount, and the margin past the limit.
3. Resize counts (`RaisedToMinimumCount`, `CappedAtMaximumCount`, `UnscalableCount`) on every result.
4. Required caller inputs, never defaulted: FTMO lot grid, initial capital (D5); the source data
   timezone is a persisted per-symbol IANA zone, superseding a caller-declared offset (D4, see
   design.md §Decision 1 and §Corrections).
5. Deprecation of `DailyBreached` and correction of its disclosure (D2).
6. A backend query endpoint (read side, per conventions).
7. A minimal web change so D2's relabel is not silently invisible: the `BreachBasis` label map
   (`portfolio-detail.component.ts:693`) currently returns `''` for any unknown value, so the new
   `VarQuantileComparison` label needs a TS enum member, an EN/ES i18n label, and a CHANGELOG entry.
   This is the only UI work in scope; everything else UI-facing for this capability stays out of
   scope.

### Out of Scope
- Swap modelling. Deferred; trigger: a Darwinex-sourced per-day swap series for the backtest period.
- MT4/MT5 commission-timing correction (the separate change `SourcePlatform` enables). Commission
  itself is not absent (see D6): SQX `Profit` already embeds it, rescaled with the P/L.
- Profit target, minimum days, inactivity, Swing behaviour rules, 1-Step, other services.
- UI, except the one label needed so D2's relabel is not silently invisible (see Scope item 6 and
  `funding-guardrails` delta).

## Non-Goals
- **Never "passed", "safe", "survived", or anything like them.** A no-breach result reads
  "no breach found in closed-trade data — does not show the account would have survived".
- **No portfolio breach.** Daily loss is the worst point on a path, not a sum of daily totals; two
  members under the limit can breach together. Stays in layers 4–6.
- **The existing cross-broker resizer defect stays unfixed.** `BacktestReadService.cs:232-246`
  passes one `grid` to both `TryNormalize` and `TradeResizer.Resize`, and `Bridge` scales P/L by
  `q'/q`, assuming equal money-per-point on both sides. It already misprices the group-risk panel
  whenever the target grid belongs to a different broker than the backtest. This change does not
  fix it; it is a named follow-up. The new FTMO projector (design.md Decision 2) deliberately does
  NOT reuse that path, so it does not inherit the defect.

## Capabilities

### New Capabilities
- `ftmo-breach-simulation`: per-strategy FTMO 2-Step loss-limit elimination over resized backtest trades.

### Modified Capabilities
- `funding-guardrails`: "Closed-Trade Lower-Bound Disclosure" — a VaR-based readout MUST NOT carry
  `ClosedTradeLowerBound`; `DailyBreached` declared a VaR comparison, deprecated.

## Approach — decisions

| # | Decision |
|---|---|
| **D1** | **No boolean.** Enum per limit: `Breached` (strong evidence), `BreachContingent` (breach seen, but another position was open at that moment, or the breaching close is within the DST/offset-ambiguous hour), `NoBreachObserved` (says nothing about survival). The executor's check found cases where the asymmetry fails — overlapping positions and day-boundary errors (`explore.md` §Asymmetry) — so `Breached` requires neither to apply. Overlap comes from `BacktestTrade.OpenTime`, because `DatedNet` drops it. |
| **D2** | **Deprecate `DailyBreached`, do not silently fix it.** The value stays the same; XML/spec mark it as a VaR comparison; its `BreachBasis` label is corrected. A true replacement is not available on that path: `WorstDay` is portfolio-wide and windowed, not per-service (explore correction), and a portfolio-level breach is layer 4–6. Only the label changes, and that is disclosed in the changelog. Layer 1 reads nothing from it. |
| **D3** | **5%/10% come from `BrokerRiskLimits`** (`FtmoProduct = TwoStep`, `DrawdownModel = Static`). The reference-point rule and CE(S)T reset **do not fit that row** (no fields); they are FTMO 2-Step mechanics, encoded in the simulator with a KB citation and applied only when `FtmoProduct = TwoStep`. A null product means the simulation is refused, never assumed. |
| **D4** | **Convert, do not only disclose.** Slice A's disclose-only precedent does not fit: here the day boundary decides the answer. Timestamps: source zone (`Asia/Jerusalem`, IANA, DST-aware) → UTC → `Europe/Berlin` (IANA CE(S)T, DST-aware). **Superseded by design.md**: the source zone is not caller-declared per request; it is a persisted `SourceTimeZoneId` on the new per-SQX-symbol `FtmoInstrumentSpecs` row. The symbol's `_UTC02` suffix is shown only as a hint, never parsed. With exact IANA conversion, only closes whose FTMO day differs from the naive source-minus-1h day, or whose source time is ambiguous/invalid, are uncertain (`BreachContingent`) — not every close inside a DST-mismatch window. |
| **D5** | **No lot-grid default.** FTMO's grid is now known (Engram #2766) but is still a caller/`FtmoInstrumentSpecs`-declared input; `LotGrid.ImoxRetester` is never substituted silently. |
| **D6** | **Superseded by design.md.** Commission is not absent: SQX `Profit` already embeds the configured commission, rescaled with the P/L. Only swap is absent. Both facts — embedded commission, absent swap — are disclosed on every result. |

## Affected Areas

| Area | Impact |
|---|---|
| `Domain/Backtests/` (evaluator, day-boundary clock) | New |
| `Application/DTOs/Backtests/` (result DTO, finding enum) | New |
| `Infrastructure/Services/` (simulation service) | New |
| `Infrastructure/Services/PortfolioService.cs:444-460` | Modified — deprecation + label |
| WebAPI query endpoint | New |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `NoBreachObserved` read as a pass | High | D1 enum, disclosure text, spec scenarios ban pass wording |
| Wrong data offset puts losses on the wrong day | Med | Persisted per-symbol IANA zone (D4), narrowed contingency; Q2 resolved |
| Relabel read as a value change | Med | D2 changelog; the value is pinned by tests |
| Sizing overrun | High | Floors below |

## Rollback Plan
All new code is additive. Revert the four PRs (design.md §Decision 8), in reverse order. D2 is
limited to XML docs and a label, and one revert restores it.

## Dependencies
- An FTMO 2-Step `BrokerRiskLimits` row configured by the user; the user-supplied FTMO lot grid;
  the user-confirmed SQX→FTMO symbol mapping (Engram #2769) and FTMO contract specs (Engram #2766).

## Sizing (floors; recent slices overran ~2×)

Superseded by design.md §Decision 8: four chained PRs, not two. Floors and realistic estimates below
are floors, not targets.

| PR | Content | Floor | Realistic | Budget risk |
|---|---|---|---|---|
| P1 | `FtmoDayClock` + characterization pins | 300 | 550 | High |
| P2 | Projector + evaluator + enums | 450 | 900 | High |
| P3 | `FtmoInstrumentSpec` entity, config, migration + seed, schema tests | 300 | 550 | Med |
| P4 | Read service, DTOs, endpoint, D2 relabel, web label | 450 | 850 | High |

Seam: P1 is pure domain with no I/O; P2 wires it in; P3 adds the persisted spec table; P4 wires the
read service and the D2 relabel (including the one web label). Before a full UI exists, the user
calls the endpoint (Swagger/GraphQL client) per strategy to find the candidates to discard.

## Success Criteria
- [ ] A −8% single day with VaR95 2% yields `Breached`; the old `DailyBreached` stays `false`, now labelled a VaR comparison.
- [ ] The reference point moves with the previous midnight balance; the static floor never moves.
- [ ] Overlap and the DST-ambiguous hour yield `BreachContingent`, never `Breached`.
- [ ] No output contains pass wording; resize counts appear on every result.
- [ ] A missing lot grid, offset or `TwoStep` product causes a refusal, never a default.

## Proposal question round (RESOLVED)
1. **Q1** — RESOLVED: measured 2026-09-24, 0 of 46 backtest runs have any overlapping position; every
   current strategy holds one position at a time. Overlap detection stays live for future strategies
   (MT5 execution, pyramiding) rather than becoming dead code.
2. **Q2** — RESOLVED: the SQX source zone is `Asia/Jerusalem` (IANA, DST-observed), not a fixed offset
   — confirmed at `MEASURED_Demo_vs_Backtest_Divergence.md:98`. This decides D4: convert via
   `Asia/Jerusalem` → UTC → `Europe/Berlin`, both DST-aware.
3. **Q3** — RESOLVED: the user supplied FTMO contract specs for all four instruments (Engram #2766)
   and confirmed the SQX→FTMO symbol mapping (Engram #2769): `XAUUSD_M1_UTC02`→`XAUUSD`,
   `DEUIDXEUR_M1_UTC02`→`GER40.cash`, `USATECHIDXUSD_M1_UTC02`→`US100.cash`,
   `BTCUSD_M1_UTC02`→`BTCUSD` (contract size 1, min 0.01, step 0.01, max 5.00, USD). All four are
   seeded by the `AddFtmoInstrumentSpecs` migration (design.md §Decision 4), pending the user's
   separate authorisation to apply it.
4. **Q4** — RESOLVED: both, reported separately. IS and OOS segments MUST NOT be merged.
