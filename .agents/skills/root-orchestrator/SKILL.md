---
name: root-orchestrator
description: Coordinate full-stack development between Host and Web projects.
---

# Root Orchestrator Skill

## 🎯 Trigger
Activate this skill when the user requests a feature that involves both:
- Database / Backend API changes (`app.trading.algoritmico.api`)
- Frontend UI / Logic changes (`app.trading.algoritmico.web`)

## 📋 Process

### 1. Analyze Requirements
Break down the user request into:
- **Data Layer**: What entities need creation or modification?
- **API Layer**: What endpoints are required?
- **UI Layer**: What pages or components need to be built?

### 2. Backend Execution (Phase 1)
Direct the agent to working in `app.trading.algoritmico.api`.
- **Check Skills**: Look for `manage-db` or `scaffold-feature` in `app.trading.algoritmico.api/.agents/skills`.
- **Action**: 
  - Create Entities/Migrations.
  - Implement Repositories/Services.
  - Expose API Endpoints.
- **Verification**: Ensure the API builds and is accessible (e.g., via Swagger).

### 3. Frontend Execution (Phase 2)
Direct the agent to working in `app.trading.algoritmico.web`.
- **Check Skills**: Look for `angular` or `data-services` in `app.trading.algoritmico.web/universal-skills`.
- **Action**:
  - Generate Data Services (proxies to the new API).
  - Create/Update Angular Features/Components.
- **Verification**: Ensure the frontend builds and connects to the backend.

### 4. Integration & Validation
- Verify end-to-end functionality.
- Ensure consistent naming conventions across the stack.


