# App Trading Algoritmico — Project Instructions

## Architecture

Monorepo with two projects:
- **app.trading.algoritmico.api** — .NET 10 backend (Clean Architecture, CQRS)
- **app.trading.algoritmico.web** — Angular 21 frontend (Signals, Standalone Components)

Single-user personal trading platform. No multitenancy. Dark-first UI.

## Conventions

Before writing code, read the relevant convention file from `.claude/conventions/`.
Start with the index to find what applies: `.claude/conventions/_index.md`

## Domain Knowledge

Trading domain knowledge (IMOX Academy — SQX pipeline, strategy selection, KPIs):
`.agents/knowledge/imox/INDEX.md`

Read ONLY when the task involves trading concepts, pipeline stages, or strategy evaluation.

## Non-Negotiable Rules

- **Git**: NEVER `git commit`, `git push`, `git merge`, or `git rebase` unless the user explicitly asks. Use `/commit` skill for all commits.
- **Database**: NEVER execute CLI commands that connect to a database without user authorization. Always state which connection/environment will be targeted.
- **Language**: All code, comments, and documentation in English. Rioplatense Spanish for conversation.
- **No AI attribution**: Never add Co-Authored-By or AI attribution to commits.
- **Conventions first**: Read the relevant convention file BEFORE writing code. Do not invent patterns — use what's documented.

## Tech Stack Quick Reference

| Layer | Technology |
|-------|-----------|
| Backend runtime | .NET 10 |
| Backend language | C# 12+ |
| ORM | Entity Framework Core 10 |
| Auth | JWT + ASP.NET Core Identity |
| API style | REST (commands) + GraphQL/HotChocolate (queries) |
| Frontend framework | Angular 21 |
| Frontend state | Signals + computed() |
| Frontend styling | SCSS + CSS Variables + BEM |
| i18n | ngx-translate (EN + ES) |
| Package manager | pnpm |
| Database | SQL Server |
| Testing (backend) | xUnit + FluentAssertions + Moq |
| Testing (frontend) | Vitest |

## Project Structure

```
app-trading-algoritmico/
├── app.trading.algoritmico.api/     # .NET 10 backend
│   ├── src/
│   │   ├── AppTradingAlgoritmico.Domain/
│   │   ├── AppTradingAlgoritmico.Application/
│   │   ├── AppTradingAlgoritmico.Infrastructure/
│   │   └── AppTradingAlgoritmico.WebAPI/
│   └── tests/
├── app.trading.algoritmico.web/     # Angular 21 frontend
│   └── src/
├── .claude/
│   ├── skills/                      # Slash commands (/commit, /scaffold, etc.)
│   └── conventions/                 # Reference patterns (backend, frontend, universal)
└── .agents/
    └── knowledge/imox/              # Trading domain knowledge base
```
