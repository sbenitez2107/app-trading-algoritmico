---
name: agent-orchestration
description: >
  Formalizes the coordination between specialized agents (roles) for end-to-end feature implementation.
  Trigger: When the user requests a complex feature that spans multiple projects (Host and Web) or requires multi-step planning and delegation.
license: Apache-2.0
metadata:
  version: "1.0"
---

## When to Use

Use this skill when:
- A feature requires changes in both Backend (.NET) and Frontend (Angular).
- You need to coordinate between different "Specialized Agents" (e.g., Architect, Backend Dev, Frontend Dev, QA).
- The user asks for an "Agent Manager" or organizational structure for agents.

---

## Agent Roles (Contextual Roles)

| Role | Responsibility | Primary Skill(s) | Required Workflow |
|------|----------------|------------------|-------------------|
| **Agent Manager (Orchestrator)** | High-level planning, delegation, and final verification. | `agent-orchestration` | - |
| **Backend Developer** | Domain logic, API, Database, Security. | `clean-architecture`, `csharp-dotnet` | `check-health-host.yaml` |
| **Frontend Developer** | UI/UX, Component logic, State management. | `angular`, `design-core` | `check-health-web.yaml` |
| **QA / Tester** | Automated testing, verification, self-healing. | `testing-standards`, `automation` | - |

---

## Coordination Protocol

### 1. Planning (Orchestrator Phase)
The Orchestrator MUST create an `implementation_plan.md` that breaks down the feature into:
- Backend requirements.
- Frontend requirements.
- Infrastructure/DevOps requirements.

### 2. Execution (Worker Phase)
The Orchestrator "switches context" to the specialized role:
- **Backend Execution**:
  1. Load Host skills and implement.
  2. **Execute `@[check-health-host.yaml]`** to verify health before finishing.
- **Frontend Execution**:
  1. Load Web skills and implement.
  2. **Execute `@[check-health-web.yaml]`** to verify health before finishing.

### 3. Synchronization (Handover)
- API contracts must be defined in the implementation plan before coding.
- Shared models or DTOs should be prioritized.

### 4. Verification (QA Phase)
The Orchestrator verifies both components together using `walkthrough.md`.

---

## Critical Patterns

### Pattern 1: Multi-Step Breakdown
Never jump to implementation for cross-project features. Always create a task list first.

### Pattern 2: Contract-First
Define the communication boundary (API Endpoint, Request/Response) before implementing logic.

---

## Code Examples

### Example: Feature Delegation
```markdown
# Feature: User Profile Update
- [ ] [Orchestrator] Define API Contract
- [ ] [Backend] Implement `UpdateUserProfileCommand`
- [ ] [Frontend] Create `ProfileFormComponent`
- [ ] [QA] Verify integration
```
