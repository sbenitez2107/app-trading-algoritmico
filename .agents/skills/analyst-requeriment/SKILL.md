---
name: analyst-requeriment
description: Analyzes a ticket URL, interprets requirements within the project context, and proposes an implementation and test plan for user approval.
---

# 🕵️ Skill: Analyst Requirement

This skill acts as a Technical Business Analyst. It bridges the gap between external requirements (Tickets/Jira/Docs) and the specific technical implementation in the app-trading-algoritmico architecture.

## 📋 Usage
Trigger this skill when the user runs the command `analyst-requeriment` or says:
- "Analiza este ticket [URL]"
- "Revisa este requerimiento"
- "Genera el plan de pruebas para este ticket"
- "Planifica este cambio"

## ⚙️ Process

### 1. 📥 Application input
- **Ask for URL**: If the user didn't provide a URL, ask for it.
- **Read Content**: 
  - Try `read_url_content` first for public/static pages.
  - If that fails or requires login, use `browser_subagent` to read the page content.
- **Get Context**: 
  - Read `PROJECT_STRUCTURE.md`.
  - Check `documentation/functionality` for related features.

### 2. 🧠 Analysis & Strategy
- **Interpret**: Map the functional requirements to technical components.
- **Gap Analysis**: Identify what is missing in the current codebase (`Host` vs `Web`).
- **Dependencies**: Does this require new database tables? New generic skills?

### 3. 📝 Proposal Generation (The "Plan")
You MUST generate a markdown report and save it in the `Analys_Report` folder at the project root.
- **Filename Pattern**: `REQ-[TicketID]-[BriefName].md`.
- **Content**: The report must include:

#### A. Executive Summary
Briefly explain what you understood the task to be.

#### B. Technical Implementation Plan
- **Backend (`app.trading.algoritmico.api`)**:
  - Entities/DTOs needed.
  - API Endpoints to create/modify.
  - Background Jobs (Hangfire) if applicable.
- **Frontend (`app.trading.algoritmico.web`)**:
  - Components/Pages.
  - Services/Stores.
  - UX/UI considerations.

#### C. 🧪 Test Plan (PlanTest)
Define how we will verify this works.
1.  **Automated Tests**:
    - Unit Tests for Business Logic.
    - Integration Tests for API.
2.  **User Acceptance Tests (UAT)**:
    - Step-by-step instructions for the **Browser Agent** to verify the feature (e.g., "Login -> Click Button X -> Verify Y").

### 4. ✋ Confirmation
**STOP**. Do not write any code yet.
Ask the user: *"¿Apruebas este plan de implementación y pruebas?"*

### 5. 🚀 Execution (Post-Confirmation)
**Only after** the user says "Si" or "Proceed":
- Execute the plan using `root-orchestrator`, `scaffold-feature`, `manage-db`, etc.
- Finally, execute the **Test Plan** using the `perform-testing` skill.


