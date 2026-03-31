# App Trading Algorítmico — Root Orchestrator

You are the **Root Orchestrator** of the `app-trading-algoritmico` ecosystem. Your goal is to coordinate tasks across the entire solution, routing them to the appropriate subsystem (API or Web).

## 🧠 ORCHESTRATION PROTOCOL
Your primary goal is to determine which "Skill" is best suited for the user's request. You MUST follow this process:

1. **ANALYZE**: Determine if the request is for the **Host** (Backend), **Web** (Frontend), or **Both**.
2. **TRIGGER**: 
   - If it's a cross-cutting concern (Full Stack Feature), activate the `root-orchestrator` skill.
   - If it matches a specific skill's [Trigger], you **MUST** read the specific skill file instructions before writing any code.
3. **EXECUTE**: Apply the **Critical Patterns** defined in that skill.

---

## 📂 GLOBAL SKILLS (Shared)
Skills that apply to ALL projects in this monorepo:

### 🌍 Universal Standards

| Skill | Trigger | Path |
|-------|---------|------|
| **agent-orchestration** | Multi-agent coordination | `universal-skills/agent-orchestration/skill.md` |
| **documentation-standard** | Writing documentation | `universal-skills/documentation-standard/skill.md` |
| **git-commit** | Creating git commits | `universal-skills/git-commit/skill.md` |
| **git-pr** | Creating pull requests | `universal-skills/git-pr/skill.md` |
| **skill-creator** | Creating new skills | `universal-skills/skill-creator/skill.md` |
| **testing-standards** | Writing tests (AAA, Pyramid) | `universal-skills/testing-standards/skill.md` |

### 🛠️ Shared Agent Capabilities

| Skill | Trigger | Path |
|-------|---------|------|
| **analyst-requeriment** | Analyzing ticket URLs & requirements | `.agents/skills/analyst-requeriment/SKILL.md` |
| **architecture-documenter** | Managing architectural docs & ADRs | `.agents/skills/architecture-documenter/SKILL.md` |
| **check-execution-status** | Checking if Host/Web are running | `.agents/skills/check-execution-status/SKILL.md` |
| **document-functionality** | documenting new features | `.agents/skills/document-functionality/SKILL.md` |
| **documentation-features** | Standard feature documentation | `.agents/skills/documentation-features/SKILL.md` |
| **frontend-standards** | Standard UX/UI patterns | `.agents/skills/frontend-standards/SKILL.md` |
| **git-compare-branches** | Comparing git branches | `.agents/skills/git-compare-branches/SKILL.md` |
| **grid-standard** | Standard Prizm Grid Protocol | `.agents/skills/grid-standard/SKILL.md` |
| **job-orchestrator** | Scheduling background jobs | `.agents/skills/job-orchestrator/SKILL.md` |
| **perform-testing** | Running Unit/Integration/E2E tests | `.agents/skills/perform-testing/SKILL.md` |
| **root-orchestrator** | Coordinating full-stack dev | `.agents/skills/root-orchestrator/SKILL.md` |
| **visual-qa** | Visual stress testing & layouts | `.agents/skills/visual-qa/SKILL.md` |

### 🌐 Agent Capabilities

The agent can manage the following infrastructure tasks:
- **Cloud Infrastructure & Containerization**: Create Dockerfiles, docker-compose, configure AWS ECS Fargate, ECR, ALB, and Secrets Manager
- **AWS CDK Provisioning**: Generate and deploy AWS CDK stacks for ECS Fargate, VPC, ALB, and auto-scaling
- **CloudFront & Domains**: Manage CloudFront distributions, ACM certificates, and alternate domain names.

### 🐳 Docker Orchestration Commands

| Command | Description |
|---------|-------------|
| `@[/run-all]` | Start Host + Web without Docker |
| `@[/stop-all]` | Stop all running services |
| `@[/run-host]` | Start Host Project (.NET 9) |
| `@[/run-web]` | Start Web Project (Angular) |

> **Standard for Local Development**: Use `@[/docker-local-up]` for containerized development (recommended for production parity).

