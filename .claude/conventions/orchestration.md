# Orchestration — Multi-Agent & Full-Stack Coordination

Consolidated from: `agent-orchestration` + `root-orchestrator` skills.
Applies to: cross-project features (API + Web).

---

## When to Orchestrate

Activate when a feature involves BOTH:
- Backend changes (`app.trading.algoritmico.api`)
- Frontend changes (`app.trading.algoritmico.web`)

---

## Execution Phases

### Phase 1: Planning (Orchestrator)

Break down into:
- **Data Layer**: entities, migrations
- **API Layer**: endpoints, DTOs
- **UI Layer**: components, services, routes

Define API contracts BEFORE implementation (Contract-First).

### Phase 2: Backend Execution

1. Create entities / migrations → `backend-data.md` conventions
2. Implement services / repositories → `backend-core.md` conventions
3. Expose endpoints → `backend-api.md` conventions
4. Verify: API builds, Swagger accessible

### Phase 3: Frontend Execution

1. Create data services (proxies to new API) → `frontend-data.md` conventions
2. Build components → `frontend-core.md` conventions
3. Style → `frontend-design.md` conventions
4. Verify: frontend builds, connects to backend

### Phase 4: Integration Validation

- End-to-end functionality check
- Consistent naming across stack
- Run tests for both projects

---

## Agent Roles (Contextual)

| Role | Responsibility | Key Conventions |
|------|----------------|-----------------|
| Orchestrator | Planning, delegation, verification | This file |
| Backend Dev | Domain, API, DB, Security | `backend-*.md` |
| Frontend Dev | UI/UX, Components, State | `frontend-*.md` |
| QA / Tester | Automated testing, self-healing | `*-testing.md`, `*-automation.md` |

---

## Rules

- NEVER jump to implementation for cross-project features — create task list first.
- API contracts (DTOs, endpoints) must be agreed before coding.
- Shared models prioritized early.
- Health check both projects after each phase.
