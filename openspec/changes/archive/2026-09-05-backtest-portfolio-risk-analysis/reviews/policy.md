# Review Policy — backtest-portfolio-risk-analysis

## Risk classification: **Medium** → exactly ONE lens

The change adds a read endpoint and a UI panel but touches no authentication, no permission model,
no payments, no data-loss or data-exposure path, and no shell/process boundary. Single-user app, no
multitenancy. **Size is not a classifier input at any magnitude** — the target is roughly 5,600
changed lines across three PRs and that does not escalate the tier.

## Lens selected: `review-reliability`

Dominant risk is behaviour, determinism, contracts and regressions. The specific failure mode of
this code is **a wrong number that still passes**, not an exception — the slice exists because
reusing the shipped daily VaR unchanged on backtest data returns exactly `0.00`. The lens was
directed at determinism, decimal semantics, boundary conditions, interval algebra, and whether the
existing assertions would survive a real defect.

Lenses NOT run: `review-risk` (no security, permission, dependency or data-exposure surface),
`review-resilience` (no external dependency, no partial-failure or recovery path),
`review-readability` (not the dominant risk; the code follows an established calculator precedent).

## Budget consumed

One initial lens sweep. One correction transaction. One scoped fix-delta validation, which returned
**escalate**, followed by the correction of what it escalated on. No refuter batch: the contract
routes only *severe inferential* findings to a refuter, and the single CRITICAL was proven by
execution before correction, making it corroborated rather than inferential.
