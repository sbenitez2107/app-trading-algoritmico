# Trading API (.NET 10) — Local Skills

## 🧠 ORCHESTRATION PROTOCOL
You are working in the **Backend (.NET 10)** project of `app-trading-algoritmico`. Follow this process:

1. **ANALYZE**: Read the user's request
2. **CHECK GLOBAL**: First see `../AGENTS.md` for global skills (Universal & Shared)
3. **TRIGGER**: If request matches a local skill, read it before coding
4. **EXECUTE**: Apply the Critical Patterns

---

## 📂 LOCAL SKILLS (.NET Specific)

| Skill | Trigger | Path |
|-------|---------|------|
| **auditing** | Implementing audit logs, change tracking | `skills/auditing/SKILL.md` |
| **clean-architecture** | Creating/modifying Domain, Application, Infrastructure, or WebAPI layers | `skills/clean-architecture/SKILL.md` |
| **csharp-dotnet** | Writing or refactoring any C# code | `skills/csharp-dotnet/SKILL.md` |
| **dotnet-automation** | Building, testing, or self-healing .NET code | `skills/dotnet-automation/SKILL.md` |
| **entity-framework** | Database, migrations, entity configuration | `skills/entity-framework/SKILL.md` |
| **external-integrations** | Integrating broker APIs, market data providers | `skills/external-integrations/SKILL.md` |
| **security** | Authentication (JWT), authorization, CORS, secrets | `skills/security/SKILL.md` |
| **testing** | Writing unit or integration tests | `skills/testing/SKILL.md` |
| **webapi-patterns** | Creating REST controllers, middleware, GraphQL resolvers | `skills/webapi-patterns/SKILL.md` |

---

## 📂 INHERITED GLOBAL SKILLS
See `../AGENTS.md` for:
- **Universal Standards**: git-commit, git-pr, documentation-standard, testing-standards
- **Shared Capabilities**: root-orchestrator, analyst-requeriment, architecture-documenter, job-orchestrator, perform-testing

---

## 🏗️ PROJECT STRUCTURE

```
src/
├── AppTradingAlgoritmico.Domain/          # Entities, Value Objects, Enums
├── AppTradingAlgoritmico.Application/     # Interfaces, DTOs, Services, Commands
├── AppTradingAlgoritmico.Infrastructure/  # EF Core, Repositories, External Services
└── AppTradingAlgoritmico.WebAPI/          # Controllers, Middleware, Program.cs
```

---

## ⚡ INSTRUCTIONS
- **Do not guess**. If a skill seems relevant, read it.
- **Strict Adherence**: Rules in skill files are non-negotiable laws.
- **Fallback**: If no skill matches, follow .NET 10 / Clean Architecture best practices.
- **Self-Healing**: When a build or test fails, attempt to fix it before reporting to user (max 2 attempts).

---

## 🚫 NON-NEGOTIABLE GIT RULES
- **NEVER** run `git commit` unless the user explicitly asks to commit.
- **NEVER** run `git push` unless the user explicitly asks to push.
- **NEVER** run `git merge`, `git rebase`, or any destructive git operation unless explicitly requested.
- The user owns the git workflow. The agent owns the code.

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
- **SOFT BLOCK**: Even for clearly development connections (e.g., `TradingAlgoritmicoLocal`), always confirm with the user before proceeding, explicitly naming the target connection.

> Example prompt before executing: *"I'm about to run migrations against `TradingAlgoritmicoLocal` (development). Do I have your authorization to proceed?"*
