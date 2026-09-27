# FTMO Breach Simulation Specification (Roadmap Layer 1)

## Purpose

Today the only FTMO-labelled readout, `DailyBreached`, compares VaR95 to the daily loss limit — a
quantile comparison that discards the exact tail where a breach lives (Engram #2746). This capability
adds a per-strategy, per-segment simulation that replays a resized backtest trade series against
FTMO's 2-Step Swing loss rules and reports a three-state finding per limit. It never certifies
survival; a closed-trade series and an unmodelled swap both push toward "looks safer than reality",
so the capability only eliminates candidates, never clears them.

This is a NEW capability, `ftmo-breach-simulation`. It is not modeled as an ADDED delta against
`funding-guardrails`: `funding-guardrails` defines the storage shape of a broker's rulebook
(`BrokerRiskLimits`, `GuardrailKind`, the Risk-tab card) and computes readouts directly from
portfolio member trades. This capability computes a full intraday-boundary replay over a resized
backtest series for a single strategy segment, an evaluation kind `funding-guardrails` has no
requirement describing and does not read from `BrokerRiskLimits` output — it only reads the
`DailyLossLimitPct`/`MaxLossLimitPct` numbers from a `TwoStep` `LossLimits` row as declared inputs.
This mirrors `demo-backtest-cost-decomposition`, which was written as its own capability rather than
an addition to `demo-backtest-comparability` because it introduces a distinct kind of output (a
diagnostic decomposition) built on top of, not inside, the shape the earlier capability defines. The
one piece of `funding-guardrails` this change touches — the `DailyBreached` mislabelling — is captured
as an ADDED delta against `funding-guardrails` itself (see that capability's delta in this change), not
here.

## Requirements

### Requirement: Three-State Breach Finding, No Boolean

Every evaluated limit (daily loss, max loss) MUST produce a finding of exactly one of three states:
`Breached`, `BreachContingent`, or `NoBreachObserved`. No boolean breach flag MUST exist anywhere in
this capability's output. No field, method, comment, or user-facing label produced by this capability
MUST use the words "passed", "safe", "survived", "would have passed", or any equivalent affirming
survival.

> `NoBreachObserved` is silent on survival, not weak evidence of it. Two known errors — replaying
> closed trades instead of a continuous equity path, and omitting swap — both push toward a strategy
> looking safer than it is. An absence of a detected breach under those conditions certifies nothing
> about what a live equity path would have done.

#### Scenario: A single bad day yields Breached, not a VaR-shaped near-miss
- GIVEN a strategy segment whose resized trade series produces one day with a running intraday
  balance drop of 8% of Initial Capital, while its VaR95 across the whole segment is 2%
- WHEN the daily loss limit is evaluated
- THEN the daily finding is `Breached`, independent of any VaR95 value computed over the segment

#### Scenario: No pass-style wording appears anywhere in the result
- GIVEN a produced `FtmoBreachSimulationResult` for any strategy, segment, and limit
- WHEN every field, label, and disclosure string on it is inspected
- THEN none contains "passed", "safe", "survived", "would have passed", or an equivalent
  affirmation of survival

#### Scenario: NoBreachObserved carries its own disclosure text
- GIVEN a daily-loss finding of `NoBreachObserved`
- WHEN the finding is rendered
- THEN its accompanying text states that no breach was found in closed-trade data and does not
  characterize the account as having survived

### Requirement: Daily Loss Evaluated On Running Intraday Balance, Not Daily Totals

The daily loss limit MUST be evaluated after every trade close in chronological order, using the
running balance at that instant, never a single end-of-day total. The reference floor for a given
FTMO trading day MUST be the previous CE(S)T midnight balance minus `DailyLossLimitPct` of Initial
Capital, except day 1, which MUST use Initial Capital as the previous-midnight balance. `FtmoProduct`
MUST be `TwoStep` on the supplied `BrokerRiskLimits` row; a null or non-`TwoStep` product MUST cause
the simulation to be refused, never assumed.

> A flat "5% of Initial Capital every day" is wrong from day 2 onward: FTMO's own reference point
> moves with the previous day's closing balance, not a fixed fraction of the starting capital
> (`SERVICE_FTMO.md:62-63`, confirmed against the official FTMO wording). Encoding a fixed daily floor
> would silently pass a day that FTMO would have failed once the account has drawn down, and fail a
> day FTMO would have passed once the account has grown.

#### Scenario: Reference floor moves with the previous day's balance
- GIVEN a segment where day 1 closes with a balance 2% above Initial Capital, and day 2 loses 5% of
  Initial Capital intraday
- WHEN the daily loss limit is evaluated for day 2
- THEN the day-2 floor is (day-1 closing balance) minus 5% of Initial Capital, not Initial Capital
  minus 5%, and the day-2 finding reflects a floor higher than a flat Initial-Capital-based floor
  would produce

#### Scenario: A flat "5% of initial every day" implementation would fail this requirement
- GIVEN the same two-day segment as above, evaluated instead under a hypothetical implementation
  that always floors at Initial Capital minus 5%
- WHEN day 2's finding is compared against this requirement's floor
- THEN the two floors differ, demonstrating that a fixed-fraction-of-initial daily floor does not
  satisfy this requirement

#### Scenario: Day 1 uses Initial Capital as the reference balance
- GIVEN a segment's very first FTMO trading day
- WHEN the daily loss limit is evaluated for that day
- THEN the floor is Initial Capital minus `DailyLossLimitPct` of Initial Capital

#### Scenario: Every close in chronological order is checked, not only the day's last close
- GIVEN a day with three closes where the second close's running balance breaches the daily floor
  and the third close's running balance recovers above it
- WHEN the daily loss limit is evaluated for that day
- THEN the finding reflects the breach detected at the second close, not the recovered balance at
  the third close

#### Scenario: A non-TwoStep or missing product refuses evaluation
- GIVEN a `BrokerRiskLimits` row with `FundingService = Ftmo` and `FtmoProduct` null or `OneStep`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with an explicit reason, and no finding is produced

### Requirement: Max Loss Evaluated As A Static Floor From Initial Capital

The max loss limit MUST be a static floor computed once as Initial Capital minus `MaxLossLimitPct` of
Initial Capital, and MUST NOT move as the account's balance changes over the segment. It MUST be
evaluated on the same running intraday balance, after every close in chronological order, as the
daily loss limit.

#### Scenario: Max loss floor does not move after a profitable day
- GIVEN a segment where day 1 closes 5% above Initial Capital
- WHEN the max loss floor is evaluated on day 2
- THEN the floor remains Initial Capital minus `MaxLossLimitPct` of Initial Capital, unchanged from
  day 1

### Requirement: Overlapping Positions Downgrade A Detected Breach To Contingent

The simulator MUST detect whether another position was open at the instant a detected breach occurred,
by reading `BacktestTrade.OpenTime` alongside close times, because the net series alone drops open
time. When a detected breach occurs while another position was open, the finding for that limit MUST
be `BreachContingent`, never `Breached`.

> Measured 2026-09-24: 0 of 46 backtest runs contain a trade whose open time precedes the previous
> trade's close time — every current strategy holds one position at a time. Overlap detection stays a
> live requirement rather than dead code because future strategies (MT5 execution, pyramiding) may
> overlap, and a closed-trade breach is only genuine evidence when no other position's floating P/L
> could have kept real equity above the floor at that instant.

#### Scenario: No overlap in current data still yields Breached
- GIVEN a strategy segment where a detected daily-loss breach occurs and no other trade's open time
  precedes the breaching trade's close time
- WHEN the finding is produced
- THEN the finding is `Breached`

#### Scenario: A breach coinciding with an open second position is downgraded
- GIVEN a strategy segment where a detected daily-loss breach occurs while a second trade's open
  time precedes and close time follows the breaching close
- WHEN the finding is produced
- THEN the finding is `BreachContingent`, not `Breached`

### Requirement: An Excluded Unscalable Trade Downgrades Only The Breaches It Can Affect

An `Unscalable` trade contributes no P/L to the replayed balance. A detected max-loss breach at any
close after an `Unscalable` close MUST be `BreachContingent` with cause `UnscalableTradeExcluded`. A
detected daily-loss breach MUST carry that cause only when the `Unscalable` close falls on the same
FTMO trading day as the breach, at or before it; a daily-loss breach on a later FTMO day MUST NOT be
downgraded on account of it.

> The excluded trade's P/L is missing from every later balance by the same amount. The max-loss floor
> is static, so that offset shifts every later comparison against it. The daily reference is the
> replayed balance at the previous FTMO midnight, so on any later day the balance and its reference
> both lack the same amount and `balance < reference − daily%·initial` is unchanged. Downgrading every
> later daily breach instead would let one unscalable trade weeks earlier stop the simulator from
> ever eliminating a strategy on the daily limit.

#### Scenario: A same-day daily breach after an unscalable close is contingent
- GIVEN an `Unscalable` trade that closes on an FTMO day, and a later close on the same FTMO day that
  breaches the daily-loss floor
- WHEN the daily finding is produced
- THEN it is `BreachContingent` with cause `UnscalableTradeExcluded`

#### Scenario: A later-day daily breach stays Breached
- GIVEN an `Unscalable` trade that closes on one FTMO day, and a clean daily-loss breach on a later
  FTMO day
- WHEN the daily finding is produced
- THEN it is `Breached`

#### Scenario: A max-loss breach any time after an unscalable close is contingent
- GIVEN an `Unscalable` trade that closes on one FTMO day, and a max-loss breach on a later FTMO day
- WHEN the max-loss finding is produced
- THEN it is `BreachContingent` with cause `UnscalableTradeExcluded`

### Requirement: Day-Boundary Conversion Uses A Persisted IANA Source Zone, Never A Fixed Offset

Timestamp-to-FTMO-day conversion MUST use the source data's timezone as a persisted IANA zone —
`Asia/Jerusalem` with DST observed, for the symbols this capability supports — read from the
per-SQX-symbol `FtmoInstrumentSpec.SourceTimeZoneId`, convert to UTC, then to `Europe/Berlin` (IANA,
DST-aware) to determine the FTMO trading day. The source offset MUST NOT be hardcoded as a fixed
`+02:00`, and MUST NOT be supplied as a caller-declared request offset; the `_UTC02` suffix in a
symbol name MUST be treated only as a display hint, never parsed as the authoritative offset.

> `Asia/Jerusalem` is confirmed as the SQX Data Manager's recorded source zone (with DST) —
> `MEASURED_Demo_vs_Backtest_Divergence.md:98`. Because both `Asia/Jerusalem` and `Europe/Berlin`
> observe DST, the offset between them is a near-constant one hour year-round (FTMO midnight = 01:00
> data time), not the "coincide in summer, drift in winter" pattern a fixed-`+02:00` assumption would
> produce. The two zones change DST on different calendar dates, so for the days between Israel's and
> the EU's DST transitions (late March, late October) the effective offset becomes 0h or 2h instead of
> 1h. With exact IANA conversion, a close inside that mismatch window is still correctly attributed
> most of the time; only a close whose exact FTMO day differs from the naive `(source − 1h).Date`
> day, or whose source timestamp is itself ambiguous or invalid under `Asia/Jerusalem`, is uncertain.

#### Scenario: A close near FTMO midnight is bucketed by the converted day, not the raw data date
- GIVEN a trade closing at 00:30 in `Asia/Jerusalem` data time, outside any DST transition window
- WHEN its FTMO trading day is determined
- THEN it is assigned to the FTMO day ending at that instant under `Europe/Berlin` time (the
  previous FTMO day relative to the raw data calendar date), reflecting the near-constant one-hour
  offset

#### Scenario: A close whose exact day differs from the naive day is contingent
- GIVEN a breaching close inside a window where `Asia/Jerusalem` and `Europe/Berlin` have
  transitioned DST on different calendar dates, and the exact FTMO day computed via full IANA
  conversion differs from the naive `(source − 1h).Date` day
- WHEN the finding for that breach is produced
- THEN the finding is `BreachContingent` with cause `DstMismatchWindow`, not `Breached`

#### Scenario: A close inside the mismatch window whose exact day matches the naive day is not downgraded on that account
- GIVEN a close inside the same DST-mismatch window, where the exact FTMO day computed via full IANA
  conversion agrees with the naive `(source − 1h).Date` day, and the source timestamp is neither
  ambiguous nor invalid
- WHEN the finding for a breach at that close is produced
- THEN the finding is not downgraded to `BreachContingent` on account of the mismatch window alone

#### Scenario: An ambiguous or invalid source timestamp is contingent
- GIVEN a close whose `Asia/Jerusalem` source timestamp is ambiguous (fall-back) or invalid
  (spring-forward)
- WHEN the finding for a breach at that close is produced
- THEN the finding is `BreachContingent` with cause `AmbiguousSourceTime` or `InvalidSourceTime`
  respectively, not `Breached`

#### Scenario: A fixed +02:00 offset would fail this requirement
- GIVEN the same close as the near-midnight scenario above, evaluated under a hypothetical
  implementation that hardcodes a fixed `+02:00` source offset instead of the persisted IANA
  `Asia/Jerusalem` zone
- WHEN the resulting FTMO-day assignment is compared against this requirement's declared conversion
- THEN the two assignments differ during at least part of the year, demonstrating that a fixed
  offset does not satisfy this requirement

#### Scenario: A caller-supplied offset would fail this requirement
- GIVEN a request that attempts to supply its own source UTC offset instead of relying on the
  persisted `FtmoInstrumentSpec.SourceTimeZoneId`
- WHEN the simulation processes the request
- THEN the caller-supplied offset is not used for day-boundary conversion; the persisted zone is
  used instead

### Requirement: Rescaling By Money-Per-Point, Never By Lot Count

Rescaling a backtest's trade series onto an FTMO account size MUST be performed by money-per-point
(the FTMO contract's point value), never by scaling the backtest's lot count directly. The FTMO point
value for the symbol being simulated MUST be a caller-declared input, never inferred or defaulted from
the backtest's own `SymbolCalibrations` point value. When no FTMO point value is available for a
symbol, the simulator MUST refuse to produce a verdict for that symbol rather than approximate one.

> Measured (Engram #2766): the backtest calibration point value for `DEUIDXEUR_M1_UTC02` is 10; FTMO's
> `GER40.cash` contract size is 1 — a 10x mismatch. Rescaling by lot count alone would carry that 10x
> error directly into the simulated P/L. `XAUUSD` happens to match at 100, but `USATECHIDXUSD_M1_UTC02`
> (NQ) has calibration status `Inconsistent` with a null point value, so no safe inference is possible
> for it at all.

#### Scenario: DAX rescaling uses the declared FTMO point value, not the backtest calibration
- GIVEN a `GER40.cash` simulation with a declared FTMO point value of 1, while the backtest's own
  `DEUIDXEUR_M1_UTC02` calibration point value is 10
- WHEN the trade series is rescaled onto the FTMO account
- THEN the rescaled P/L is computed using the declared point value of 1, not the backtest's
  calibration value of 10

#### Scenario: NQ simulation is refused when no FTMO point value is supplied
- GIVEN a request to simulate `US100.cash` (backed by `USATECHIDXUSD_M1_UTC02`, calibration status
  `Inconsistent`, point value null) with no FTMO point value declared by the caller
- WHEN the simulation is requested
- THEN it is refused with an explicit reason, and no finding is produced for that symbol

### Requirement: Currency Conversion Is A Declared Band, Evaluated At Both Ends, Never Silent Or Hardcoded

When a symbol's FTMO contract settles in a currency different from the account's currency, the
simulator MUST NOT compare that symbol's P/L directly against the account's loss limits without an
explicit, caller-declared FX band (`fxLow`, `fxHigh`, USD per unit; a degenerate band where
`fxLow == fxHigh` is allowed). The simulator MUST NOT apply any hardcoded or embedded single exchange
rate. The projector and evaluator MUST run once at `fxLow` and once at `fxHigh`. If the two runs
produce different verdicts for a limit, the finding MUST be `BreachContingent` with cause
`FxRoundingSensitive`. When no FX band is supplied for a currency mismatch, the simulator MUST refuse
to produce a verdict for that symbol with reason `FxRateNotDeclared`, rather than silently treating
the mismatched-currency P/L as if it were already in the account currency. The declared band MUST be
echoed on the result.

> `GER40.cash` settles in EUR; `XAUUSD`, `US100.cash`, and `BTCUSD` settle in USD (Engram #2766). A USD
> FTMO account's loss limits are in USD. Comparing EUR losses to a USD limit without conversion silently
> mixes units. Because sizing targets USD risk, FX nearly cancels in the projector's own arithmetic
> (`net' ≈ Profit·target/Â`) and survives only through the lot-grid floor and min/max clamps — so a
> single point-in-time rate would not distort the P/L, but it would hide the rounding sensitivity a
> band exposes. A band, evaluated at both ends, surfaces exactly that sensitivity instead of masking
> it with one chosen rate.

#### Scenario: A EUR-settling symbol without a supplied FX band is refused
- GIVEN a `GER40.cash` simulation against a USD-denominated account, with no FX band supplied by the
  caller
- WHEN the simulation is requested
- THEN it is refused with reason `FxRateNotDeclared`, and no finding is produced

#### Scenario: A same-currency symbol needs no FX band
- GIVEN an `XAUUSD` simulation against a USD-denominated account
- WHEN the simulation is requested
- THEN no FX band is required and the simulation proceeds

#### Scenario: A EUR-settling symbol with an agreeing band produces a clean verdict
- GIVEN a `GER40.cash` simulation with a declared FX band where both `fxLow` and `fxHigh` produce the
  same breach verdict for a limit
- WHEN the finding is produced
- THEN the finding reflects that verdict directly, without a `FxRoundingSensitive` cause

#### Scenario: A EUR-settling symbol with a disagreeing band is FX-contingent
- GIVEN a `GER40.cash` simulation with a declared FX band where `fxLow` yields `NoBreachObserved` and
  `fxHigh` yields a clean breach for the same limit
- WHEN the finding is produced
- THEN the finding is `BreachContingent` with cause `FxRoundingSensitive`, and the declared band is
  echoed on the result

### Requirement: FTMO Symbol Mapping Is Declared, Never Inferred

The mapping from an FTMO contract symbol (e.g. `GER40.cash`, `US100.cash`) to the backtest data
symbol it corresponds to (e.g. `DEUIDXEUR_M1_UTC02`, `USATECHIDXUSD_M1_UTC02`) MUST be a caller-declared
input. The simulator MUST NOT infer this mapping from string similarity, substring matching, or any
other heuristic.

#### Scenario: Simulation without a declared symbol mapping is refused
- GIVEN a simulation request for an FTMO symbol with no declared backtest-symbol mapping supplied
- WHEN the simulation is requested
- THEN it is refused with an explicit reason, and no string-similarity match is attempted or used

### Requirement: FTMO Lot Grid Is A Required Caller Input

The FTMO minimum lot, lot step, and maximum lot per symbol MUST be caller-declared inputs. The
simulator MUST NOT substitute any other lot grid (including `LotGrid.ImoxRetester`) as a default when
the FTMO grid is not supplied.

#### Scenario: Simulation without a declared lot grid is refused
- GIVEN a simulation request for a symbol with no FTMO lot grid supplied
- WHEN the simulation is requested
- THEN it is refused with an explicit reason, and no default lot grid is substituted

### Requirement: Resize Counts Travel With Every Result

Every produced result MUST report `RaisedToMinimumCount`, `CappedAtMaximumCount`, and
`UnscalableCount` from the resized trade series used to produce it, regardless of the finding states
of its limits.

> At a 10k account the daily loss limit is US$500 and the static max loss is US$1,000
> (Engram #2747) — the tightest floors among account sizes considered, where minimum-lot pinning on
> the FTMO grid bites hardest. A strategy whose backtest lots were resized up to the FTMO minimum may
> carry materially different risk than its original backtest, and that fact must be visible beside
> every verdict, not absorbed into it.

#### Scenario: Resize counts appear even on a NoBreachObserved result
- GIVEN a segment whose resize produced two trades raised to the FTMO minimum lot and zero capped or
  unscalable trades, with no breach detected
- WHEN the result is produced
- THEN it reports `RaisedToMinimumCount = 2`, `CappedAtMaximumCount = 0`, `UnscalableCount = 0`
  alongside the `NoBreachObserved` findings

### Requirement: In-Sample and Out-of-Sample Segments Reported Separately

When a strategy has both in-sample (IS) and out-of-sample (OOS) backtest segments, the simulation
MUST produce and report a distinct result for each segment. Results from different segments MUST NOT
be merged, averaged, or blended into a single combined finding.

> Blending IS and OOS results would hide exactly the difference the user needs to see: whether a
> strategy's IS performance (which the strategy was fit to) generalizes to OOS data it was not fit to.
> A blended result could show `NoBreachObserved` while the OOS segment alone breached.

#### Scenario: IS and OOS produce separate results for the same strategy
- GIVEN a strategy with both an IS run and an OOS run
- WHEN the simulation is requested for that strategy
- THEN two distinct results are produced, one per segment, each with its own findings and resize
  counts

### Requirement: Swap Is Not Modelled, Commission Is Embedded Not Absent — Both Disclosed On Every Result

The simulation MUST NOT model swap or overnight financing cost, and MUST NOT model the MT4/MT5
commission-timing (commission-day) correction. Every produced result MUST carry an explicit
disclosure that swap is not modelled, and a separate explicit disclosure that the source backtest's
`Profit` figure already embeds the configured commission — rescaled together with the P/L during
resizing, not absent from it — regardless of its findings.

> Swap omission pushes toward "looks safer than reality", compounding with the closed-trade
> limitation. Gold swap was measured at 16.7% of gross P/L in a related analysis, so the omission is
> not negligible for weekend-holding strategies. Commission is a different case: SQX `Profit` already
> includes the configured commission (`MEASURED_Demo_vs_Backtest_Divergence.md` §6), so it is not an
> unmodelled cost the way swap is — it is a modelled-but-rescaled one, and disclosing it as "absent"
> would misstate what the number already contains.

#### Scenario: Swap disclosure appears on every result
- GIVEN any produced result, regardless of its findings
- WHEN the result is inspected
- THEN it carries an explicit statement that swap is not modelled

#### Scenario: Embedded-commission disclosure appears on every result
- GIVEN any produced result, regardless of its findings
- WHEN the result is inspected
- THEN it carries an explicit statement that the source backtest's commission is embedded in
  `Profit` and rescaled with the P/L, not absent

### Requirement: A Missing Or Out-Of-Range Loss-Limit Percentage Refuses The Run

A `LossLimits` row supplied for a simulation MUST have both `DailyLossLimitPct` and
`MaxLossLimitPct` present and expressed as fractions in `(0, 1]`. When either percentage is null, or
present but outside `(0, 1]` (zero, negative, or above 1), the simulation MUST be refused with
`FtmoSimulationRefusal.LimitsNotConfigured`, the same reason used when no `BrokerRiskLimits` row
exists for the broker at all, and no finding MUST be produced.

> A null percentage read as 0% would put the max-loss floor at Initial Capital and report every
> closed loss as a false `Breached`; a negative or above-1 value is equally not a usable rule. Both
> are properties of the stored configuration, never of the request, so they refuse exactly like a
> missing row rather than being silently substituted or approximated.
> `RiskLimitsService.ValidateKindFields` does not require `DailyLossLimitPct`/`MaxLossLimitPct` to be
> present or bounded when persisting a `LossLimits` row — that upstream write path is deliberately
> unchanged by this capability, so the simulation guards itself at read time instead of relying on a
> write-time invariant that does not exist yet.

#### Scenario: Null daily percentage refuses the run
- GIVEN a `LossLimits` row with `DailyLossLimitPct = null` and a valid `MaxLossLimitPct`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with `LimitsNotConfigured`, and no finding is produced

#### Scenario: Null max percentage refuses the run
- GIVEN a `LossLimits` row with `MaxLossLimitPct = null` and a valid `DailyLossLimitPct`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with `LimitsNotConfigured`, and no finding is produced

#### Scenario: A zero percentage refuses the run
- GIVEN a `LossLimits` row with `DailyLossLimitPct = 0` and a valid `MaxLossLimitPct`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with `LimitsNotConfigured`, and no finding is produced

#### Scenario: A negative percentage refuses the run
- GIVEN a `LossLimits` row with `MaxLossLimitPct = -0.05` and a valid `DailyLossLimitPct`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with `LimitsNotConfigured`, and no finding is produced

#### Scenario: A percentage above 1 refuses the run
- GIVEN a `LossLimits` row with `DailyLossLimitPct = 1.5` and a valid `MaxLossLimitPct`
- WHEN a breach simulation is requested against it
- THEN the simulation is refused with `LimitsNotConfigured`, and no finding is produced

#### Scenario: A percentage of exactly 1 is accepted and evaluated
- GIVEN a `LossLimits` row with `DailyLossLimitPct = 1` and `MaxLossLimitPct = 1`, both otherwise
  valid
- WHEN a breach simulation is requested against it
- THEN the run proceeds to evaluation and is not refused with `LimitsNotConfigured` on account of
  either percentage

### Requirement: Simulation Is Per-Strategy Only, Never Portfolio-Level

The simulation MUST evaluate one strategy segment at a time and MUST NOT aggregate, sum, or otherwise
combine loss across multiple strategies into a single portfolio-level breach finding.

> A daily loss limit is the worst point on a path, not a sum of daily totals; two strategies each
> individually under the limit can, combined, breach together. Detecting that requires per-instant
> portfolio equity, which is out of scope for this capability (deferred to a future portfolio-level
> layer).

#### Scenario: Multiple strategies are evaluated independently
- GIVEN two strategies, each individually evaluated with `NoBreachObserved` on their daily loss limit
- WHEN a combined portfolio-level daily loss finding is requested
- THEN no such combined finding is produced by this capability

### Requirement: First-Breach Timing Is Additive, Never A Truncation
The simulator MUST continue to replay the full trade series for every limit, exactly as today. Adding
first-breach timing MUST NOT change `Verdict`, `Causes`, or `DisclosureText` for any limit, for any
run, in any way. Truncating the replay at a limit's first breaching close MUST NOT be implemented.

> A first breach can be `BreachContingent` — possibly not a real breach. Truncating the replay there
> would hide a later clean breach and would silently change a shipped readout that callers already
> depend on. Timing is read alongside the verdict, never a substitute computed by stopping early.

#### Scenario: A verdict, its causes, and its disclosure text are unchanged by adding timing
- GIVEN a breaching fixture that produces a contingent daily breach followed by a clean daily breach,
  plus a disagreeing FX band on the max-loss limit
- WHEN the simulation is run before and after first-breach timing is added
- THEN `Verdict`, `Causes`, and `DisclosureText` for every limit are byte-identical between the two
  runs

#### Scenario: The full series is still replayed after a first breach
- GIVEN a strategy segment whose daily limit is first breached (contingently) in month 2, and whose
  daily limit is breached cleanly in month 5
- WHEN the simulation is run
- THEN the daily finding reflects the month-5 clean breach in `Verdict`/`Causes`, not only the month-2
  contingent one, and both breaches are discoverable through the limit's first-breach and
  first-clean-breach timing

### Requirement: Per-Limit First-Breach And First-Clean-Breach Timing
Every evaluated limit (daily loss, max loss) MUST report its first breaching close and its first
*clean* breaching close as two independent, separately-nullable timing records. Each present record
MUST carry: the source close time, the FTMO trading day the floor bookkeeping used for that close, the
balance after that close, the floor it fell below, whether that specific close was clean or
contingent, that close's own causes (not the causes attached to any other close, and not the causes
the run-level `Verdict` carries), elapsed calendar days and elapsed FTMO trading days from the
replay-start anchor, and which FX band end produced it.

A limit that is never breached MUST report both timing records as null. A limit whose first breach is
already clean MUST report the same close for both records. Null timing MUST NOT be read, labelled, or
disclosed as evidence the limit was never approached — it states only that a breaching close was not
detected in the replayed data, consistent with the capability's existing `NoBreachObserved` disclosure.

> The shipped `BuildFinding` discards the first breach's own causes when the run-level verdict ends up
> `Breached` (it passes `[]` instead) — that discard is a property of the run-level `Causes` field, and
> it must not leak into this per-close timing record, which needs the first breaching close's actual
> causes to be readable on its own.

#### Scenario: A breaching limit reports full timing on its first breach
- GIVEN a daily-loss limit whose first breaching close is a contingent breach at a known time,
  balance, and floor
- WHEN the finding is produced
- THEN the daily limit's `FirstBreach` record reports that close's source time, FTMO trading day,
  balance after close, floor, `Contingent` classification, that close's own causes, elapsed calendar
  and FTMO trading days from the anchor, and the FX band end that produced it

#### Scenario: A first breach that is contingent has a distinct first clean breach
- GIVEN a daily-loss limit whose first breaching close is contingent, and a later close that is a
  clean breach
- WHEN the finding is produced
- THEN `FirstBreach` reports the earlier contingent close and `FirstCleanBreach` reports the later
  clean close, as two distinct records

#### Scenario: A first breach that is already clean is reported once, in both fields
- GIVEN a daily-loss limit whose first breaching close is itself clean
- WHEN the finding is produced
- THEN `FirstBreach` and `FirstCleanBreach` both report that same close

#### Scenario: A never-breached limit reports null timing on both fields
- GIVEN a max-loss limit with `NoBreachObserved`
- WHEN the finding is produced
- THEN `FirstBreach` and `FirstCleanBreach` are both null, and neither is rendered or disclosed as
  evidence the account survived

#### Scenario: A first breach's own causes are not the run-level discarded causes
- GIVEN a daily-loss limit whose first breaching close carries cause `UnscalableTradeExcluded`, and
  whose run-level `Verdict` is `Breached` with `Causes = []` (per the shipped discard behaviour)
- WHEN the finding is produced
- THEN the limit's `FirstBreach` record reports `UnscalableTradeExcluded` as that close's own cause,
  independent of the run-level `Causes` being empty

### Requirement: Per-Run First-Limit-To-Break
Every produced run result MUST report which limit's first breach occurred earliest: `Daily`, `Max`, or
`BothSameClose` when the daily limit's and the max limit's first breaching closes are the same close.
Sameness MUST be detected by identifying the same underlying row in the replayed series, never by
comparing timestamps alone. The run MUST NOT pick one of the two limits when they tie on the same
close. A run where neither limit is breached MUST report this field as null.

> Two closes can carry an identical source timestamp without being the same close (for example,
> distinct rows produced by resizing or by FX-band evaluation). Comparing timestamps alone risks a
> false tie or a missed one; comparing the identifying row is the only way to detect a genuine
> same-close tie.

#### Scenario: The daily limit breaks first
- GIVEN a run where the daily limit's first breach occurs before the max limit's first breach
- WHEN the run result is produced
- THEN the first-limit-to-break field reports `Daily` with that breach's timing

#### Scenario: Both limits break on the same close
- GIVEN a run where the daily limit's first breaching close and the max limit's first breaching close
  are the same underlying row
- WHEN the run result is produced
- THEN the first-limit-to-break field reports `BothSameClose`, and neither `Daily` nor `Max` is
  reported alone

#### Scenario: Identical timestamps on different rows are not treated as a tie
- GIVEN a run where the daily limit's first breach and the max limit's first breach carry the same
  source close time but come from different rows of the replayed series
- WHEN the run result is produced
- THEN the first-limit-to-break field reports the limit whose row is actually earliest (or, if the
  rows are genuinely distinct and not the same row, does not report `BothSameClose`)

#### Scenario: Neither limit is breached
- GIVEN a run where both the daily and max limits are `NoBreachObserved`
- WHEN the run result is produced
- THEN the first-limit-to-break field is null

### Requirement: Elapsed Time Is Measured From The Replay-Start Anchor, Echoed On The Result
Every produced run result MUST declare a replay-start anchor: the FTMO trading day of the first
replayed trade's OPEN time. Every first-breach and first-clean-breach timing record MUST report
elapsed time from that anchor in two units: calendar days (the breach's FTMO trading day minus the
anchor day) and FTMO trading days (the count of distinct FTMO trading days, from the anchor through
the breach day inclusive, that contain at least one replayed close; no holiday calendar is applied).
The anchor itself MUST be echoed on the run result. A refused run MUST NOT report an anchor or any
elapsed time.

> The replay starts at the backtest's first trade, so a reported breach date means "an account opened
> on the anchor date lasted N days" — it is not a property of the strategy in isolation. A breach
> falling in March 2016, when the backtest happens to start in January 2016, is an artefact of that
> start date, not a fact about the strategy's risk. Reporting elapsed time next to the date, and
> echoing the anchor itself, is how a reader distinguishes "when" from "how long", and is the reason
> this change does not attempt a rolling-start survival distribution (see Non-Goals).

> The anchor uses the first trade's OPEN, not its close: capital is at risk from the moment a position
> is opened, not from the moment it first closes. Using the close would understate elapsed time by
> the duration of the first open trade.

#### Scenario: The anchor is the first trade's open, not its close
- GIVEN a segment whose first trade opens on FTMO trading day D and closes on FTMO trading day D+2
- WHEN the run result is produced
- THEN the replay-start anchor is FTMO trading day D

#### Scenario: Elapsed FTMO trading days counts only days with a replayed close
- GIVEN a replay-start anchor of day D, and a first breaching close on FTMO trading day D+10, where
  the replayed series contains closes on 7 distinct FTMO trading days between D and D+10 inclusive
- WHEN the breach's elapsed FTMO trading days is computed
- THEN it reports 7, not 10, and no holiday calendar is consulted

#### Scenario: Calendar days elapsed is a plain date difference
- GIVEN a replay-start anchor of FTMO trading day D, and a first breaching close on FTMO trading day
  D+45
- WHEN the breach's elapsed calendar days is computed
- THEN it reports 45

#### Scenario: The anchor is echoed on every non-refused result
- GIVEN any run result that was not refused
- WHEN the result is inspected
- THEN it reports the replay-start anchor's source time and its FTMO trading day

#### Scenario: A refused run reports no anchor and no elapsed time
- GIVEN a simulation request that is refused (for example, `LimitsNotConfigured`)
- WHEN the result is inspected
- THEN no replay-start anchor is reported, and no first-breach or first-clean-breach timing is
  reported for any limit

### Requirement: FX-Band Timing Reports The Earliest Point And Names The Producing End
When a symbol requires an FX band, first-breach and first-clean-breach timing MUST be computed once
per FX band end (`fxLow`, `fxHigh`), and the reported timing record for each field MUST be the
earliest point across both ends, ordered by source close time and then by row when close times tie.
The record MUST name which end produced it: `FxLow`, `FxHigh`, or `BothEnds` when both ends' earliest
breaching closes are the same underlying row. This applies independently of whether the two ends agree
on the limit's verdict. The existing rule that keeps only `fxLow`'s causes on the run-level `Causes`
field when both ends agree on the verdict MUST remain unchanged; FX-band timing is merged separately
from that verdict-level merge.

> The FX projector's lot-grid floor and min/max clamps can shift balances differently between the two
> band ends even when both ends land on the same verdict, so the two ends can produce different
> first-breach points while still agreeing that the limit was (or was not) breached. Reporting the
> earliest point, tagged with the end that produced it, surfaces that divergence without doubling the
> reported shape for the dominant case (same-currency symbols, where the band is degenerate and both
> ends necessarily agree).

#### Scenario: Both FX ends agree on the verdict but produce different first-breach points
- GIVEN a `GER40.cash` simulation whose `fxLow` end and `fxHigh` end both yield `Breached` on the max
  limit, but at different closes due to lot-grid rounding
- WHEN the max limit's `FirstBreach` timing is produced
- THEN it reports the earlier of the two ends' first-breach closes, and names the end that produced it

#### Scenario: FX ends producing the same first-breach row report BothEnds
- GIVEN a `GER40.cash` simulation whose `fxLow` and `fxHigh` first-breach points fall on the same
  underlying row of the replayed series
- WHEN the timing record is produced
- THEN the FX band end field reports `BothEnds`

#### Scenario: A disagreeing FX band still reports the earliest available breach point
- GIVEN a `GER40.cash` simulation where `fxLow` yields `NoBreachObserved` and `fxHigh` yields a clean
  breach on the same limit (the existing `FxRoundingSensitive` case)
- WHEN the limit's `FirstBreach` timing is produced
- THEN it reports `fxHigh`'s breaching close, tagged `FxHigh`, while the limit's `Verdict` and
  `Causes` remain governed by the existing FX-rounding-sensitivity rule, unchanged

#### Scenario: A same-currency symbol's timing needs no band comparison
- GIVEN an `XAUUSD` simulation requiring no FX band
- WHEN first-breach timing is produced
- THEN no FX-band-end comparison is performed and the timing record's FX band end field reflects the
  single evaluation run

### Requirement: New Timing Fields Use Enums, Never Booleans Or Survival Wording
Every new discriminator introduced for first-breach timing (clean-versus-contingent classification,
first-limit-to-break, FX band end) MUST be an enum, never a boolean. No new field, label, or disclosure
introduced by first-breach timing MUST contain the words "passed", "safe", "survived", "would have
passed", or any equivalent affirmation of survival, and no new field MUST be defaulted to a
non-null placeholder value.

#### Scenario: Every new discriminator is inspected for boolean or survival wording
- GIVEN a produced result with first-breach timing populated for a breaching run
- WHEN every new field on it is inspected
- THEN none is typed as a boolean, and none of their string values contain "passed", "safe",
  "survived", "would have passed", or an equivalent affirmation of survival
