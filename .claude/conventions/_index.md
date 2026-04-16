# Conventions Index

Read this file first when you need to apply project conventions.
Then read ONLY the relevant convention file(s) — do NOT read all.

---

## Trigger Table

| Working on... | Read these conventions |
|---------------|----------------------|
| Any `.cs` file (entities, services, controllers) | `backend-core.md` |
| EF Core, migrations, DbContext, seeding | `backend-data.md` |
| REST endpoints, GraphQL, security, middleware | `backend-api.md` |
| External API integrations (Refit, Polly) | `backend-external.md` |
| Backend tests (xUnit, FluentAssertions) | `backend-testing.md` |
| Any Angular `.ts` component or service | `frontend-core.md` |
| SCSS, themes, layout, UX patterns | `frontend-design.md` |
| HTTP services, i18n, auth guards | `frontend-data.md` |
| Angular builds, tests, self-healing | `frontend-automation.md` |
| Data grids, server-side filtering | `frontend-grid.md` |
| Git commits, PR creation | `universal-git.md` |
| Writing any test | `universal-testing.md` |
| Writing docs, comments, ADRs | `universal-docs.md` |
| Cross-project (API + Web) features | `orchestration.md` |
| Trading pipeline, SQX, strategies | `.agents/knowledge/imox/INDEX.md` |

---

## File Summary

### Backend (5)
| File | Scope |
|------|-------|
| `backend-core.md` | Clean Architecture + C# standards |
| `backend-data.md` | EF Core + multitenancy |
| `backend-api.md` | REST + GraphQL + Security + Auditing |
| `backend-external.md` | Refit + Polly |
| `backend-testing.md` | xUnit + self-healing |

### Frontend (5)
| File | Scope |
|------|-------|
| `frontend-core.md` | Angular 21 + Clean Architecture |
| `frontend-design.md` | Theme + BEM + UX patterns |
| `frontend-data.md` | Services + i18n + Security |
| `frontend-automation.md` | Builds + tests + self-healing |
| `frontend-grid.md` | Prizm Grid Protocol |

### Universal (4)
| File | Scope |
|------|-------|
| `universal-git.md` | Conventional Commits + PR structure |
| `universal-testing.md` | AAA + Pyramid + FIRST |
| `universal-docs.md` | TSDoc + ADRs + Feature docs |
| `orchestration.md` | Multi-agent + full-stack coordination |
