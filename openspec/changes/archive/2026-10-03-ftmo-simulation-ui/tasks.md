# Tasks: FTMO simulation UI

Strict TDD throughout: every RED precedes its GREEN, and every RED must be demonstrably able to fail
(falsification is mandatory, not optional — see the falsification task in every slice). Four chained
slices per design.md ("File Changes" / "Migration / Rollout"): **PR1a** (client + models) → **PR1b**
(mappers + i18n) → **PR1c** (presentational panels) → **PR1d** (container modal + wiring). **PR2a/PR2b
(the single-start detail) are DEFERRED (2026-10-03) and outside this change** — see the "DEFERRED"
section below. Each slice starts only
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
   for every date-bearing formatter (`MonthsWithoutStart` entries, order-stat labels if date-bearing).
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
| PR2a / PR2b estimated lines | DEFERRED (2026-10-03), not part of this change (were ~400 / ~450) |
| 400-line budget risk | High (1b, 1c, 1d) — flagged per `ask-on-risk`, not blocking; the user already accepted the slice plan in design.md's Sizing table |
| Chained slices | Yes — 1a → 1b → 1c → 1d, per design.md "Migration / Rollout" (2a, 2b deferred) |
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

- [x] 1c.1.1 RED: `ftmo-outcome-bars.component.spec.ts` — `renders exactly six outcome rows, one per
  chain outcome, in the fixed order` — TestBed + `TranslateModule.forRoot()`, an `input.required` VM
  with all six rows; asserts exactly six `.ftmo-sim__outcome-row` (or equivalent BEM) elements, in
  order.
  _Satisfies: spec.md "All six outcomes are always present"._
  _Apply note: RED confirmed — `Cannot find module './ftmo-outcome-bars.component'` (TS2307) before
  the file existed._
- [x] 1c.1.2 RED: `..._a zero-count row still renders its count and 0% share, not omitted` — asserts
  the zero row's rendered text includes "0" for both count and share.
  _Satisfies: spec.md "A zero-count outcome is still rendered"._
