# Tasks: FTMO simulation UI

Strict TDD throughout: every RED precedes its GREEN, and every RED must be demonstrably able to fail
(falsification is mandatory, not optional — see the falsification task in every slice). Six chained
slices per design.md ("File Changes" / "Migration / Rollout"): **PR1a** (client + models) → **PR1b**
(mappers + i18n) → **PR1c** (presentational panels) → **PR1d** (container modal + wiring) → **PR2a**
(single-start client + mappers) → **PR2b** (single-start detail component). Each slice starts only
after the previous slice's gates are green, and each slice is independently buildable and testable —
1a–1c add code with no importer yet (flagged, unavoidable per design.md's rollout note), all other
slices are fully wired. Read `.claude/conventions/frontend-core.md`, `frontend-data.md`, and
`frontend-design.md` before writing `.ts`/`.html`/`.scss`. Every unit cites its spec requirement/
scenario or design decision.

## Hard rules — checkpoint at every task that touches these

1. **No backend change in this capability.** If any task appears to need one — a new endpoint, a DTO
   field, a migration — STOP and report instead of touching `app.trading.algoritmico.api/`. PR0
   (`ftmo-instrument-specs`) is dropped; do not resurrect it.
2. **Enum zero values are never falsy.** `FtmoSimulationStatus.Refused=0`,
   `FtmoSimulationRefusal.InvalidRequest=0`, and `FtmoChallengeRaceRefusal.ProfitTargetMismatch=0`
   (plus every other zero member in design.md's enum-mirror table) MUST render their correct label via
   an exact-value switch and `!== null`/`!== undefined` presence checks — never `if (value)`. Each has
   its own dedicated test asserting it is labelled and rendered, not treated as absent.
3. **Banned wording.** A test sweeps every EN and ES key under the `FTMO_SIMULATION` i18n namespace
   (lowercased, diacritic-stripped substring match) for: "passed", "safe", "survived", "would have
   passed", "aprobado", "aprobó", "seguro", "sobrevivió", "habría aprobado". The SQX pipeline keys are
   out of scope and MUST NOT be touched by this test.
4. **i18n parity.** Every visible string is an i18n key present in both `en.json` and `es.json`,
   except the server's verbatim `Disclosure`/`NotModelled` text (AD11 — shown as data, never looked up
   as a key). A test checks `FTMO_SIMULATION` key parity between the two locale files.
5. **Source lot grid.** The prefilled `sizeDecimals`/`step`/`minLot`/`maxLots` defaults are
   `2`/`0.01`/`0.01`/`10` (`IMOX_RETESTER_LOT_GRID`, mirroring `LotGrid.ImoxRetester`), never the FTMO
   grid's own values. A test pins these exact defaults on the constant and on the form's initial state.
6. **Dates.** Any `DateOnly`/`yyyy-MM-dd` wire string is formatted by slicing the string, and is never
   passed to `new Date(...)` — a UTC-midnight parse shows the previous day at UTC-3. A test pins this
   for every date-bearing formatter (`MonthsWithoutStart` entries, order-stat labels if date-bearing,
   PR2 challenge-race phase dates).
7. **A test that cannot fail is a defect.** Every slice's falsification task is mandatory: break the
   unit under test, observe the target test go red, then restore it and confirm green again.
8. **Process safety.** Never kill any process — the user's API and/or `ng serve` dev server may be
   running. No database access. No HTTP call to the running API from a test (`HttpTestingController`
   only). Do NOT run `git commit`, `git push`, `git merge`, or `git rebase` — the user commits each
   slice on approval via `/commit`.
9. **No test-only code in production.** No production branch, flag, or export exists solely to be
   asserted by a test; test doubles and fixtures live in `.spec.ts` files only.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| PR1a estimated lines | ~360 (client, models, `.spec.ts`) |
| PR1b estimated lines | ~500 (mappers, i18n namespace, `.spec.ts`) |
| PR1c estimated lines | ~600 (3 presentational components + specs) |
| PR1d estimated lines | ~600 (container modal + account-detail wiring + specs) |
| PR2a estimated lines | ~400 (single-start client, mappers, i18n additions + specs) |
| PR2b estimated lines | ~450 (single-start detail component + container wiring + specs) |
| 400-line budget risk | High (1b, 1c, 1d, 2b) — flagged per `ask-on-risk`, not blocking; the user already accepted the slice plan in design.md's Sizing table |
| Chained slices | Yes — 1a → 1b → 1c → 1d → 2a → 2b, per design.md "Migration / Rollout" |
| Delivery strategy | `ask-on-risk` (default) |

Gate commands (verbatim, from `.claude/skills/commit/SKILL.md`, run from
`app.trading.algoritmico.web/`):
```bash
npx prettier --check <staged .ts/.html/.scss paths, relative to this dir>
npx tsc --build --emitDeclarationOnly false --noEmit
npx ng test --watch=false
```
Prettier is scoped to the slice's own new/changed files (paths relative to `app.trading.algoritmico.web/`).
`tsc --build` and `ng test` always run against the whole project — there is no narrower gate for them.

---

## PR1a — API client and models (`web/core/models/ftmo-simulation.model.ts`, `web/core/services/ftmo-simulation.service.ts`)

Not yet imported by any component (design.md's rollout note) — unavoidable, flagged, not dead code:
every symbol here is consumed starting in PR1b (mappers) and PR1d (container).

### Phase 1a.1 — Enums and the lot-grid constant

- [x] 1a.1.1 RED: `ftmo-simulation.model.spec.ts` — `FTMO_SIMULATION enums pin their exact numeric
  values`: one assertion per enum member in design.md's "Enum mirrors" table for PR1a
  (`FtmoSimulationStatus`, `FtmoSimulationRefusal`, `FtmoChallengeRaceRefusal`, `FtmoChainOutcome`,
  `FtmoPhaseOutcome`, `FtmoFundedOutcome`, `FtmoFirstBreachingLimit`, `FtmoBreachPointClass`,
  `FtmoFxBandEnd`, `FtmoStartGrain`), asserting `Refused === 0`, `InvalidRequest === 0`,
  `ProfitTargetMismatch === 0`, `Phase1UndecidedAtEndOfData === 0`, both `NotStarted === 0`,
  `Daily === 0`, `Clean === 0`, `FxLow === 0`, `Monthly === 0` explicitly, alongside every non-zero
  member.
  _Satisfies: design.md "Enum mirrors" table; hard rule 2 (zero values)._
  _Apply note: RED confirmed — `Cannot find module './ftmo-simulation.model'` (TS2307) before the
  file existed._
- [x] 1a.1.2 GREEN: create `ftmo-simulation.model.ts` with the 10 enums as TS `enum` declarations with
  explicit numeric initializers copied verbatim from `Domain/Enums/*.cs`. Confirm 1a.1.1 passes.
  _Satisfies: design.md AD2; proposal.md D7._
  _Apply note: values verified against the live `.cs` sources (not just design.md's table): all 10
  enums match exactly._
- [x] 1a.1.3 RED: `IMOX_RETESTER_LOT_GRID pins the backtest lot grid, never the FTMO grid` — asserts
  `{ sizeDecimals: 2, step: 0.01, minLot: 0.01, maxLots: 10 }`.
  _Satisfies: spec.md "The Source Lot Grid Is Prefilled From The IMOX Retester Constant" requirement;
  hard rule 5._
- [x] 1a.1.4 GREEN: add the `IMOX_RETESTER_LOT_GRID` constant to the same file. Confirm 1a.1.3 passes.
  _Satisfies: design.md AD7 (revised); "Resolved: the lot grid hazard"._
  _Apply note: 11/11 model tests green after 1a.1.2+1a.1.4._
- [x] 1a.1.5 Falsification: temporarily set `maxLots: 1000` (the FTMO grid's own value) on the
  constant; confirm 1a.1.3 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (`toEqual` mismatch, `maxLots: 1000` vs expected `10`);
  restored; 11/11 green again._

### Phase 1a.2 — Wire DTOs and query type

- [x] 1a.2.1 GREEN: add the wire DTO interfaces (`FtmoMultiStartDto`, `FtmoMultiStartRunDto`,
  `FtmoMultiStartRowDto`, `FtmoFundedPhaseDto`, `FtmoOutcomeCountDto`, `FtmoOrderStatisticsDto`,
  `FtmoMultiStartSummaryDto`) mirroring the API's `Application/DTOs/Backtests/FtmoMultiStartDto.cs`
  records 1:1, and `FtmoSimulationQuery` (the 8 required fields + optional `fxLow`/`fxHigh`). No test
  needed for pure interface shapes; the mapper tests in PR1b are the behavioural proof.
  _Satisfies: design.md AD3, Interfaces/Contracts._
  _Apply note: field names camelCased per ASP.NET's default JSON casing (confirmed — no
  `JsonNamingPolicy`/`JsonStringEnumConverter` registered anywhere in `src/`, the only
  `JsonSerializerOptions` is `GridPresetService.cs:12` and is unrelated to controller responses).
  `kind`/`segment` on `FtmoMultiStartRunDto` are typed as `BacktestRunKind`/`BacktestSegment`, imported
  from `backtest.service.ts`, per design.md AD2 ("`BacktestRunKind` and `BacktestSegment` are reused
  from `backtest.service.ts:19-35`")._
  _Correction note (review RELIABILITY-001, CRITICAL): the first pass was NOT 1:1 —
  `FtmoMultiStartRowDto` lacked `phase1`/`phase2` (`FtmoChallengePhaseDto`) and `funded`
  (`FtmoFundedPhaseDto`), and `FtmoMultiStartRunDto` lacked `rules` (`FtmoChallengeRulesDto`). Added
  the four properties plus the `FtmoChallengePhaseDto` and `FtmoChallengeRulesDto` interfaces (C#
  source: `FtmoBreachSimulationDto.cs`; `DateOnly`/`DateTime` → `string`, `decimal`/`int` → `number`,
  `T?` → `T | null`). The "no test needed" premise above was wrong: `ftmo-simulation.model.spec.ts` now
  pins the whole shape with typed, cast-free fixture literals carrying every C# field (enforced by
  `tsc --build` via `tsconfig.spec.json`) plus 3 runtime key-list assertions. RED confirmed before the
  fix — tsc exit 2: TS2305 (no exported `FtmoChallengePhaseDto`/`FtmoChallengeRulesDto`), TS2353
  (`phase1` not in `FtmoMultiStartRowDto`, `rules` not in `FtmoMultiStartRunDto`), TS2339 (`phase1`,
  `phase2`, `funded`, `rules`). Every other interface (`FtmoOrderStatisticsDto`, `FtmoFundedPhaseDto`,
  `FtmoOutcomeCountDto`, `FtmoMultiStartSummaryDto`, `FtmoMultiStartDto`) and all 10 enums re-checked
  against C#: no other gap._

### Phase 1a.3 — `FtmoSimulationService.getMultiStart`

- [x] 1a.3.1 RED: `ftmo-simulation.service.spec.ts` — `getMultiStart sends every required query
  param, with sizeDecimals=0 included` — a query with `sizeDecimals: 0` and no `fxLow`/`fxHigh`;
  asserts the request URL/params include all 8 required params (including the literal `"0"` for
  `sizeDecimals`) and omit `fxLow`/`fxHigh` entirely.
  _Satisfies: spec.md "A zero sizeDecimals value does not disable Run" (client parity); design.md
  Testing Strategy row 1; hard rule 2._
  _Apply note: RED confirmed — `Could not resolve "./ftmo-simulation.service"` before the file
  existed._
- [x] 1a.3.2 RED: `..._fxLow and fxHigh are included only when not null` — a query with both fx bounds
  set; asserts both appear in the params.
  _Satisfies: design.md AD6, Interfaces/Contracts `buildFtmoQueryParams` note._
- [x] 1a.3.3 RED: `..._a 400 response maps to INVALID_QUERY with the server message as detail` —
  `HttpTestingController` flushes a 400 with `{ message: "..." }`; asserts the mapped
  `FtmoRequestError` has `key: 'ERRORS.INVALID_QUERY'` and `detail` equal to the server message.
  _Satisfies: spec.md "An HTTP Error Or 400 Response Is Shown, Never Silently Swallowed"; design.md
  AD10._
- [x] 1a.3.4 RED: `..._a network failure maps to REQUEST_FAILED with null detail` — a network-error
  flush; asserts `key: 'ERRORS.REQUEST_FAILED'`, `detail: null`.
  _Satisfies: spec.md same requirement, network-failure scenario; design.md AD10._
- [x] 1a.3.5 GREEN: implement `FtmoSimulationService.getMultiStart(strategyId, query)`:
  `buildFtmoQueryParams` (every required field via `.set(k, String(v))`, fx bounds only when
  `!== null`), `GET api/strategies/{id}/ftmo-breach/multi-start`, `catchError` mapping per AD10.
  Confirm 1a.3.1–1a.3.4 pass.
  _Satisfies: design.md AD2, AD10, Interfaces/Contracts._
  _Apply note: 4/4 service tests green. Route and 400 body shape (`{ message }`) confirmed against
  `StrategyBacktestsController.GetFtmoMultiStart`/`TryValidateFtmoBreachQuery`._
- [x] 1a.3.6 Falsification: temporarily change the 400-mapping branch to also match on status `0`
  (network errors); confirm 1a.3.4 goes RED (asserts `REQUEST_FAILED`, gets `INVALID_QUERY`); restore;
  confirm both 1a.3.3 and 1a.3.4 green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (network-failure test expected `REQUEST_FAILED`, got
  `INVALID_QUERY`); restored; 4/4 green again._
- [x] 1a.3.7 RED: `..._fxLow and fxHigh of zero are sent as "0", never dropped` — a query with
  `fxLow: 0, fxHigh: 0`; asserts both params equal `"0"` (the backend refuses the band with
  `InvalidFxBand`, per design.md AD6).
  _Satisfies: design.md AD6; hard rule 2. Added by review correction RELIABILITY-002 (WARNING)._
  _Apply note: passes against the existing `!== null && !== undefined` guard. Falsification: both
  guards temporarily changed to a truthy `if (query.fxLow)`/`if (query.fxHigh)`; the test went RED
  (`expected null to be '0'`, 1 failed / 4 passed); service file restored byte-identical (`cmp`);
  5/5 green again._

### Phase 1a.4 — PR1a gates

- [x] 1a.4.1 `cd app.trading.algoritmico.web && npx prettier --check src/app/core/models/ftmo-simulation.model.ts src/app/core/models/ftmo-simulation.model.spec.ts src/app/core/services/ftmo-simulation.service.ts src/app/core/services/ftmo-simulation.service.spec.ts` — no diffs.
  _Apply note: initial run flagged the two service files (mechanical formatting only, no logic
  change); `--write` applied, re-check passed clean._
- [x] 1a.4.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [x] 1a.4.3 `npx ng test --watch=false` (full suite, once) — confirm every pre-existing spec plus every
  new PR1a spec passes, with **0 existing assertions edited**.
  _Apply note: baseline (pre-change) was 33 test files / 414 tests, all passing. After PR1a: 35 test
  files / 429 tests, all passing — the +2 files / +15 tests are exactly the two new PR1a spec files
  (11 model + 4 service); every pre-existing test file and count is unchanged._
  _Correction re-run (RELIABILITY-001/002): prettier clean (exit 0), tsc exit 0 with empty output,
  full suite 35 files / 433 tests passing — +4 over 429 are exactly the 3 new model wire-shape tests
  and the 1 new zero-fx service test; 0 existing assertions edited._
- [ ] 1a.4.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching. (N/A — no failures or diffs occurred.)

---

## PR1b — Mappers and i18n (`FM/ftmo-simulation.mappers.ts`, `public/assets/i18n/{en,es}.json`, `FM/ftmo-simulation.i18n.spec.ts`)

`FM/` = `web/features/broker-accounts/ftmo-simulation-modal/`. Not yet imported by a component
(unavoidable per design.md's rollout note) — consumed starting in PR1c (panels) and PR1d (container).

### Phase 1b.1 — i18n namespace and banned-wording/parity tests

- [x] 1b.1.1 RED: `ftmo-simulation.i18n.spec.ts` — `every FTMO_SIMULATION key present in en.json has a
  matching key in es.json, and vice versa` — imports both JSON files and diffs the flattened
  `FTMO_SIMULATION` key sets.
  _Satisfies: spec.md "Every Visible String Comes From i18n, In EN And ES"; hard rule 4._
  _Apply note: RED confirmed — `expected 0 to be greater than 0` (no `FTMO_SIMULATION` namespace yet)._
- [x] 1b.1.2 RED: `..._no FTMO_SIMULATION key text contains banned survival/pass wording` — sweeps
  every EN and ES value under `FTMO_SIMULATION` (lowercased, diacritics stripped) against the 9 banned
  substrings.
  _Satisfies: spec.md "Banned Wording Is Excluded From The FTMO i18n Keys"; hard rule 3._
- [x] 1b.1.3 RED: `..._an SQX pipeline key using "survived" is not scoped by the banned-wording check`
  — asserts a known SQX key containing "survived" is excluded from the swept key set.
  _Satisfies: spec.md "SQX pipeline keys using 'survived' are out of scope" scenario._
  _Apply note: no literal "survived" key exists anywhere in `en.json`/`es.json` (confirmed by grep); the
  existing SQX out-of-scope word is "passed" (`SQX.WORKFLOW.PASSED`, `en.json`/`es.json`). The test
  asserts that key exists and contains "PASSED", and that the flattened FTMO-scoped key set never
  includes an `SQX`-prefixed key — the same out-of-scope behaviour the scenario describes._
- [x] 1b.1.4 GREEN: add the `FTMO_SIMULATION` namespace to `en.json` and `es.json` — modal chrome,
  field labels (including the "backtest (IMOX retester) lot grid" label, never "FTMO grid"), the
  always-visible disclosure block copy, the six outcome-share labels, the order-stat row labels, every
  refusal-reason label (one per `FtmoSimulationRefusal`/`FtmoChallengeRaceRefusal` member, each
  distinct — `InstrumentSpecMissing`/`FxRateNotDeclared`/`InvalidFxBand` get their own wording, not a
  shared generic message), `NOT_REPORTED`, `NO_RUN_HELD`, `ERRORS.INVALID_QUERY`,
  `ERRORS.REQUEST_FAILED`, and `UNKNOWN_VALUE`. Confirm 1b.1.1–1b.1.3 pass.
  _Satisfies: design.md AD4, AD11; spec.md D6/D4; proposal.md D4, D6._
  _Apply note: 3/3 i18n tests green. Also added label groups for every enum needed by 1b.2 (STATUS,
  REFUSAL, RACE_REFUSAL, CHAIN_OUTCOME, PHASE_OUTCOME, FUNDED_OUTCOME, FIRST_BREACHING_LIMIT,
  BREACH_POINT_CLASS, FX_BAND_END, START_GRAIN) and the order-stats row labels, so 1b.2/1b.3 need no
  further i18n additions. ES copy is neutral professional Spanish, no voseo; outcomes describe
  elimination ("Eliminado en Fase 1") and target-reached is phrased as an optimistic result ("Objetivo
  alcanzado primero"), never certification._
- [x] 1b.1.5 Falsification: temporarily insert the literal word "safe" into one EN
  `FTMO_SIMULATION` value; confirm 1b.1.2 goes RED; revert; confirm green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (`expected [ Array(1) ] to deeply equal []`, parity test also
  collaterally failed as expected); reverted; 3/3 green again._

### Phase 1b.2 — Label accessors (exact-value switch, zero-safe)

- [x] 1b.2.1 RED: `ftmo-simulation.mappers.spec.ts` — one test per zero-valued enum member listed in
  hard rule 2 (`Refused`, `InvalidRequest`, `ProfitTargetMismatch`, `Phase1UndecidedAtEndOfData`, both
  `NotStarted`, `Daily`, `Clean`, `FxLow`, `Monthly`): asserts the label accessor returns the correct
  i18n key for value `0`, not `UNKNOWN_VALUE` and not an empty string.
  _Satisfies: spec.md "Every FTMO Enum Is Mapped By Exact Value, Never By Truthiness"; hard rule 2._
  _Apply note: RED confirmed — `Could not resolve "./ftmo-simulation.mappers"` before the file existed._
- [x] 1b.2.2 RED: `..._a value with no matching label renders UNKNOWN_VALUE with the raw number, never
  another member's label` — an out-of-range integer; asserts the `UNKNOWN_VALUE` key plus the raw
  value, and that it does not collide with any real member's label.
  _Satisfies: design.md AD4._
- [x] 1b.2.3 RED: `..._a null or undefined enum value returns null, not a label` — asserts the presence
  check short-circuits before the switch.
  _Satisfies: design.md AD4 (`!== null`/`!== undefined` presence checks)._
- [x] 1b.2.4 GREEN: implement the `Record<Enum, string>` label maps and the shared accessor
  `x === null || x === undefined ? null : MAP[x] ?? UNKNOWN_KEY` for every enum mirrored in PR1a.
  Confirm 1b.2.1–1b.2.3 pass.
  _Satisfies: design.md AD4._
  _Apply note: 25/25 mapper tests green (10 zero-value + UNKNOWN_VALUE + null/undefined + 12 toPanels
  tests, see 1b.3). All 10 PR1a enums have a `Record<Enum,string>` label map._
- [x] 1b.2.5 Falsification: temporarily replace one accessor's presence check with `if (x)` (a
  truthy check); confirm the matching zero-value test from 1b.2.1 goes RED; restore; confirm green
  again.
  _Satisfies: hard rule 2, hard rule 7._
  _Apply note: falsification confirmed RED — all 10 zero-value tests failed simultaneously (`if (!value)`
  treats every zero-valued member as absent); restored; 25/25 green again._

### Phase 1b.3 — `toPanels` and formatters

- [x] 1b.3.1 RED: `..._a Refused run maps to state "refused" with no outcome shares or order stats` —
  a DTO with `status: Refused`; asserts the resulting VM's discriminant is `'refused'` and carries no
  outcome/order-stat data.
  _Satisfies: spec.md "A Whole-Run Refusal Shows Its Reason"._
- [x] 1b.3.2 RED: `..._InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand map to three
  distinct message keys` — three refused DTOs, one per reason; asserts three distinct resolved i18n
  keys.
  _Satisfies: spec.md "InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render a
  distinct message"._
- [x] 1b.3.3 RED: `..._a race refusal of ProfitTargetMismatch carries the stored value through to the
  VM` — a DTO with `raceRefusal: ProfitTargetMismatch, storedProfitTargetPct: 0.08`; asserts the VM
  surfaces both the refusal state and `0.08`.
  _Satisfies: spec.md "A Race Refusal Of ProfitTargetMismatch Shows Its Stored Value"; hard rule 2._
- [x] 1b.3.4 RED: `..._a summary with no starts maps to state "noStarts"` — `summary: null`; asserts
  discriminant `'noStarts'`.
  _Satisfies: design.md Data Flow, per-run VM state ordering._
- [x] 1b.3.5 RED: `..._an evaluated run renders all six outcome rows including zero-count ones` — a
  summary with `FundedBreached` count 0; asserts exactly six rows in the fixed order, the zero-count
  row present with `count: 0, share: 0`.
  _Satisfies: spec.md "The Six Outcome Shares Render With Counts, Zeros Included"._
- [x] 1b.3.6 RED: `..._an order-stat row with N=0 renders every quantile as absent, not zero` — asserts
  the VM's quantile fields are `null`/a sentinel meaning "—", never `0`.
  _Satisfies: spec.md "The Order-Statistics Table..." — zero-observation scenario._
- [x] 1b.3.7 RED: `..._an order-stat row with Min=0 and N>0 renders "0", not "—"` — distinguishes a
  real zero value from an absent one.
  _Satisfies: design.md Data Flow, "A `null` renders '—'; `0` renders '0'."_
- [x] 1b.3.8 RED: `..._the funded-duration headline uses FundedDaysToBreachFromFundedStart, with
  FromChainStart shown as secondary` — a summary where the two order-stat sets differ; asserts the
  VM's headline field equals the funded-start stats and a separate secondary field equals the
  chain-start stats.
  _Satisfies: spec.md "The Order-Statistics Table Renders With The Funded-Duration Headline..."._
- [x] 1b.3.9 RED: `..._months without a start are listed with their count, never dropped` — a run with
  3 `monthsWithoutStart` entries; asserts the VM lists all 3 with `count: 3`.
  _Satisfies: spec.md "Months Without A Start Are Disclosed"._
- [x] 1b.3.10 RED: `..._a "yyyy-MM" month string is sliced, never parsed with new Date` — a
  `monthsWithoutStart` entry `"2024-13"` style edge or a UTC-3-hazard date (e.g. `"2024-01-01"`);
  asserts the formatted output matches a direct string slice and does NOT match what `new Date(...)`
  would produce at UTC-3.
  _Satisfies: spec.md date handling per design.md; hard rule 6._
  _Apply note: the mapper passes `monthsWithoutStart` strings through unchanged (pure slice/pass-through,
  no `new Date(...)` call anywhere in the mapper); pinned by asserting `"2024-01"` stays `"2024-01"` and
  never becomes `"2023-..."` (the UTC-3 hazard a `new Date('2024-01-01')` parse would produce)._
- [x] 1b.3.11 RED: `..._Start1DiffersFromSingleStartAnchor=true surfaces an explicit disclosure flag
  in the VM` — asserts a boolean/flag field is `true` and distinct from the generic disclosure text.
  _Satisfies: spec.md "The Start1DiffersFromSingleStartAnchor Flag Is Disclosed"._
- [x] 1b.3.12 RED: `..._the server Disclosure and NotModelled text pass through verbatim, never looked
  up as an i18n key` — a DTO with arbitrary disclosure text; asserts the VM field equals that text
  exactly, unmodified.
  _Satisfies: spec.md "The Disclosure Text Is Always Visible, And Is Shown Verbatim As Data"; design.md
  AD11._
- [x] 1b.3.13 GREEN: implement `toPanels(dto: FtmoMultiStartDto | null): FtmoRunPanelVm[]` and its
  formatters (`FtmoRunPanelState` discriminant per AD3/design.md Data Flow's state-ordering rule:
  `Refused` → `raceRefusal !== null` → `summary === null` → `evaluated`), the fixed six-outcome-row
  builder, the order-stat "—"/"0" formatter, and the `yyyy-MM` slicer. Confirm 1b.3.1–1b.3.12 pass.
  _Satisfies: design.md AD3, Data Flow._
  _Apply note: implemented `toRunPanelVm(run)` (per-run) and `toPanels(dto)` (maps `dto.runs`, `[]` for
  `null`). 25/25 mapper tests green. `CHAIN_OUTCOME_ORDER` fixes the six-row order; `statRow()` maps
  `n === 0` to every quantile `null` and otherwise passes the raw value (including `0`) through
  unchanged._
- [x] 1b.3.14 Falsification: temporarily reorder the state-priority check so `summary === null` is
  tested before `status === Refused`; confirm 1b.3.1 or 1b.3.4 goes RED (a refused-with-null-summary
  fixture now maps to the wrong state); restore; confirm green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (3 failures: the Refused test, the three-distinct-refusals
  test, and the race-refusal test — all now short-circuit into `noStarts` first since `summary: null` in
  those fixtures); restored; 25/25 green again._

### Phase 1b.4 — PR1b gates

- [x] 1b.4.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers.spec.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.i18n.spec.ts public/assets/i18n/en.json public/assets/i18n/es.json` — no diffs.
  _Apply note: initial run flagged the 3 new `.ts` files (mechanical formatting only, no logic change);
  the JSON files were already clean; `--write` applied to the `.ts` files, re-check passed clean._
- [x] 1b.4.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [x] 1b.4.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR1b specs plus every new
  PR1b spec pass, **0 existing assertions edited**.
  _Apply note: baseline (pre-PR1b) was 35 test files / 433 tests, all passing. After PR1b: 37 test files /
  461 tests, all passing — the +2 files / +28 tests are exactly the 2 new PR1b spec files (3 i18n + 25
  mappers); every pre-existing test file and count is unchanged._
- [x] 1b.4.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching. (N/A — no failures or diffs occurred.)

---

## PR1c — Presentational panels (`FM/ftmo-outcome-bars/`, `FM/ftmo-order-stats-table/`, `FM/ftmo-run-panel/`)

Not yet imported by the container (unavoidable per design.md's rollout note) — wired in PR1d.

### Phase 1c.1 — `FtmoOutcomeBarsComponent`

- [ ] 1c.1.1 RED: `ftmo-outcome-bars.component.spec.ts` — `renders exactly six outcome rows, one per
  chain outcome, in the fixed order` — TestBed + `TranslateModule.forRoot()`, an `input.required` VM
  with all six rows; asserts exactly six `.ftmo-sim__outcome-row` (or equivalent BEM) elements, in
  order.
  _Satisfies: spec.md "All six outcomes are always present"._
- [ ] 1c.1.2 RED: `..._a zero-count row still renders its count and 0% share, not omitted` — asserts
  the zero row's rendered text includes "0" for both count and share.
  _Satisfies: spec.md "A zero-count outcome is still rendered"._
- [ ] 1c.1.3 RED: `..._every share bar label resolves to an i18n key present in both locales` —
  renders in EN then ES (or asserts each label's translate key against both loaded JSON files);
  asserts no raw/untranslated key string is shown.
  _Satisfies: spec.md "Every Visible String Comes From i18n"; hard rule 4._
- [ ] 1c.1.4 GREEN: implement `FtmoOutcomeBarsComponent` (OnPush, `input.required<FtmoOutcomeRowVm[]>()`
  or equivalent), six BEM/CSS share bars, count beside each share, no green colour (AD9). Confirm
  1c.1.1–1c.1.3 pass.
  _Satisfies: design.md AD5, AD9; proposal.md D3._
- [ ] 1c.1.5 Falsification: temporarily filter out rows with `count === 0` before rendering; confirm
  1c.1.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1c.2 — `FtmoOrderStatsTableComponent`

- [ ] 1c.2.1 RED: `ftmo-order-stats-table.component.spec.ts` — `renders a plain semantic <table> with
  N/Min/Q1/Median/Q3/Max columns for phase-1, phase-2, funded (funded-start headline), funded
  (chain-start, secondary), and censored-runway rows` — asserts a `<table>` element (not a grid
  component) with the expected row count and headline/secondary distinction (e.g. a `--secondary`
  modifier class or muted styling hook on the chain-start row).
  _Satisfies: spec.md "The Order-Statistics Table Renders With The Funded-Duration Headline..."._
- [ ] 1c.2.2 RED: `..._an N=0 row renders every quantile as "—", never "0"` — asserts rendered cell
  text.
  _Satisfies: spec.md zero-observation scenario._
- [ ] 1c.2.3 RED: `..._a Min=0 row with N>0 renders "0", not "—"` — asserts the distinction is
  preserved at render time, not just in the VM.
  _Satisfies: design.md Data Flow, "0 renders '0'"._
- [ ] 1c.2.4 GREEN: implement `FtmoOrderStatsTableComponent` (OnPush, `input.required<...>()`), a
  plain `<table>` (no Prizm grid, per AD5's rejection of a data-grid component for six rows). Confirm
  1c.2.1–1c.2.3 pass.
  _Satisfies: design.md AD5; proposal.md D3 (grid rejection rationale)._
- [ ] 1c.2.5 Falsification: temporarily render `stat.min ?? 0` instead of the "—" sentinel; confirm
  1c.2.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1c.3 — `FtmoRunPanelComponent` (per-run composition: refusal / race-refusal / no-starts / evaluated)

- [ ] 1c.3.1 RED: `ftmo-run-panel.component.spec.ts` — `state "refused" renders the refusal reason and
  no outcome bars or order-stats table` — asserts the bars/table children are absent (`@if` false) and
  the refusal message text is present.
  _Satisfies: spec.md "A refused run shows its reason, not empty results"._
- [ ] 1c.3.2 RED: `..._InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render distinct
  text` — three fixtures; asserts three distinct rendered strings.
  _Satisfies: spec.md "InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render a
  distinct message"._
- [ ] 1c.3.3 RED: `..._state "raceRefused" renders ProfitTargetMismatch alongside the stored value
  0.08` — asserts both the refusal label and the numeric value appear.
  _Satisfies: spec.md "ProfitTargetMismatch is shown with the stored value"._
- [ ] 1c.3.4 RED: `..._monthsWithoutStart renders the months and their count when non-empty` — 3
  entries; asserts all 3 plus the count "3" are rendered.
  _Satisfies: spec.md "Months without a start are listed with their count"._
- [ ] 1c.3.5 RED: `..._Start1DiffersFromSingleStartAnchor=true renders an explicit disclosure` —
  asserts the disclosure text/element is present.
  _Satisfies: spec.md "A divergent start 1 is disclosed"._
- [ ] 1c.3.6 RED: `..._the run's Disclosure and NotModelled text are always visible, shown verbatim,
  with no collapsed accordion` — asserts the text node is present in the rendered DOM without
  simulating a click/expand interaction.
  _Satisfies: spec.md "Disclosure is visible on a normal run"; design.md AD11._
- [ ] 1c.3.7 RED: `..._state "evaluated" composes FtmoOutcomeBarsComponent and
  FtmoOrderStatsTableComponent as children` — asserts both child components are present with the VM's
  data passed through.
  _Satisfies: design.md AD5; proposal.md D3._
- [ ] 1c.3.8 RED: `..._two panels render in two distinct <section> elements, each separately labelled,
  and stack on narrow widths without merging` — renders two `FtmoRunPanelComponent` host sections (or
  a parent fixture with two panels); asserts two separate `<section>`s with distinct labels/headings,
  never a single merged section.
  _Satisfies: spec.md "Deploy and Eval render as separate panels" / "Narrow widths stack but do not
  merge the panels"; design.md AD9._
- [ ] 1c.3.9 GREEN: implement `FtmoRunPanelComponent` (OnPush, `input.required<FtmoRunPanelVm>()`):
  an `@switch` on the VM's state discriminant rendering the refused / race-refused / no-starts /
  evaluated branches, the always-visible disclosure block, `monthsWithoutStart`, and the Start-1 flag
  in every branch. Confirm 1c.3.1–1c.3.8 pass.
  _Satisfies: design.md AD5, AD9, Data Flow ("Every state renders the disclosure,
  monthsWithoutStart..., the start-1 flag, notModelled...")._
- [ ] 1c.3.10 Falsification: temporarily wrap the disclosure block in an `@if (expanded())` gated by a
  click handler; confirm 1c.3.6 goes RED (text absent without a simulated click); restore; confirm
  green again.
  _Satisfies: hard rule 7._

### Phase 1c.4 — PR1c gates

- [ ] 1c.4.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-outcome-bars/ src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-order-stats-table/ src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-run-panel/` — no diffs.
- [ ] 1c.4.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [ ] 1c.4.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR1c specs plus every new
  PR1c spec pass, **0 existing assertions edited**.
- [ ] 1c.4.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching.

---

## PR1d — Container modal, form, and wiring into account-detail (`FM/ftmo-simulation-modal.component.*`, `account-detail.component.*`)

This is the slice where the feature becomes reachable (design.md's rollout note). All PR1a–1c code
gets its first importer here.

### Phase 1d.1 — Form signals and `canRun`

- [ ] 1d.1.1 RED: `ftmo-simulation-modal.component.spec.ts` — `on open, the source-grid fields are
  prefilled from IMOX_RETESTER_LOT_GRID, broker from account context, and initialCapital to 10000` —
  TestBed with a mocked `FtmoSimulationService`; asserts the rendered field values.
  _Satisfies: spec.md "The source lot grid is prefilled from the IMOX retester constant"; hard rule 5._
- [ ] 1d.1.2 RED: `..._the source-grid fields stay editable after prefill` — simulates editing
  `maxLots`; asserts the new value is accepted and Run stays available.
  _Satisfies: spec.md "The source-grid fields stay editable"._
- [ ] 1d.1.3 RED: `..._Run is disabled while targetRiskPerTrade is empty` — asserts the Run button's
  `disabled` state.
  _Satisfies: spec.md "Run is disabled while targetRiskPerTrade is empty"._
- [ ] 1d.1.4 RED: `..._Run is enabled once all 8 required fields are filled, fxLow/fxHigh left empty`
  — asserts Run enabled.
  _Satisfies: spec.md "Run is enabled once all 8 required fields are filled"._
- [ ] 1d.1.5 RED: `..._sizeDecimals=0 does not disable Run` — asserts Run stays enabled with
  `sizeDecimals: 0` and every other field filled.
  _Satisfies: spec.md "A zero sizeDecimals value does not disable Run"; hard rule 2._
- [ ] 1d.1.6 GREEN: implement the form signals (`number | null` per field, `string` for
  broker/sqxSymbol), prefill on construction/open from `IMOX_RETESTER_LOT_GRID` and account context,
  and `canRun = computed(...)` per AD6 (all 8 required fields `!== null` and finite, `!running()`).
  Confirm 1d.1.1–1d.1.5 pass.
  _Satisfies: design.md AD6, AD7, AD8._
- [ ] 1d.1.7 Falsification: temporarily change `canRun`'s `sizeDecimals` check to `!!sizeDecimals()`
  (truthy); confirm 1d.1.5 goes RED; restore; confirm green again.
  _Satisfies: hard rule 2, hard rule 7._

### Phase 1d.2 — Run lifecycle

- [ ] 1d.2.1 RED: `..._editing an input after a completed run does not send a new request` — one
  `expectOne` for the initial run, then an edit; asserts `httpTestingController.verify()` finds no
  additional request.
  _Satisfies: spec.md "Editing an input does not trigger a request"._
- [ ] 1d.2.2 RED: `..._a second Run activation while one is in flight sends no second request and Run
  stays disabled` — activates Run twice before flushing; asserts exactly one `expectOne` and Run
  disabled until resolution.
  _Satisfies: spec.md "A second Run is blocked while one is in flight"._
- [ ] 1d.2.3 RED: `..._a 400 response shows an explicit error, re-enables Run, and renders no partial
  result` — flushes a 400; asserts an error state element is present, Run re-enabled, no run panels
  rendered.
  _Satisfies: spec.md "A 400 response shows an explicit error"._
- [ ] 1d.2.4 RED: `..._a network failure shows an explicit error and re-enables Run` — flushes a
  network error; asserts error state and Run re-enabled.
  _Satisfies: spec.md "A network failure shows an explicit error"._
- [ ] 1d.2.5 RED: `..._destroying the fixture while a request is in flight cancels the request` —
  destroys the fixture mid-flight; asserts the `HttpTestingController` request was cancelled/aborted
  (no dangling open request at `verify()`).
  _Satisfies: design.md AD10 (`takeUntilDestroyed`)._
- [ ] 1d.2.6 GREEN: implement `run()` per AD10: early return while `running()`; clear `result`/`error`;
  `getMultiStart` via `takeUntilDestroyed(destroyRef)`; on success `result.set(dto)`, `lastQuery`
  already snapshotted on activation; on error set the mapped `FtmoRequestError` and clear any stale
  result. Confirm 1d.2.1–1d.2.5 pass.
  _Satisfies: design.md AD10._
- [ ] 1d.2.7 Falsification: temporarily omit the `running()` early-return guard in `run()`; confirm
  1d.2.2 goes RED (a second `expectOne` now exists); restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1d.3 — Panel composition and layout

- [ ] 1d.3.1 RED: `..._a successful multi-start result renders two separately labelled panels, Deploy
  then Evaluation` — asserts `panels = computed(() => toPanels(result()))` drives exactly two
  `FtmoRunPanelComponent` instances in kind order.
  _Satisfies: spec.md "Deploy and Eval render as separate panels"; design.md AD9._
- [ ] 1d.3.2 RED: `..._a missing run kind renders NO_RUN_HELD in that slot instead of omitting it` —
  a DTO with only one run present; asserts the second slot renders the `NO_RUN_HELD` key.
  _Satisfies: design.md AD9._
- [ ] 1d.3.3 RED: `..._the disclosure block is visible before any run has completed` — fresh fixture,
  no run activated; asserts the i18n-keyed disclosure block is present.
  _Satisfies: spec.md "The disclosure block is visible before any run"._
- [ ] 1d.3.4 GREEN: wire `panels = computed(() => toPanels(result()))`, the `.ftmo-sim__runs` grid
  layout per AD9 (`repeat(auto-fit, minmax(min(100%, 30rem), 1fr))`), the always-visible disclosure
  block, and the two fixed Deploy/Evaluation slots. Confirm 1d.3.1–1d.3.3 pass.
  _Satisfies: design.md AD3, AD9._
- [ ] 1d.3.5 Falsification: temporarily render only `panels()` present entries (skip missing kinds)
  instead of two fixed slots; confirm 1d.3.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1d.4 — account-detail wiring

- [ ] 1d.4.1 RED: `account-detail.component.spec.ts` — `triggering the FTMO simulation action on a
  strategy row opens the modal scoped to that strategy` — asserts `ftmoTargetStrategy()` is set to the
  clicked row's strategy and the modal host (`@if`) renders.
  _Satisfies: spec.md "Opening the modal from a strategy row"._
- [ ] 1d.4.2 RED: `..._the action passes strategyId, strategyName, sqxSymbol verbatim from
  strategy.symbol, and broker from account()?.broker` — asserts the modal's inputs match the row's
  strategy and the account's broker.
  _Satisfies: design.md AD8; proposal.md "Defaults to confirm" #1–2._
- [ ] 1d.4.3 RED: `..._the action is shown on every row, regardless of broker` — asserts the action is
  present even for a non-FTMO account (per AD8's rejection of magic-string gating).
  _Satisfies: design.md AD8._
- [ ] 1d.4.4 RED: `..._closing the modal destroys it via @if, cancelling any in-flight request` —
  asserts `ftmoTargetStrategy()` resets to falsy and the modal component is removed from the DOM.
  _Satisfies: design.md AD1, AD10._
- [ ] 1d.4.5 GREEN: add the row action (i18n title, `FTMO_SIMULATION.*` key) to
  `account-detail.component.html`, the `ftmoTargetStrategy` signal and `@if (ftmoTargetStrategy(); as
  t)` host to `account-detail.component.ts`, following the Analytics/Monthly modal precedent
  (`account-detail.component.html:446-452`, `:97`, `:482-527`). Confirm 1d.4.1–1d.4.4 pass.
  _Satisfies: design.md AD1, AD8; spec.md "The Modal Opens From The Strategy Row"._
- [ ] 1d.4.6 Falsification: temporarily hide the row action behind `account()?.broker === 'FTMO'`;
  confirm 1d.4.3 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7; design.md AD8 rejection rationale._

### Phase 1d.5 — PR1d gates

- [ ] 1d.5.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.html src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.scss src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.spec.ts src/app/features/broker-accounts/account-detail/account-detail.component.ts src/app/features/broker-accounts/account-detail/account-detail.component.html src/app/features/broker-accounts/account-detail/account-detail.component.spec.ts` — no diffs.
- [ ] 1d.5.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [ ] 1d.5.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR1d specs plus every new
  PR1d spec pass, **0 existing assertions edited**. This is also PR1's own completion gate: confirm
  proposal.md's Success Criteria for PR1 (Deploy/Eval separate, all six outcomes with zero counts,
  every refusal path including value-0 ones, disclosure/MonthsWithoutStart/Start-1 always shown,
  banned-word test green) are all covered by passing specs.
- [ ] 1d.5.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching.

---

## PR2a — Single-start client, models, and mappers (`FtmoBreachVerdict`, `BreachContingencyCause`, `getSingleStart`)

Do not start PR2a until PR1d's Phase 1d.5 gates are all green. Modifies `ftmo-simulation.model.ts`,
`ftmo-simulation.service.ts`, `ftmo-simulation.mappers.ts`, and the i18n namespace — **the PR1
spec files stay untouched** (design.md File Changes: "the PR1 spec file stays untouched" for the
container; the model/service/mapper spec files gain new tests but keep every existing assertion
unedited).

### Phase 2a.1 — New enums

- [ ] 2a.1.1 RED: `ftmo-simulation.model.spec.ts` (additions only, no existing assertion edited) —
  `FtmoBreachVerdict and BreachContingencyCause pin their exact numeric values` — asserts
  `BreachContingent === 0`, `Breached === 1`, `NoBreachObserved === 2`, and
  `ConcurrentOpenPosition === 0` through `FxRoundingSensitive === 5`.
  _Satisfies: design.md "Enum mirrors" table, PR2 row; hard rule 2._
- [ ] 2a.1.2 GREEN: add `FtmoBreachVerdict` and `BreachContingencyCause` enums to
  `ftmo-simulation.model.ts`. Confirm 2a.1.1 passes.
  _Satisfies: design.md AD2._
- [ ] 2a.1.3 Falsification: temporarily set `BreachContingent = 1` (off-by-one); confirm 2a.1.1 goes
  RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 2a.2 — Single-start DTOs and `getSingleStart`

- [ ] 2a.2.1 GREEN: add the single-start wire DTO interfaces (verdict, findings, first-breach timing,
  challenge-race phase 1/phase 2 outcomes and dates) mirroring `ftmo-breach` and `ftmo-challenge-race`
  response shapes 1:1. No test needed for pure interface shapes.
  _Satisfies: design.md AD3, D5._
  _Note (from PR1a correction RELIABILITY-001): `FtmoChallengePhaseDto` and `FtmoChallengeRulesDto`
  already exist in `ftmo-simulation.model.ts` (PR1a, consumed by the multi-start row/run). PR2a MUST
  reuse them for `FtmoChallengeRaceDto.phase1`/`phase2`/`rules` — never redeclare them. PR2a still
  adds `FtmoChallengeRaceDto` and the other single-start shapes._
- [ ] 2a.2.2 RED: `ftmo-simulation.service.spec.ts` (additions only) — `getSingleStart sends the same
  8+fx params as getMultiStart, against the single-start endpoints` — asserts the request URL/params.
  _Satisfies: design.md AD12._
- [ ] 2a.2.3 RED: `..._getSingleStart maps 400 and network errors the same way as getMultiStart` —
  reuses the same error-mapping assertions as 1a.3.3/1a.3.4 against the new method.
  _Satisfies: design.md AD10 (shared error mapping)._
- [ ] 2a.2.4 GREEN: implement `FtmoSimulationService.getSingleStart(strategyId, query)`, reusing
  `buildFtmoQueryParams` and the shared error mapper. Confirm 2a.2.2–2a.2.3 pass.
  _Satisfies: design.md AD12._
- [ ] 2a.2.5 Falsification: temporarily point `getSingleStart` at the multi-start URL; confirm 2a.2.2
  goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 2a.3 — Single-start mappers and i18n

- [ ] 2a.3.1 RED: `ftmo-simulation.mappers.spec.ts` (additions only) — `toSingleStartVm maps the
  verdict, findings, first-breach timing, and both challenge-race phases into a view model` — a
  fixture DTO; asserts every field is present and correctly shaped in the VM.
  _Satisfies: spec.md "The single-start section shows verdict, findings, first-breach timing, and race
  phases"._
- [ ] 2a.3.2 RED: `..._BreachContingent (value 0) renders its correct verdict label, not treated as
  absent` — asserts the label accessor for `FtmoBreachVerdict` at value `0`.
  _Satisfies: hard rule 2._
- [ ] 2a.3.3 RED: `..._ConcurrentOpenPosition (value 0) renders its correct contingency-cause label`
  — asserts the label accessor for `BreachContingencyCause` at value `0`.
  _Satisfies: hard rule 2._
- [ ] 2a.3.4 RED: `..._a challenge-race phase date (DateOnly yyyy-MM-dd) is formatted by string slice,
  never new Date` — same UTC-3 hazard pin as 1b.3.10, applied to the single-start phase dates.
  _Satisfies: hard rule 6._
- [ ] 2a.3.5 GREEN: implement `toSingleStartVm`. Confirm 2a.3.1–2a.3.4 pass.
  _Satisfies: design.md D5, AD3._
- [ ] 2a.3.6 RED (i18n): `ftmo-simulation.i18n.spec.ts` (additions only) — extend the EN/ES parity and
  banned-wording sweeps to cover the new single-start `FTMO_SIMULATION.*` keys (verdict labels,
  contingency-cause labels, race-phase labels).
  _Satisfies: hard rule 3, hard rule 4._
- [ ] 2a.3.7 GREEN: add the single-start keys to `en.json`/`es.json`. Confirm 2a.3.6 passes (and
  1b.1.1–1b.1.2 stay green, unedited).
  _Satisfies: design.md AD11._
- [ ] 2a.3.8 Falsification: temporarily insert "seguro" into one new ES key; confirm 2a.3.6 goes RED;
  revert; confirm green again.
  _Satisfies: hard rule 7._

### Phase 2a.4 — PR2a gates

- [ ] 2a.4.1 `npx prettier --check src/app/core/models/ftmo-simulation.model.ts src/app/core/models/ftmo-simulation.model.spec.ts src/app/core/services/ftmo-simulation.service.ts src/app/core/services/ftmo-simulation.service.spec.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.mappers.spec.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation.i18n.spec.ts public/assets/i18n/en.json public/assets/i18n/es.json` — no diffs.
- [ ] 2a.4.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [ ] 2a.4.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR2a specs (including
  every PR1a–1d spec) plus every new PR2a spec pass, with **0 existing assertions edited**, per
  design.md's `ftmo-simulation-modal.single-start.spec.ts` note ("the PR1 spec file stays untouched").
- [ ] 2a.4.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching.

---

## PR2b — Single-start detail component and container wiring (`FM/ftmo-single-start-detail/*`, container `.ts`/`.html`, `ftmo-simulation-modal.single-start.spec.ts`)

Do not start PR2b until PR2a's Phase 2a.4 gates are all green.

### Phase 2b.1 — `FtmoSingleStartDetailComponent`

- [ ] 2b.1.1 RED: `ftmo-single-start-detail.component.spec.ts` — `renders the verdict, findings,
  first-breach timing, and phase 1/phase 2 outcomes and dates` — a fixture VM; asserts each element is
  present and correctly labelled.
  _Satisfies: spec.md "The single-start section shows verdict, findings, first-breach timing, and race
  phases"._
- [ ] 2b.1.2 RED: `..._a BreachContingent verdict (value 0) renders its label, not blank` — asserts
  the rendered verdict text for value `0`.
  _Satisfies: hard rule 2._
- [ ] 2b.1.3 RED: `..._every visible string resolves to an i18n key present in both en.json and
  es.json` — renders in both locales; asserts no raw key or hardcoded string.
  _Satisfies: spec.md "Every rendered string resolves to an i18n key in both languages"._
- [ ] 2b.1.4 GREEN: implement `FtmoSingleStartDetailComponent` (OnPush, `input.required<...>()`).
  Confirm 2b.1.1–2b.1.3 pass.
  _Satisfies: design.md AD5, D5._
- [ ] 2b.1.5 Falsification: temporarily hardcode the verdict label as a literal string instead of an
  i18n lookup; confirm 2b.1.3 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 2b.2 — Container wiring, additive-only (the PR1 spec file stays untouched)

- [ ] 2b.2.1 RED: `ftmo-simulation-modal.single-start.spec.ts` (new file, per design.md — the PR1
  container spec file is never edited) — `a "Show single-start detail" action is enabled only after a
  multi-start success` — asserts the action is disabled before any run and before an errored run, and
  enabled after a successful multi-start result.
  _Satisfies: design.md AD12._
- [ ] 2b.2.2 RED: `..._activating the action calls getSingleStart with lastQuery(), and has its own
  in-flight flag independent of the multi-start Run flag` — asserts the request uses the snapshotted
  query and that the main Run button's disabled state is unaffected by the single-start request being
  in flight.
  _Satisfies: design.md AD12 (own in-flight flag, `lastQuery` consistency)._
- [ ] 2b.2.3 RED: `..._adding the single-start section does not alter any PR1-rendered element or
  behaviour` — re-runs the PR1d panel/disclosure/outcome-bar assertions (imported/duplicated, not
  edited in the PR1 spec file) against a fixture that also has the single-start section rendered;
  asserts identical PR1 output.
  _Satisfies: spec.md "Adding the single-start section leaves the multi-start view unchanged"._
- [ ] 2b.2.4 GREEN: add the "Show single-start detail" action and section to the container's
  `.ts`/`.html`, calling `getSingleStart(strategyId, lastQuery())` per AD12, composing
  `FtmoSingleStartDetailComponent` below the existing runs section. Confirm 2b.2.1–2b.2.3 pass, and
  confirm every PR1d test in `ftmo-simulation-modal.component.spec.ts` (untouched) still passes
  unedited.
  _Satisfies: design.md AD12; spec.md "The PR2 Single-Start Detail Section Is Additive..."._
- [ ] 2b.2.5 Falsification: temporarily enable the single-start action before any multi-start success;
  confirm 2b.2.1 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 2b.3 — PR2b gates (also PR2's and the whole change's completion gate)

- [ ] 2b.3.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-single-start-detail/ src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.html src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.single-start.spec.ts` — no diffs.
- [ ] 2b.3.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [ ] 2b.3.3 `npx ng test --watch=false` (full suite, once) — confirm every spec across PR1a–PR2b
  passes, with **0 existing assertions edited anywhere in the change**, and specifically confirm
  `ftmo-simulation-modal.component.spec.ts` (the PR1 container spec) is byte-identical to its PR1d
  state.
- [ ] 2b.3.4 Confirm proposal.md's Success Criteria checklist end to end: Deploy/Eval separate with
  all six outcomes (incl. zero counts); every refusal path incl. value-0 ones, with
  InstrumentSpecMissing/FxRateNotDeclared/InvalidFxBand each distinct; disclosure,
  MonthsWithoutStart, and Start-1 flag always shown; banned-word test green on EN/ES; PR2 leaves the
  PR1 view/tests unchanged.
- [ ] 2b.3.5 If any pre-existing spec fails, a prettier/tsc diff appears, or the PR1 container spec
  file shows any diff, STOP and investigate before patching.

---

## Spec-to-task mapping (17 requirements → tasks, none unmapped)

| Spec requirement | Slice | Tasks |
|---|---|---|
| The Modal Opens From The Strategy Row | 1d | 1d.4.1, 1d.4.5 |
| The Source Lot Grid Is Prefilled From The IMOX Retester Constant... | 1a, 1d | 1a.1.3–1a.1.5, 1d.1.1–1d.1.2, 1d.1.6 |
| Required Fields Are Validated Before Run | 1d | 1d.1.3–1d.1.7 |
| A Simulation Runs Only On Explicit Run, And A Second Run Is Blocked... | 1d | 1d.2.1–1d.2.7 |
| Deploy And Eval Render Side By Side, Never Merged | 1c, 1d | 1c.3.8, 1d.3.1, 1d.3.4 |
| The Six Outcome Shares Render With Counts, Zeros Included | 1b, 1c | 1b.3.5, 1c.1.1–1c.1.5 |
| The Order-Statistics Table Renders With The Funded-Duration Headline... | 1b, 1c | 1b.3.6–1b.3.8, 1c.2.1–1c.2.5 |
| A Whole-Run Refusal Shows Its Reason | 1b, 1c | 1b.3.1–1b.3.2, 1c.3.1–1c.3.2 |
| A Race Refusal Of ProfitTargetMismatch Shows Its Stored Value | 1b, 1c | 1b.3.3, 1c.3.3 |
| Months Without A Start Are Disclosed | 1b, 1c | 1b.3.9, 1c.3.4 |
| The Start1DiffersFromSingleStartAnchor Flag Is Disclosed | 1b, 1c | 1b.3.11, 1c.3.5 |
| The Disclosure Text Is Always Visible, And Is Shown Verbatim As Data | 1b, 1c, 1d | 1b.3.12, 1c.3.6, 1d.3.3 |
| An HTTP Error Or 400 Response Is Shown, Never Silently Swallowed | 1a, 1d | 1a.3.3–1a.3.4, 1d.2.3–1d.2.4 |
| The PR2 Single-Start Detail Section Is Additive, Never Changing The PR1 View | 2b | 2b.2.1–2b.2.5 |
| Banned Wording Is Excluded From The FTMO i18n Keys | 1b, 2a | 1b.1.2–1b.1.3, 2a.3.6 |
| Every FTMO Enum Is Mapped By Exact Value, Never By Truthiness | 1a, 1b, 2a | 1a.1.1, 1b.2.1–1b.2.5, 2a.1.1, 2a.3.2–2a.3.3 |
| Every Visible String Comes From i18n, In EN And ES, Except The Server's Disclosure Text | 1b, 1c, 2a, 2b | 1b.1.1, 1c.1.3, 2a.3.6, 2b.1.3 |
