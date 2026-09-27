## ADDED Requirements

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
