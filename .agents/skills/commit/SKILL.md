---
name: commit
description: >
  Full commit workflow: guard check, version bump, dead code + debug artifacts +
  security scan, formatting, tests, type safety, version files, CHANGELOG, docs,
  Engram sync, and git commit.
metadata:
  author: code-assistant
  version: "1.1"
---

# Commit Skill

Invoke with `/commit`. Executes every step in order. Do NOT skip any step.
Do NOT run `git commit` manually — this skill owns the commit.

---

## Step 0 — Guard: Verify Staged Changes

Run:
```bash
git diff --cached --name-only
```

If the output is **empty** → **STOP immediately**. Do not proceed.

Output to user:
> "Nothing staged. Run `git add <files>` and then `/commit` again."

Only continue if at least one file is staged.

---

## Step 1 — Read Current Version

Read the current version from:
```
app.trading.algoritmico.web/src/environments/environment.ts
```

Extract the `version` field value (e.g., `'0.4.3'`). This is the **base version** for all calculations in this session.

---

## Step 2 — Analyze Staged Changes

Run:
```bash
git diff --cached --name-only
git diff --cached --stat
```

From the staged file list and diff, determine:

1. **Commit type** — infer from the nature of the changes:
   - New feature, new component, new endpoint, new entity → `feat`
   - Bug fix, incorrect behavior corrected → `fix`
   - Refactor, docs, chore, config, agent files only → `chore`

2. **Version bump rule**:
   | Type | Bump | Example |
   |------|------|---------|
   | `feat` | **minor** | `0.4.3` → `0.5.0` |
   | `fix` | **patch** | `0.4.3` → `0.4.4` |
   | `chore` / `refactor` / `docs` | **patch** | `0.4.3` → `0.4.4` |
   | Breaking change | **major** | `0.4.3` → `1.0.0` |

3. Calculate the **new version** string (e.g., `0.5.0`).

> **Announce before proceeding**: "Versión actual: X.Y.Z → Nueva versión: A.B.C (tipo: feat/fix/chore). Continúo con el checklist."

---

## Step 3 — Dead Code, Debug Artifacts & Security Scan

Scan ONLY the staged files (`git diff --cached --name-only`).

### 3a — Dead Code
Remove:
- **Unused imports**: any `import` / `using` whose symbol is never referenced.
- **Unreachable code**: code after `return`/`throw` that cannot execute.
- **Commented-out code blocks**: blocks of code left as comments (not explanatory comments).
- **Unused variables, signals, or fields**: declared but never read.
- **TODO/FIXME leftovers**: temporary markers never resolved.

### 3b — Debug Artifacts
Scan staged `.ts` and `.cs` files for:
- `console.log(`, `console.warn(`, `console.error(` — unless inside a dedicated logging service or explicitly intentional.
- `debugger;` statements.

**Rule**: If found → flag to user and remove unless there is an explicit justification to keep.

### 3c — Security Scan
Scan all staged files for:
- Hardcoded secret patterns: `password = "`, `apiKey = "`, `token = "`, `secret = "`, `connectionString = "`, `Authorization: Bearer <literal>`.
- Hardcoded non-localhost URLs in non-environment files (e.g., `https://api.prod.com` inside a component).
- Any `TODO: remove` or `FIXME: security` markers.

**Rule**: If any secret or hardcoded credential is found → **STOP**. Flag to user. Do not commit until resolved.
**Rule**: Non-localhost URLs in non-environment files → flag to user and ask whether to move to environment config.

**Rule (general)**: Do NOT touch files outside the current diff.
**Rule**: If changes are made → re-stage the affected files before continuing.

---

## Step 4 — Code Formatting

### Frontend — if any `.ts`, `.html`, or `.scss` files are staged:

Run Prettier check on staged frontend files only:
```bash
npx prettier --check <staged .ts/.html/.scss files — space-separated>
```

If formatting issues are found:
```bash
npx prettier --write <same files>
git add <same files>
```

### Backend — if any `.cs` files are staged:

```bash
dotnet format app.trading.algoritmico.api/AppTradingAlgoritmico.sln --include <staged .cs files — space-separated>
```

If `dotnet format` made changes → re-stage the modified files.

**Rule**: Formatting must pass (or be auto-fixed) before continuing. This step is **blocking**.

---

## Step 5 — Run Tests

### Backend — run if any `.cs` files are staged:
```bash
cd app.trading.algoritmico.api
dotnet test --no-build
```

### Frontend — run if any `.ts`, `.html`, or `.scss` files are staged:
```bash
cd app.trading.algoritmico.web
npx ng test --watch=false
```

**Rule**: If any test fails → **STOP**. Do not continue. Fix the failing tests first.

Report result as:
- `✅ Backend: X passed` / `✅ Frontend: X passed`
- `❌ Backend: X failed — BLOCKED` / `❌ Frontend: X failed — BLOCKED`

---

## Step 6 — Type Safety & Static Analysis

### Frontend — if any `.ts` or `.html` files are staged:

Run TypeScript compiler check (no output files produced):
```bash
cd app.trading.algoritmico.web && npx tsc --noEmit
```

**Rule**: ANY type error → **BLOCKED**. Fix before continuing.

### Backend — if any `.cs` files are staged:

Build treating warnings as errors:
```bash
cd app.trading.algoritmico.api && dotnet build /warnaserror
```

**Rule**: ANY warning-elevated-to-error → **BLOCKED**. Fix before continuing.

> **Note**: If warnings predate this commit (not introduced by staged files), report them to the user and ask: fix now or add to backlog? Do not block the commit for pre-existing warnings unless they are in the staged files.

