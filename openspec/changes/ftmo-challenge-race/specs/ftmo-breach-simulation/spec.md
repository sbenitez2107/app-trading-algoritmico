## MODIFIED Requirements

### Requirement: Elapsed Time Is Measured From The Replay-Start Anchor, Echoed On The Result
Every produced run result MUST declare a replay-start anchor: the FTMO trading day of the first
replayed trade's OPEN time. Every first-breach and first-clean-breach timing record MUST report
elapsed time from that anchor in two units: calendar days (the breach's FTMO trading day minus the
anchor day) and FTMO trading days (the count of distinct FTMO trading days, from the anchor through
the breach day inclusive, on which at least one replayed, non-`Unscalable` position was OPENED; no
holiday calendar is applied). `FtmoTradingDaysElapsed` MUST count days by OPEN, not by close, and
MUST exclude `Unscalable` trades from that count; the anchor day itself is still determined by the
first replayed trade's OPEN time regardless of whether any trade opened is `Unscalable`. The anchor
itself MUST be echoed on the run result. A refused run MUST NOT report an anchor or any elapsed time.

A scalable (non-`Unscalable`) trade counts toward the elapsed count at a given cutoff instant `c`
(the source close time of the event whose elapsed days are being computed) if and only if its open
time is strictly before `c`, or its close time is at or before `c` — that is, `Open < c ∨ Close ≤ c`.
A trade satisfying this condition is attributed to the FTMO trading day of its own OPEN time, and
counts only if that day falls within `[anchor, eventDay]` inclusive. A trade whose open is on or
after `c` MUST NOT be counted toward that event's elapsed days, even if its open falls on the same
FTMO trading day as the event and even if that day would otherwise be within `[anchor, eventDay]`.

> `FtmoTradingDaysElapsed` is corrected here, keeping its field name unchanged. The shipped
> implementation counted a distinct FTMO day as elapsed when it contained a replayed *close*; FTMO's
> own definition of a trading day is "any day 00:00-23:59 CE(S)T with at least one position **opened**"
> (`SERVICE_FTMO.md:158`), so a day containing only a close (no open) is not an FTMO trading day and
> must not be counted, while a day containing only an open (settling later) is one. A rename to a new
> field name was rejected: a caller reading the DTO through PowerShell (`$result.FtmoTradingDaysElapsed`)
> would read a missing property as `$null` with no error, silently breaking the existing readout
> instead of surfacing the change. Calendar-days-elapsed is unaffected — it is a plain date difference
> against the anchor day, not a count of days containing an event — so no already-recorded roadmap
> measurement expressed in calendar days changes value.
>
> `Unscalable` trades are excluded from the count for the same reason they are excluded from the
> replayed balance elsewhere in this capability (see "An Excluded Unscalable Trade Downgrades Only The
> Breaches It Can Affect"): a trade the FTMO account could never have opened (source `Size <= 0`) is
> not an FTMO trading day, so a day containing only such an open still counts as zero trading days
> elapsed even though the replay-start anchor (fixed by the first replayed trade's open, `Unscalable`
> or not) can still land on that day.

#### Scenario: Elapsed FTMO trading days counts distinct days with a position opened, not closed
- GIVEN a replay-start anchor of day D, and a first breaching close on FTMO trading day D+10, where
  the replayed series contains non-`Unscalable` position opens on 6 distinct FTMO trading days between
  D and D+10 inclusive, and closes (with no same-day opens) on 2 further distinct days in that range
- WHEN the breach's elapsed FTMO trading days is computed
- THEN it reports 6, not 8, and no holiday calendar is consulted

#### Scenario: A position opened later on the breach day, after the breach close, is not counted
- GIVEN a breach whose source close instant is `c` on the breach's FTMO trading day, and a position
  that opens later on that same FTMO trading day, strictly after `c`, with no other position opened
  that day before `c`
- WHEN elapsed FTMO trading days is computed for that breach
- THEN the later-opened position does not satisfy `Open < c ∨ Close ≤ c` and does not contribute to
  the count, even though its FTMO day is within `[anchor, eventDay]`

#### Scenario: A day containing only a close, no open, is not counted
- GIVEN an FTMO trading day within the elapsed window on which a position opened on a prior day closes,
  and no position is opened on that day
- WHEN elapsed FTMO trading days is computed for a breach on or after that day
- THEN that day does not contribute to the count

#### Scenario: An Unscalable open does not count as a trading day
- GIVEN an FTMO trading day within the elapsed window on which the only replayed position opened that
  day is `Unscalable`, and no other non-`Unscalable` position opens that day
- WHEN elapsed FTMO trading days is computed for a breach on or after that day
- THEN that day does not contribute to the count

#### Scenario: A flat count-by-close implementation would fail this requirement
- GIVEN the same series as the first scenario above, evaluated instead under a hypothetical
  implementation that counts a distinct FTMO day as elapsed whenever it contains a replayed close
  (the shipped behaviour), non-`Unscalable` or not
- WHEN the two counts are compared against this requirement's declared definition
- THEN the two counts differ, demonstrating that counting by close does not satisfy this requirement

#### Scenario: Calendar days elapsed is unaffected by the trading-day correction
- GIVEN a replay-start anchor of FTMO trading day D, and a first breaching close on FTMO trading day
  D+45, evaluated both before and after the trading-day counting correction
- WHEN the breach's elapsed calendar days is computed under each version
- THEN it reports 45 under both, unchanged by the correction

#### Scenario: The anchor is echoed on every non-refused result
- GIVEN any run result that was not refused
- WHEN the result is inspected
- THEN it reports the replay-start anchor's source time and its FTMO trading day, unchanged by the
  trading-day counting correction

#### Scenario: A refused run reports no anchor and no elapsed time
- GIVEN a simulation request that is refused (for example, `LimitsNotConfigured`)
- WHEN the result is inspected
- THEN no replay-start anchor is reported, and no first-breach or first-clean-breach timing is
  reported for any limit

### Requirement: First-Breach Timing Is Additive, Never A Truncation
The simulator MUST continue to replay the full trade series for every limit, exactly as today. Adding
first-breach timing, and recalculating `FtmoTradingDaysElapsed`, MUST NOT change `Verdict`, `Causes`,
or `DisclosureText` for any limit, for any run, in any way. Truncating the replay at a limit's first
breaching close MUST NOT be implemented.

> The trading-day recalculation in this change touches only `FtmoTradingDaysElapsed` and the elapsed
> FTMO-trading-days figure carried on each timing record — it MUST NOT reach into the shipped
> breach-detection path (`Verdict`, `Causes`, `DisclosureText`), which reads the running balance and
> the floor comparisons, not the trading-day count.

#### Scenario: A verdict, its causes, and its disclosure text are unchanged by the trading-day correction
- GIVEN a breaching fixture that produces a contingent daily breach followed by a clean daily breach,
  plus a disagreeing FX band on the max-loss limit
- WHEN the simulation is run before and after `FtmoTradingDaysElapsed` is recalculated to count by open
- THEN `Verdict`, `Causes`, and `DisclosureText` for every limit are byte-identical between the two
  runs, and only the elapsed-trading-days figures on the timing records may differ