- [x] 1c.1.3 RED: `..._every share bar label resolves to an i18n key present in both locales` —
  renders in EN then ES (or asserts each label's translate key against both loaded JSON files);
  asserts no raw/untranslated key string is shown.
  _Satisfies: spec.md "Every Visible String Comes From i18n"; hard rule 4._
  _Apply note: no `TranslateLoader` is configured in the test module, so ngx-translate renders each
  key literally rather than the EN/ES text — the assertion checks that key text is present (the
  `SOURCE_PLATFORM_UNDECLARED_NOTE` precedent in `import-strategy-backtests-modal.component.spec.ts`),
  plus a separate EN/ES JSON-lookup check that both locale values resolve and are non-empty._
- [x] 1c.1.4 GREEN: implement `FtmoOutcomeBarsComponent` (OnPush, `input.required<FtmoOutcomeRowVm[]>()`
  or equivalent), six BEM/CSS share bars, count beside each share, no green colour (AD9). Confirm
  1c.1.1–1c.1.3 pass.
  _Satisfies: design.md AD5, AD9; proposal.md D3._
  _Apply note: 3/3 tests green. Count and share are rendered as plain interpolated text (not through
  an interpolated i18n string), because the test module carries no loader and interpolation params
  never resolve without one — new `FTMO_SIMULATION.OUTCOME.COUNT_LABEL`/`SHARE_LABEL` keys (added to
  both `en.json`/`es.json`) label the two numbers. Bar fill uses `--color-neutral`, never
  `--color-gain`._
  _Correction (RELIABILITY-001/002): the "params never resolve without a loader" rationale was wrong
  for production — the app ships `provideTranslateHttpLoader`, and params already resolve elsewhere
  (`backtests-list.component.html:57`). The keys now read `Count: {{count}}` / `Share: {{share}}%`
  (ES `Cantidad: {{count}}` / `Porcentaje: {{share}}%`) and the template passes the params, per
  `frontend-data.md` "Interpolation (never concatenation)". See 1c.5._
- [x] 1c.1.5 Falsification: temporarily filter out rows with `count === 0` before rendering; confirm
  1c.1.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (`expected 5 to be 6`); restored; 3/3 green again._

### Phase 1c.2 — `FtmoOrderStatsTableComponent`

- [x] 1c.2.1 RED: `ftmo-order-stats-table.component.spec.ts` — `renders a plain semantic <table> with
  N/Min/Q1/Median/Q3/Max columns for phase-1, phase-2, funded (funded-start headline), funded
  (chain-start, secondary), and censored-runway rows` — asserts a `<table>` element (not a grid
  component) with the expected row count and headline/secondary distinction (e.g. a `--secondary`
  modifier class or muted styling hook on the chain-start row).
  _Satisfies: spec.md "The Order-Statistics Table Renders With The Funded-Duration Headline..."._
  _Apply note: RED confirmed — `Cannot find module './ftmo-order-stats-table.component'` (TS2307)
  before the file existed._
- [x] 1c.2.2 RED: `..._an N=0 row renders every quantile as "—", never "0"` — asserts rendered cell
  text.
  _Satisfies: spec.md zero-observation scenario._
- [x] 1c.2.3 RED: `..._a Min=0 row with N>0 renders "0", not "—"` — asserts the distinction is
  preserved at render time, not just in the VM.
  _Satisfies: design.md Data Flow, "0 renders '0'"._
- [x] 1c.2.4 GREEN: implement `FtmoOrderStatsTableComponent` (OnPush, `input.required<...>()`), a
  plain `<table>` (no Prizm grid, per AD5's rejection of a data-grid component for six rows). Confirm
  1c.2.1–1c.2.3 pass.
  _Satisfies: design.md AD5; proposal.md D3 (grid rejection rationale)._
  _Apply note: 3/3 tests green. `cellKey(value)` returns the `ORDER_STATS.ABSENT` key only for `null`;
  the template's `@if/@else` renders the raw numeric value (including `0`) on the `@else` branch, so a
  `0` is never routed through the falsy-`@if` trap that would also swallow it._
- [x] 1c.2.5 Falsification: temporarily render `stat.min ?? 0` instead of the "—" sentinel; confirm
  1c.2.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (`expected '  ' to contain 'FTMO_SIMULATION.ORDER_STATS.ABSENT'`
  — `cellKey` was temporarily made a no-op); restored; 3/3 green again._

### Phase 1c.3 — `FtmoRunPanelComponent` (per-run composition: refusal / race-refusal / no-starts / evaluated)

- [x] 1c.3.1 RED: `ftmo-run-panel.component.spec.ts` — `state "refused" renders the refusal reason and
  no outcome bars or order-stats table` — asserts the bars/table children are absent (`@if` false) and
  the refusal message text is present.
  _Satisfies: spec.md "A refused run shows its reason, not empty results"._
  _Apply note: RED confirmed — `Cannot find module './ftmo-run-panel.component'` (TS2307) before the
  file existed._
- [x] 1c.3.2 RED: `..._InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render distinct
  text` — three fixtures; asserts three distinct rendered strings.
  _Satisfies: spec.md "InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render a
  distinct message"._
- [x] 1c.3.3 RED: `..._state "raceRefused" renders ProfitTargetMismatch alongside the stored value
  0.08` — asserts both the refusal label and the numeric value appear.
  _Satisfies: spec.md "ProfitTargetMismatch is shown with the stored value"._
- [x] 1c.3.4 RED: `..._monthsWithoutStart renders the months and their count when non-empty` — 3
  entries; asserts all 3 plus the count "3" are rendered.
  _Satisfies: spec.md "Months without a start are listed with their count"._
- [x] 1c.3.5 RED: `..._Start1DiffersFromSingleStartAnchor=true renders an explicit disclosure` —
  asserts the disclosure text/element is present.
  _Satisfies: spec.md "A divergent start 1 is disclosed"._
- [x] 1c.3.6 RED: `..._the run's Disclosure and NotModelled text are always visible, shown verbatim,
  with no collapsed accordion` — asserts the text node is present in the rendered DOM without
  simulating a click/expand interaction.
  _Satisfies: spec.md "Disclosure is visible on a normal run"; design.md AD11._
- [x] 1c.3.7 RED: `..._state "evaluated" composes FtmoOutcomeBarsComponent and
  FtmoOrderStatsTableComponent as children` — asserts both child components are present with the VM's
  data passed through.
  _Satisfies: design.md AD5; proposal.md D3._
- [x] 1c.3.8 RED: `..._two panels render in two distinct <section> elements, each separately labelled,
  and stack on narrow widths without merging` — renders two `FtmoRunPanelComponent` host sections (or
  a parent fixture with two panels); asserts two separate `<section>`s with distinct labels/headings,
  never a single merged section.
  _Satisfies: spec.md "Deploy and Eval render as separate panels" / "Narrow widths stack but do not
  merge the panels"; design.md AD9._
  _Apply note: layout/stacking itself (the `.ftmo-sim__runs` grid) is a PR1d container concern
  (design.md AD9); PR1c proves the component-level precondition — each panel is its own separately
  labelled `<section>`, so two instances never share one section._
- [x] 1c.3.9 GREEN: implement `FtmoRunPanelComponent` (OnPush, `input.required<FtmoRunPanelVm>()`):
  an `@switch` on the VM's state discriminant rendering the refused / race-refused / no-starts /
  evaluated branches, the always-visible disclosure block, `monthsWithoutStart`, and the Start-1 flag
  in every branch. Confirm 1c.3.1–1c.3.8 pass.
  _Satisfies: design.md AD5, AD9, Data Flow ("Every state renders the disclosure,
  monthsWithoutStart..., the start-1 flag, notModelled...")._
  _Apply note: 8/8 tests green. The `<section>`'s `aria-label` is the translated `FTMO_SIMULATION.KIND
  .DEPLOY`/`EVALUATION` key text, so Deploy and Evaluation panels are always distinctly labelled.
  Numeric interpolation (stored profit target) is rendered as a separate plain-text span next to the
  translated label, not through an i18n interpolation param, for the same no-loader-in-test reason as
  1c.1.4._
  _Correction (RELIABILITY-001): that approach was a defect — `STORED_PROFIT_TARGET` is
  `"Stored profit target: {{value}}"`, so production rendered `Stored profit target: {{value}}: 0.08`.
  The template now passes `{ value: storedProfitTargetPct }`. See 1c.5._
- [x] 1c.3.10 Falsification: temporarily wrap the disclosure block in an `@if (expanded())` gated by a
  click handler; confirm 1c.3.6 goes RED (text absent without a simulated click); restore; confirm
  green again.
  _Satisfies: hard rule 7._
  _Apply note: falsification confirmed RED (`expected '...' to contain 'Server disclosure text'` — the
  disclosure paragraph disappeared behind `expanded = false`); restored; 8/8 green again._

### Phase 1c.4 — PR1c gates

- [x] 1c.4.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-outcome-bars/ src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-order-stats-table/ src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-run-panel/` — no diffs.
- [x] 1c.4.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [x] 1c.4.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR1c specs plus every new
  PR1c spec pass, **0 existing assertions edited**.
  _Apply note: baseline (pre-PR1c) was 37 test files / 461 tests, all passing. After PR1c: 40 test
  files / 475 tests, all passing — the +3 files / +14 tests are exactly the 3 new PR1c spec files (3
  outcome-bars + 3 order-stats-table + 8 run-panel); every pre-existing test file and count is
  unchanged._
- [x] 1c.4.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching. (N/A — prettier initially flagged 4 files, mechanical formatting only, fixed with
  `--write`; re-check passed clean; no test failures or tsc errors occurred.)

### Phase 1c.5 — Correction: i18n placeholder leaks (RELIABILITY-001 BLOCKER, RELIABILITY-002 WARNING)

- [x] 1c.5.1 RED: real-dictionary tests (`TranslateService.setTranslation('en'|'es', <real json>)` +
  `use(...)`, the `portfolio-detail.component.spec.ts` precedent) added to all three PR1c specs,
  asserting the final text and that no `{{` appears in the rendered component.
  _Apply note: RED against the unchanged template —
  `expected 'Stored profit target: {{value}}: 0.08' to be 'Stored profit target: 0.08'` (EN) and
  `Received: "Objetivo de ganancia almacenado: {{value}}: 0.08"` (ES)._
- [x] 1c.5.2 GREEN: `ftmo-run-panel.component.html` passes `{ value: storedProfitTargetPct }` to
  `STORED_PROFIT_TARGET`; the manual `:` and the sibling number are removed.
  _Apply note: format is the raw stored fraction (`0.08`), not a percent — spec.md "ProfitTargetMismatch
  is shown with the stored value" states the rendered value is `0.08` literally, so the spec is not
  silent and wins over the percent default._
- [x] 1c.5.3 `ftmo-outcome-bars` converted from label+number concatenation to interpolated keys
  (`COUNT_LABEL` `{{count}}`, `SHARE_LABEL` `{{share}}%`, EN+ES). The forRoot-only zero-row test moved
  into the real-dictionary block (without a dictionary the number no longer renders); its assertion is
  now the stronger `Count: 0 Share: 0%`. The old forRoot-only race-refusal test was replaced by its
  real-dictionary version.
- [x] 1c.5.4 Key audit of every `{{param}}` key in `FTMO_SIMULATION` vs PR1c usage:
  `STORED_PROFIT_TARGET` fixed; `MONTHS_WITHOUT_START` already passed `count` (now guarded by a real
  dictionary test); `COUNT_LABEL`/`SHARE_LABEL` receive `count`/`share`; `ERRORS.INVALID_QUERY` is
  not rendered in PR1c (PR1d).
- [x] 1c.5.5 `UNKNOWN_VALUE` (`"Unknown value ({{value}})"`) leak — fixed under an explicit user
  authorization to touch PR1b (the one exception). `toRunPanelVm` mapped an unknown refusal / race
  refusal to `UNKNOWN_VALUE_KEY` but dropped the raw number, so the panel had no value to pass.
  _Apply note: the VM now carries `refusalValue: FtmoSimulationRefusal | null` and
  `raceRefusalValue: FtmoChallengeRaceRefusal` (raw DTO values). The run panel passes
  `{ value: refusalValue ?? ('FTMO_SIMULATION.NOT_REPORTED' | translate) }` and `{ value:
  raceRefusalValue }`, and known keys ignore the param. The `?? NOT_REPORTED` fallback covers a Refused
  run with a null refusal (a contract violation), so the placeholder cannot leak there either.
  Tests were only added; no existing PR1b assertion was edited. RED (mapper): compile failure TS2339 /
  TS2551, `refusalValue`/`raceRefusalValue` do not exist. RED (panel, after mapper GREEN):
  `expected 'Unknown value ({{value}})' to be 'Unknown value (999)'`, the same for the race refusal
  and the null-refusal case. GREEN: 54/54 FTMO-modal specs. Falsification: mapper set to
  `refusalValue: null` / `raceRefusalValue: undefined` gave 5 failed (`expected null to be 999`,
  `expected 'Unknown value ({{value}}) Stored prof…' to be 'Unknown value (999) …'`); restored
  byte-identical; green again._
- [x] 1c.5.6 Gates: prettier --check on touched files clean; tsc --noEmit exit 0; full `ng test`
  40 files / 481 tests passed (475 + 6 net new); `ftmo-simulation.i18n.spec.ts` parity and
  banned-wording 3/3 green. After 1c.5.5, re-run: prettier clean, tsc exit 0, `ng test` 40 files /
  487 tests passed (481 + 3 mapper + 3 run-panel).

---

## PR1d — Container modal, form, and wiring into account-detail (`FM/ftmo-simulation-modal.component.*`, `account-detail.component.*`)

This is the slice where the feature becomes reachable (design.md's rollout note). All PR1a–1c code
gets its first importer here.

### Phase 1d.1 — Form signals and `canRun`

- [x] 1d.1.1 RED: `ftmo-simulation-modal.component.spec.ts` — `on open, the source-grid fields are
  prefilled from IMOX_RETESTER_LOT_GRID, broker from account context, and initialCapital to 10000` —
  TestBed with a mocked `FtmoSimulationService`; asserts the rendered field values.
  _Satisfies: spec.md "The source lot grid is prefilled from the IMOX retester constant"; hard rule 5._
- [x] 1d.1.2 RED: `..._the source-grid fields stay editable after prefill` — simulates editing
  `maxLots`; asserts the new value is accepted and Run stays available.
  _Satisfies: spec.md "The source-grid fields stay editable"._
- [x] 1d.1.3 RED: `..._Run is disabled while targetRiskPerTrade is empty` — asserts the Run button's
  `disabled` state.
  _Satisfies: spec.md "Run is disabled while targetRiskPerTrade is empty"._
- [x] 1d.1.4 RED: `..._Run is enabled once all 8 required fields are filled, fxLow/fxHigh left empty`
  — asserts Run enabled.
  _Satisfies: spec.md "Run is enabled once all 8 required fields are filled"._
- [x] 1d.1.5 RED: `..._sizeDecimals=0 does not disable Run` — asserts Run stays enabled with
  `sizeDecimals: 0` and every other field filled.
  _Satisfies: spec.md "A zero sizeDecimals value does not disable Run"; hard rule 2._
- [x] 1d.1.6 GREEN: implement the form signals (`number | null` per field, `string` for
  broker/sqxSymbol), prefill on construction/open from `IMOX_RETESTER_LOT_GRID` and account context,
  and `canRun = computed(...)` per AD6 (all 8 required fields `!== null` and finite, `!running()`).
  Confirm 1d.1.1–1d.1.5 pass.
  _Satisfies: design.md AD6, AD7, AD8._
- [x] 1d.1.7 Falsification: temporarily change `canRun`'s `sizeDecimals` check to `!!sizeDecimals()`
  (truthy); confirm 1d.1.5 goes RED; restore; confirm green again.
  _Satisfies: hard rule 2, hard rule 7._

### Phase 1d.2 — Run lifecycle

- [x] 1d.2.1 RED: `..._editing an input after a completed run does not send a new request` — one
  `expectOne` for the initial run, then an edit; asserts `httpTestingController.verify()` finds no
  additional request.
  _Satisfies: spec.md "Editing an input does not trigger a request"._
- [x] 1d.2.2 RED: `..._a second Run activation while one is in flight sends no second request and Run
  stays disabled` — activates Run twice before flushing; asserts exactly one `expectOne` and Run
  disabled until resolution.
  _Satisfies: spec.md "A second Run is blocked while one is in flight"._
- [x] 1d.2.3 RED: `..._a 400 response shows an explicit error, re-enables Run, and renders no partial
  result` — flushes a 400; asserts an error state element is present, Run re-enabled, no run panels
  rendered.
  _Satisfies: spec.md "A 400 response shows an explicit error"._
- [x] 1d.2.4 RED: `..._a network failure shows an explicit error and re-enables Run` — flushes a
  network error; asserts error state and Run re-enabled.
  _Satisfies: spec.md "A network failure shows an explicit error"._
- [x] 1d.2.5 RED: `..._destroying the fixture while a request is in flight cancels the request` —
  destroys the fixture mid-flight; asserts the `HttpTestingController` request was cancelled/aborted
  (no dangling open request at `verify()`).
  _Satisfies: design.md AD10 (`takeUntilDestroyed`)._
- [x] 1d.2.6 GREEN: implement `run()` per AD10: early return while `running()`; clear `result`/`error`;
  `getMultiStart` via `takeUntilDestroyed(destroyRef)`; on success `result.set(dto)`, `lastQuery`
  already snapshotted on activation; on error set the mapped `FtmoRequestError` and clear any stale
  result. Confirm 1d.2.1–1d.2.5 pass.
  _Satisfies: design.md AD10._
- [x] 1d.2.7 Falsification: temporarily omit the `running()` early-return guard in `run()`; confirm
  1d.2.2 goes RED (a second `expectOne` now exists); restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1d.3 — Panel composition and layout

- [x] 1d.3.1 RED: `..._a successful multi-start result renders two separately labelled panels, Deploy
  then Evaluation` — asserts `panels = computed(() => toPanels(result()))` drives exactly two
  `FtmoRunPanelComponent` instances in kind order.
  _Satisfies: spec.md "Deploy and Eval render as separate panels"; design.md AD9._
- [x] 1d.3.2 RED: `..._a missing run kind renders NO_RUN_HELD in that slot instead of omitting it` —
  a DTO with only one run present; asserts the second slot renders the `NO_RUN_HELD` key.
  _Satisfies: design.md AD9._
- [x] 1d.3.3 RED: `..._the disclosure block is visible before any run has completed` — fresh fixture,
  no run activated; asserts the i18n-keyed disclosure block is present.
  _Satisfies: spec.md "The disclosure block is visible before any run"._
- [x] 1d.3.4 GREEN: wire `panels = computed(() => toPanels(result()))`, the `.ftmo-sim__runs` grid
  layout per AD9 (`repeat(auto-fit, minmax(min(100%, 30rem), 1fr))`), the always-visible disclosure
  block, and the two fixed Deploy/Evaluation slots. Confirm 1d.3.1–1d.3.3 pass.
  _Satisfies: design.md AD3, AD9._
- [x] 1d.3.5 Falsification: temporarily render only `panels()` present entries (skip missing kinds)
  instead of two fixed slots; confirm 1d.3.2 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7._

### Phase 1d.4 — account-detail wiring

- [x] 1d.4.1 RED: `account-detail.component.spec.ts` — `triggering the FTMO simulation action on a
  strategy row opens the modal scoped to that strategy` — asserts `ftmoTargetStrategy()` is set to the
  clicked row's strategy and the modal host (`@if`) renders.
  _Satisfies: spec.md "Opening the modal from a strategy row"._
- [x] 1d.4.2 RED: `..._the action passes strategyId, strategyName, sqxSymbol verbatim from
  strategy.symbol, and broker from account()?.broker` — asserts the modal's inputs match the row's
  strategy and the account's broker.
  _Satisfies: design.md AD8; proposal.md "Defaults to confirm" #1–2._
- [x] 1d.4.3 RED: `..._the action is shown on every row, regardless of broker` — asserts the action is
  present even for a non-FTMO account (per AD8's rejection of magic-string gating).
  _Satisfies: design.md AD8._
- [x] 1d.4.4 RED: `..._closing the modal destroys it via @if, cancelling any in-flight request` —
  asserts `ftmoTargetStrategy()` resets to falsy and the modal component is removed from the DOM.
  _Satisfies: design.md AD1, AD10._
- [x] 1d.4.5 GREEN: add the row action (i18n title, `FTMO_SIMULATION.*` key) to
  `account-detail.component.html`, the `ftmoTargetStrategy` signal and `@if (ftmoTargetStrategy(); as
  t)` host to `account-detail.component.ts`, following the Analytics/Monthly modal precedent
  (`account-detail.component.html:446-452`, `:97`, `:482-527`). Confirm 1d.4.1–1d.4.4 pass.
  _Satisfies: design.md AD1, AD8; spec.md "The Modal Opens From The Strategy Row"._
- [x] 1d.4.6 Falsification: temporarily hide the row action behind `account()?.broker === 'FTMO'`;
  confirm 1d.4.3 goes RED; restore; confirm green again.
  _Satisfies: hard rule 7; design.md AD8 rejection rationale._

### Phase 1d.5 — PR1d gates

- [x] 1d.5.1 `npx prettier --check src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.ts src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.html src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.scss src/app/features/broker-accounts/ftmo-simulation-modal/ftmo-simulation-modal.component.spec.ts src/app/features/broker-accounts/account-detail/account-detail.component.ts src/app/features/broker-accounts/account-detail/account-detail.component.html src/app/features/broker-accounts/account-detail/account-detail.component.spec.ts` — no diffs.
- [x] 1d.5.2 `npx tsc --build --emitDeclarationOnly false --noEmit` — zero type errors.
- [x] 1d.5.3 `npx ng test --watch=false` (full suite, once) — confirm all pre-PR1d specs plus every new
  PR1d spec pass, **0 existing assertions edited**. This is also PR1's own completion gate: confirm
  proposal.md's Success Criteria for PR1 (Deploy/Eval separate, all six outcomes with zero counts,
  every refusal path including value-0 ones, disclosure/MonthsWithoutStart/Start-1 always shown,
  banned-word test green) are all covered by passing specs.
- [x] 1d.5.4 If any pre-existing spec fails or a prettier/tsc diff appears, STOP and investigate before
  patching.

### Phase 1d.6 — Correction: modal error-text rendering (RELIABILITY-101 CRITICAL)

- [x] 1d.6.1 RED: real-dictionary block (`setTranslation('en'|'es', <real json>)` + `use(...)`, the
  1c.5 precedent) added to `ftmo-simulation-modal.component.spec.ts`: 400 `{ message: 'X' }` renders
  exactly `The request was rejected: X` / `La solicitud fue rechazada: X`; network failure renders
  `REQUEST_FAILED` in EN and ES; 400 with `{}` and with `{ message: null }`; Evaluated result with
  both panels (EN and ES) contains no `{{` and no raw `FTMO_SIMULATION.`; form labels/legend, the
  RUNNING state, and `NO_RUN_HELD` (EN and ES) render dictionary text.
  _Apply note: RED against the unchanged template only on the null-detail cases —
  `expected 'The request was rejected: null' to be 'The request was rejected: Not reported'` and
  `expected 'La solicitud fue rechazada: null' ...`. ngx-translate 17 `formatValue(null)` returns the
  literal `"null"` (not the `{{detail}}` placeholder); the other 8 new tests were already green._
- [x] 1d.6.2 GREEN: `errorParams(err, notReported)` returns `{ detail: err.detail ?? notReported }`;
  the template passes `'FTMO_SIMULATION.NOT_REPORTED' | translate` (the 1c.5.5 null fallback).
  No i18n JSON change.
- [x] 1d.6.3 Falsification: temporarily rendered `errorFullKey(e) | translate` without params; 4 error
  tests went RED (`expected 'The request was rejected: {{detail}}' to be 'The request was rejected:
  X'`, and ES); restored; 26/26 green again.
- [x] 1d.6.4 Gates: prettier --check on the 3 touched modal files clean; tsc --noEmit exit 0; full
  `ng test` 41 files / 518 tests passed (508 + 10 new).
  _Note (RELIABILITY-102, WARNING, not changed): the sibling row-action buttons in
  `account-detail.component.ts` use hardcoded English `title` literals, so there is no reactive
  pattern to match; `translate.instant` for the FTMO action title stays as is._

---

## DEFERRED (2026-10-03) — PR2a and PR2b, the single-start detail section

**Outside this change.** The user's real goal is a separate portfolio-simulation screen (groups of 2..n
strategies; FTMO, Darwinex Zero and Axi Select; backtest and later live data), so the per-strategy
single-start detail loses priority. Agreed next order: (1) FTMO group simulation on its own screen,
(2) automatic combinations, (3) live data, (4) Darwinex Zero and Axi. See `proposal.md` "Deferred".

These are plain list items, not checkboxes, on purpose: verify and archive must not count them as
missing work. They were never started (no code exists for them). If the single-start detail is wanted
later, it needs its own change with its own spec and tasks; the original RED/GREEN/falsification task
list is not preserved here (the design record is `design.md` AD12 and the Enum mirrors table).

- DEFERRED PR2a (was ~400 lines): `FtmoBreachVerdict` and `BreachContingencyCause` enums with pinned
  values (including the zero members `BreachContingent` and `ConcurrentOpenPosition`); the single-start
  DTOs, reusing the existing `FtmoChallengePhaseDto` and `FtmoChallengeRulesDto`;
  `FtmoSimulationService.getSingleStart`; `toSingleStartVm` with zero-safe labels and sliced
  `yyyy-MM-dd` phase dates; the single-start i18n keys with the banned-word and parity sweeps.
- DEFERRED PR2b (was ~450 lines): `FtmoSingleStartDetailComponent`; the "Show single-start detail"
  action in the container, enabled only after a multi-start success, with its own in-flight flag and
  `lastQuery()` consistency, delivered additively through a new
  `ftmo-simulation-modal.single-start.spec.ts` so the PR1 container spec stays untouched.

---

## Spec-to-task mapping (16 requirements → tasks, none unmapped; the single-start requirement was removed with the PR2 deferral)

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
| Banned Wording Is Excluded From The FTMO i18n Keys | 1b | 1b.1.2–1b.1.3 |
| Every FTMO Enum Is Mapped By Exact Value, Never By Truthiness | 1a, 1b | 1a.1.1, 1b.2.1–1b.2.5 |
| Every Visible String Comes From i18n, In EN And ES | 1b, 1c | 1b.1.1, 1c.1.3 |

---

## Post-PR1 fixes (2026-10-01)

Four UX defects found while the user tested the modal with real data, plus three fixes (P5 to P7) from the user decision of 2026-10-03 (every modal text must be available in Spanish). Strict TDD (RED, GREEN, falsification each), no backend change.

- [x] P1 Broker prefill: `brokerValue` starts as the constant `FTMO` (`FTMO_DEFAULT_BROKER`); the modal's
  `broker` input and the `[broker]` binding in `account-detail.component.html` were removed; the field
  stays editable. User decision 2026-10-01 (strategies with backtests live in a Darwinex account, so an
  account-derived broker always refused with `LimitsNotConfigured`). Spec requirement and scenario and
  design AD8 amended. _Existing test changed (user-decided behaviour change): the `create()` helper in
  `ftmo-simulation-modal.component.spec.ts` no longer sets the removed `broker` input, and the test
  `onOpen_PrefillsSourceGridFromConstant_BrokerFromAccountContext_...` was renamed `..._BrokerToFtmo_...`
  (its `'FTMO'` assertion is unchanged). The account-detail spec never asserted that the broker was passed._
- [x] P2 Refusal copy: `LIMITS_NOT_CONFIGURED` and `PRODUCT_NOT_TWO_STEP` (EN and ES) now name the broker
  via a `{{broker}}` param. `FtmoRunPanelComponent` takes a `broker` input, fed by the modal's
  `submittedBroker()` (the broker of `lastQuery`, so later edits do not change it). The other refusals
  were checked: none else blames the account (`FX_RATE_NOT_DECLARED` mentions the profit currency, which
  is not a misattribution).
- [x] P3 White panels: root cause, the modal shell read variables that `styles/_variables.scss` never
  declares (`--color-surface`, `--color-text-primary`, ...), so it always painted its hardcoded
  Catppuccin dark fallbacks, while the panels read the real `--bg-surface` and followed the light theme
  (white). The modal now uses `--bg-surface`, `--bg-surface-2`, `--border-color`, `--text-main`,
  `--text-muted`, `--color-primary`; its run-button text no longer uses a hardcoded dark hex.
  `ftmo-simulation.theme.spec.ts` asserts on the compiled CSS that no FTMO stylesheet reads an
  undeclared legacy variable and that the shell uses the theme surface and border variables.
  _Manual check (jsdom does not resolve `var()`): open the modal with `data-theme="dark"` and with the
  light theme, confirm the modal and both panels share one surface colour in each._
- [x] P4 Empty response: `hasNoRuns` (result present, zero panels) renders one
  `NO_BACKTESTS_IMPORTED` message (EN and ES); `NO_RUN_HELD` stays only when exactly one kind is
  missing. Spec scenarios added (zero runs; exactly one missing).
- [x] P5 Disclosure to i18n: the per-panel verbatim server `disclosure` is no longer rendered or carried
  by the panel VM. The modal renders ONE `DISCLOSURE_RESULT` (EN and neutral ES) when the result has
  runs. Spec requirement and design AD11 amended (user decision 2026-10-03; reason: the user wants a
  Spanish UI). _Existing assertions changed (forced by the decision): the panel spec test
  `theRunsDisclosureAndNotModelledText_AreAlwaysVisible_ShownVerbatim_...` now asserts the panel does
  NOT render them; the mappers spec test "the server Disclosure and NotModelled text pass through
  verbatim" now asserts the VM no longer carries them._
- [x] P6 notModelled to i18n labels: `toNotModelledItems` maps the exact values `Swap`, `FtmoCommission`,
  `IntradayEquity` (verified against `FtmoRunSimulationResultDto.DefaultNotModelled`) to
  `NOT_MODELLED.*` labels, UNKNOWN fallback with the raw value as `{{value}}`, deduplicated across runs
  and rendered once beside the disclosure. ES: "Swap", "Comisión de FTMO", "Equity intradía".
- [x] P7 Before the first Run: one `BEFORE_RUN_HINT` replaces the two "No run held" slots (shown only
  while there is no result, no running request and no error). The ES hint names the real ES Run label
  "Ejecutar". "No run held" stays for exactly one missing kind; the zero-runs message is unchanged.
  _No existing assertion changed for P7: no test asserted the two empty slots before a run._
- [x] Gates: prettier clean, `tsc --noEmit` exit 0, `ng test` 42 files / 549 tests (536 + 13 net new).

- [x] P8 Header language toggle shows the active language (ES/EN) and titles what it switches to (`LanguageService.currentLabel` / `switchTitleKey`, keys `LAYOUT.HEADER.SWITCH_TO_EN|ES`); removed the now-unused `TOGGLE_LANG` key and `currentLang` field. RED: TS2339 on the new members; GREEN 550/550. Pre-existing prettier debt in `language.service.spec.ts` left untouched.
