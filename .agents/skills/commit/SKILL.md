---
name: commit
description: >
  Full commit workflow: reads current version, calculates bump, runs tests,
  detects dead code, updates version files, CHANGELOG, docs, syncs Engram,
  and executes git commit. Replaces manual pre-commit checklist.
metadata:
  author: code-assistant
  version: "1.0"
---

# Commit Skill

Invoke with `/commit`. Executes every step in order. Do NOT skip any step.
Do NOT run `git commit` manually — this skill owns the commit.

---

## Step 1 — Read Current Version

Read the current version from:
```
app.trading.algoritmico.web/src/environments/environment.ts
```

Extract the `version` field value (e.g., `'0.4.2'`). This is the **base version** for all calculations in this session.

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
   | `feat` | **minor** | `0.4.2` → `0.5.0` |
   | `fix` | **patch** | `0.4.2` → `0.4.3` |
   | `chore` / `refactor` / `docs` | **patch** | `0.4.2` → `0.4.3` |
   | Breaking change | **major** | `0.4.2` → `1.0.0` |

3. Calculate the **new version** string (e.g., `0.5.0`).

> **Announce before proceeding**: "Versión actual: X.Y.Z → Nueva versión: A.B.C (tipo: feat/fix/chore). Continúo con el checklist."

---

## Step 3 — Dead Code Cleanup

Scan ONLY the staged files (`git diff --cached --name-only`). Remove:

- **Unused imports**: any `import` / `using` whose symbol is never referenced.
- **Unreachable code**: code after `return`/`throw` that cannot execute.
- **Commented-out code blocks**: blocks left as comments (not explanatory comments).
- **Unused variables, signals, or fields**: declared but never read.
- **TODO/FIXME leftovers**: temporary markers never resolved.

**Rule**: If dead code is found → clean it and re-stage the file before continuing.
**Rule**: Do NOT touch files outside the current diff.

---

## Step 4 — Run Tests

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

## Step 5 — Update Version Files

Update the version string in ALL of these files to the new version calculated in Step 2:

| File | Field |
|------|-------|
| `app.trading.algoritmico.web/src/environments/environment.ts` | `version: 'X.Y.Z'` |
| `app.trading.algoritmico.web/src/environments/environment.development.ts` | `version: 'X.Y.Z'` |
| `app.trading.algoritmico.web/package.json` | `"version": "X.Y.Z"` |

Stage the updated files after editing.

---

## Step 6 — Update CHANGELOG.md

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

## Step 7 — Update AGENTS.md (if applicable)

Update `AGENTS.md` (root, API, or Web) when:
- A new skill was created → add it to the Skills table.
- A skill was deleted or renamed → remove or update its entry.
- A new shared capability was added.

**Rule**: Every skill file must have a corresponding entry in `AGENTS.md`.

---

## Step 8 — Update Affected SKILL.md Files (if applicable)

If the commit modifies behavior governed by an existing skill:
- Open the relevant `SKILL.md` and update patterns, commands, or examples.
- Bump the `version` field in frontmatter.

---

## Step 9 — Update README.md (if applicable)

Update root `README.md` when:
- A user-facing feature was added.
- Setup or run instructions changed.
- A new dependency affects onboarding.

Do NOT update README for internal refactors, test changes, or agent files.

---

## Step 10 — Sync Engram Memory

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

## Step 11 — Git Commit

Stage any remaining modified files from previous steps, then commit:

```bash
git add <files updated in steps 5-9>
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

| Step | Description | Status |
|------|-------------|--------|
| 1 | Version read | ✅ |
| 2 | Bump calculated | ✅ / announced |
| 3 | Dead code removed | ✅ / N/A |
| 4 | Tests pass | ✅ / N/A |
| 5 | Version files updated | ✅ |
| 6 | CHANGELOG updated | ✅ / N/A |
| 7 | AGENTS.md updated | ✅ / N/A |
| 8 | SKILL.md files updated | ✅ / N/A |
| 9 | README updated | ✅ / N/A |
| 10 | Engram synced | ✅ / N/A |
| 11 | git commit executed | ✅ |

Steps 3–10 can be N/A based on the nature of the changes. Steps 1, 2, 5, and 11 are ALWAYS mandatory.

---

## Anti-Patterns

- ❌ Skipping tests because "it's a small change".
- ❌ Hardcoding the version — always read it from `environment.ts` first.
- ❌ Creating a skill without registering it in `AGENTS.md`.
- ❌ Committing commented-out code "just in case".
- ❌ Running `git commit` manually — always use `/commit`.
- ❌ Adding CHANGELOG entries for agent/skill-only changes with no product impact.