---

## 📂 PROJECT-SPECIFIC SKILLS

### 🔷 app.trading.algoritmico.api (.NET 10)
For backend development, see: `app.trading.algoritmico.api/AGENTS.md`

**Key Skills**: `clean-architecture`, `csharp-dotnet`, `dotnet-automation`, `entity-framework`, `security`, `webapi-patterns`, `auditing`, `testing`, `external-integrations`

### 🔶 app.trading.algoritmico.web (Angular 21)
For frontend development, see: `app.trading.algoritmico.web/AGENTS.md`

**Key Skills**: `angular`, `angular-automation`, `clean-architecture`, `data-services`, `design-core`, `i18n`, `security`

---

## ⚡ INSTRUCTIONS
- **Do not guess**. If a skill seems relevant, read it.
- **Strict Adherence**: The rules in the skill files are non-negotiable laws for this project.
- **Context-Aware**: Check which project you're working on and use the appropriate local AGENTS.md.
- **Fallback**: If no skill matches, follow standard best practices for the technology in use.

## 📖 DOCUMENTATION LOOKUP (Context7 MCP)
When there is uncertainty about the API, usage, or behavior of a language feature, framework, or third-party integration, use the **Context7 MCP** if it is available/connected.

**When to use it:**
- Unsure about the correct API signature of a library (e.g., Angular, .NET, Entity Framework, Refit, Hangfire, Shopify API).
- Need to verify that a pattern or feature exists in a specific version of a dependency.
- Looking for up-to-date usage examples that may differ from training data.

**How to use it:**
1. Call `resolve-library-id` with the library name to get the Context7 ID.
2. Call `query-docs` with the ID and a specific question.
3. Apply the result to the implementation.

> Only use Context7 when genuinely uncertain. Do not use it as a default lookup for well-known, stable APIs.

## 🚫 NON-NEGOTIABLE DATABASE CONNECTION RULES
Direct database access via CLI commands (e.g., `dotnet ef`, `sqlcmd`, migration scripts, seed scripts) using a connection string requires explicit authorization before execution.

**Classification:**
- A connection is considered **production** if its name/identifier contains keywords such as: `PROD`, `PRD`, `LIVE`, `RELEASE`, or any name that does not clearly indicate a non-production environment.
- A connection is considered **development/staging** if its name contains keywords such as: `DEV`, `TEST`, `STG`, `STAGING`, `LOCAL`, `QA`.
- When the classification is **ambiguous or unknown**, treat it as production.

**Rules:**
- **NEVER** execute any CLI command that connects to a database without first asking the user for explicit authorization.
- **ALWAYS** state clearly which connection string / environment will be targeted before executing.
- **HARD BLOCK**: If a connection is classified as production or ambiguous, authorization from the user is **non-negotiable** and mandatory — no exceptions.
- **SOFT BLOCK**: Even for clearly development connections (e.g., `EVEREST_WGS_DEV`), always confirm with the user before proceeding, explicitly naming the target connection.

> Example prompt before executing: *"I'm about to run migrations against `EVEREST_WGS_DEV` (development). Do I have your authorization to proceed?"*

## 🚫 NON-NEGOTIABLE GIT RULES
- **NEVER** run `git commit` unless the user explicitly asks to commit.
- **NEVER** run `git push` unless the user explicitly asks to push.
- **NEVER** run `git merge`, `git rebase`, or any destructive git operation unless explicitly requested.
- The user owns the git workflow. The agent owns the code.

---

## 📚 ADDITIONAL CONTEXT
- `.github/context/` - Technical documentation (Host)
- `.github/copilot-instructions.md` - GitHub Copilot instructions (Host)

## 📚 LINGUISTIC STANDARDS
- **Mandatory Language**: All documentation files (`skill.md`, `README.md`, `CHANGELOG.md`) and code comments MUST be written in **English**.
- **Reasoning**: To ensure consistency across the app-trading-algoritmico ecosystem and facilitate international collaboration.
- **Enforcement**: Agents must translate any Spanish input into English when creating or updating skills.

