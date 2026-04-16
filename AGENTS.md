# App Trading Algoritmico — Agent Configuration

This file provides supplementary routing for agents. The primary project configuration is in `CLAUDE.md`.

---

## Slash Commands (`.claude/skills/`)

These are native Claude Code skills invocable with `/name`:

| Command | Description |
|---------|-------------|
| `/commit` | Full commit workflow: version bump, tests, formatting, docs, Engram |
| `/test` | Run unit, integration, and E2E tests |
| `/scaffold` | Generate vertical feature (Clean Architecture) |
| `/manage-db` | Database migrations (create, apply, revert) |
| `/generate-hangfire` | Background job setup with Hangfire |
| `/refit-client` | External API integration with Refit + Polly |
| `/analyze-req` | Analyze ticket URLs and requirements |
| `/check-status` | Health check: is Host/Web running? |
| `/compare-branches` | Branch comparison and conflict detection |
| `/doc-feature` | Document a feature (full-stack breakdown) |
| `/doc-architecture` | ADRs, system blueprints, architecture docs |
| `/visual-qa` | Visual stress testing and layout validation |

## Conventions (`.claude/conventions/`)

Reference patterns loaded on demand. See `.claude/conventions/_index.md` for the trigger table.

## Domain Knowledge (`.agents/knowledge/`)

| Knowledge Base | Path |
|---------------|------|
| IMOX Trading Academy | `.agents/knowledge/imox/INDEX.md` |

## Workflows (`.agents/workflows/`)

| Command | Description |
|---------|-------------|
| `@[/run-all]` | Start both projects (Host + Web) |
| `@[/stop-all]` | Stop all running services |
| `@[/run-host]` | Start Host project (.NET 10) |
| `@[/run-web]` | Start Web project (Angular 21) |
| `@[/stop-host]` | Stop Host project |
| `@[/stop-web]` | Stop Web project |
| `@[/restart-host]` | Stop and restart Host |

---

## Non-Negotiable Rules

### Database Connections
- NEVER execute CLI commands that connect to a database without user authorization.
- ALWAYS state which connection/environment will be targeted.
- Production or ambiguous connections require MANDATORY user approval.

### Git
- NEVER `git commit`, `git push`, `git merge`, `git rebase` unless explicitly asked.
- Use `/commit` for all commits.
- The user owns the git workflow. The agent owns the code.

### Documentation Lookup (Context7 MCP)
When uncertain about API signatures, patterns, or version-specific features, use Context7 MCP:
1. `resolve-library-id` with library name
2. `query-docs` with ID and question
3. Apply result

Only when genuinely uncertain — not for well-known stable APIs.

### Linguistic Standards
- All code, comments, and documentation in English.
- Agents translate Spanish input to English for documentation.
