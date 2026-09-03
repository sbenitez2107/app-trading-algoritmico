# Review Policy — trade-risk-normalization (slice 2a, PR1)

## Risk classification: **Medium** → exactly ONE lens

The diff is pure arithmetic over already-persisted rows. It has no authentication, no
authorisation or permission change, no payments, no data-loss or data-exposure path, no
shell/process integration, no VCS or PR automation, and no network or database access. It
introduces no chokepoint that every request passes through.

**Size was not a classifier input**, and is not one under this contract at any magnitude. The
target is roughly 1,900 changed lines; that does not escalate the tier. The change was shipped
whole under an explicit user-granted size exception, which affects human reviewability of the
diff and nothing about lens selection.

## Lens selected: `review-reliability`

Dominant risk is behaviour, state, determinism, contracts and regressions. The specific failure
mode this code has is **a wrong number that still passes** — not an exception — so the lens was
directed at determinism of the estimate, decimal semantics and rounding direction, boundary
conditions, interval algebra, and whether the existing assertions would survive a real defect.

Lenses NOT run, and why: `review-risk` (no security, permission, dependency or data-exposure
surface), `review-resilience` (no I/O, no external dependency, no partial-failure or recovery
path), `review-readability` (not the dominant risk; naming and structure follow an established
calculator precedent).

## Budget

One initial lens sweep. One correction transaction. One scoped fix-delta validation. No refuter
was invoked: the contract routes only *severe* inferential findings to a refuter, and the sweep
returned zero BLOCKER and zero CRITICAL rows.