---

## Step 7 — Update Version Files

Update the version string in ALL of these files to the new version calculated in Step 2:

| File | Field |
|------|-------|
| `app.trading.algoritmico.web/src/environments/environment.ts` | `version: 'X.Y.Z'` |
| `app.trading.algoritmico.web/src/environments/environment.development.ts` | `version: 'X.Y.Z'` |
| `app.trading.algoritmico.web/package.json` | `"version": "X.Y.Z"` |

Stage the updated files after editing.

---

## Step 8 — Update CHANGELOG.md

File: `CHANGELOG.md` (root)

### Rules:
- `feat` → close `[Unreleased]` as a new versioned section **or** create a new versioned section directly.
- `fix` / `chore` → same: new versioned section with the new version from Step 2.
- If an `[Unreleased]` section already exists → rename it to `[X.Y.Z] - YYYY-MM-DD`.
- If no `[Unreleased]` section → insert a new `## [X.Y.Z] - YYYY-MM-DD` block above the previous latest version.
- Use today's date in `YYYY-MM-DD` format.
- Use the correct subsection: `Added`, `Changed`, `Fixed`, `Removed`, `Security`.
- Write entries in English. One line per item. Focus on user-visible impact.

### Do NOT add a CHANGELOG entry for:
- Pure whitespace or formatting changes.
- Dead code removal with no behavioral change.
- Agent/skill files only (chore with no product impact).

Stage the updated file after editing.

---

## Step 9 — Update AGENTS.md (if applicable)

Update `AGENTS.md` (root, API, or Web) when:
- A new skill was created → add it to the Skills table.
- A skill was deleted or renamed → remove or update its entry.
- A new shared capability was added.

**Rule**: Every skill file must have a corresponding entry in `AGENTS.md`.

---

## Step 10 — Update Affected SKILL.md Files (if applicable)

If the commit modifies behavior governed by an existing skill:
- Open the relevant `SKILL.md` and update patterns, commands, or examples.
- Bump the `version` field in frontmatter.

---

## Step 11 — Update README.md (if applicable)

Update root `README.md` when:
- A user-facing feature was added.
- Setup or run instructions changed.
- A new dependency affects onboarding.

Do NOT update README for internal refactors, test changes, or agent files.

---

## Step 12 — Sync Engram Memory

Call `mem_save` with:
- **title**: Short, searchable — same spirit as the commit subject.
- **type**: `decision` | `pattern` | `bugfix` | `architecture` | `discovery` | `config`
- **project**: `app-trading-algoritmico`
- **topic_key**: Stable key for the topic (reuse to upsert, e.g. `feature/sqx-pipeline`, `architecture/auth`).
- **content**:
  - **What**: One sentence — what this commit introduced or changed.
  - **Why**: What motivated it.
  - **Where**: Files or paths most affected.
  - **Learned**: Gotchas or non-obvious decisions — omit if none.

### Skip Engram sync when:
- Pure formatting or whitespace.
- Dead code removal with no behavioral change.
- Typo fixes in docs.

**Rule**: `mem_save` failure is non-blocking — log the error and continue.

---

## Step 13 — Git Commit

Stage any remaining modified files from previous steps, then commit:

```bash
git add <files updated in steps 7-11>
git commit -m "<conventional commit message>"
```

### Commit message format (Conventional Commits):
```
<type>(<scope>): <short description>

<optional body — if context is needed>
```

Types: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `style`
Scope: optional, e.g. `pipeline`, `auth`, `agents`, `changelog`

**Rules**:
- Never add `Co-Authored-By` or AI attribution.
- Keep subject line under 72 characters.
- Use imperative mood: "add", "fix", "update" — not "added", "fixed".

---

## Completion Gate

| Step | Description | Mandatory |
|------|-------------|-----------|
| 0 | Staged changes verified | ALWAYS |
| 1 | Version read | ALWAYS |
| 2 | Bump calculated & announced | ALWAYS |
| 3 | Dead code, debug artifacts, secrets | ✅ / N/A |
| 4 | Code formatting | ✅ / N/A |
| 5 | Tests pass | ✅ / N/A |
| 6 | Type safety & static analysis | ✅ / N/A |
| 7 | Version files updated | ALWAYS |
| 8 | CHANGELOG updated | ✅ / N/A |
| 9 | AGENTS.md updated | ✅ / N/A |
| 10 | SKILL.md files updated | ✅ / N/A |
| 11 | README updated | ✅ / N/A |
| 12 | Engram synced | ✅ / N/A |
| 13 | git commit executed | ALWAYS |

Steps 3–12 can be N/A based on the nature of the changes.
Steps 0, 1, 2, 7, and 13 are **ALWAYS mandatory**.

---

## Anti-Patterns

- ❌ Running with nothing staged — Step 0 catches this.
- ❌ Skipping tests because "it's a small change".
- ❌ Hardcoding the version — always read it from `environment.ts` first.
- ❌ Leaving `console.log` or `debugger` in production code.
- ❌ Committing hardcoded secrets or credentials.
- ❌ Creating a skill without registering it in `AGENTS.md`.
- ❌ Committing commented-out code "just in case".
- ❌ Running `git commit` manually — always use `/commit`.
- ❌ Adding CHANGELOG entries for agent/skill-only changes with no product impact.

---

## Note: ESLint (not yet installed)

ESLint is not configured in this project. When `@angular-eslint/schematics` is installed
and configured, add a lint step between Step 4 (formatting) and Step 5 (tests):

```bash
cd app.trading.algoritmico.web && npx ng lint
```

At that point, bump this skill version and add it to the step sequence.
