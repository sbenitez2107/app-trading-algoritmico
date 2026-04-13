---
name: pre-commit
description: >
  Mandatory pre-commit checklist. Executes before every git commit:
  dead code cleanup, unit tests, and documentation updates
  (CHANGELOG.md, AGENTS.md, SKILL.md files, README.md).
  Trigger: Always, before running git-commit skill.
metadata:
  author: code-assistant
  version: "1.2"
---

# Pre-Commit Checklist

This skill is **MANDATORY** and must be executed before every `git commit`, regardless of the size of the change.

---

## Execution Order

Run the following steps IN ORDER. Do not skip any step. Do not commit until all steps pass.

---

## Step 1 — Dead Code Cleanup

Scan all files staged for commit. Remove:

- **Unused imports**: any `import` statement whose symbol is not used in the file.
- **Unreachable code**: code after a `return`/`throw` that can never execute.
- **Commented-out code blocks**: blocks of code left as comments (not explanatory comments).
- **Unused variables or signals**: declared but never read.
- **TODO/FIXME left behind**: comments that were meant to be temporary.

**Scope**: Only files in the current diff (`git diff --cached --name-only`). Do not touch unrelated files.

**Rule**: If dead code is found, clean it and re-stage the file before continuing.

---

## Step 2 — Run Unit Tests

Run the test suite for the project(s) affected by the staged changes.

### Frontend (Angular) — if `.ts`, `.html`, `.scss` files are staged:
```bash
cd app.trading.algoritmico.web
pnpm test --run
```

### Backend (.NET) — if `.cs` files are staged:
```bash
cd app.trading.algoritmico.api
dotnet test
```

**Rule**: If any test fails, the commit is **BLOCKED**. Fix the failing tests before proceeding.

**Rule**: Report the final result as `✅ X passed` or `❌ X failed — commit blocked`.

---

## Step 3 — Update CHANGELOG.md

The root `CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) with Semantic Versioning.

### When to update:
- **Always** — every commit that changes behavior, adds a feature, fixes a bug, or removes something must have a CHANGELOG entry.
- Do **not** add an entry for: pure formatting, whitespace-only changes, or dead code removal with no behavioral change.

### How to update:
1. Determine the version bump (current project version: **0.4.1**):
   - `feat`: minor bump (e.g., 0.4.1 → 0.5.0)
   - `fix`, `refactor`, `chore`: patch bump (e.g., 0.4.1 → 0.4.2)
   - Breaking change: major bump
2. Add a new versioned section if the version changed, or add to `[Unreleased]` if accumulating before a release.
3. Use the correct subsection: `Added`, `Changed`, `Fixed`, `Removed`, `Security`.

### Format:
```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- **Feature name**: One-line description of what was added and why it matters.

### Fixed
- **Bug name**: What was broken and how it was fixed.
```

---

## Step 4 — Update AGENTS.md (if applicable)

Update `AGENTS.md` (root, API, or Web) when:
- A new skill was created → add it to the Skills table with its trigger and path.
- A skill was deleted or renamed → remove or update its entry.
- A new shared capability or infrastructure command was added.
- The orchestration protocol changed.

**Rule**: Never leave a skill file that is not registered in `AGENTS.md`.

---

## Step 5 — Update Affected SKILL.md Files (if applicable)

If the commit modifies behavior that is governed by an existing skill:
- Open the relevant `SKILL.md` and update: patterns, commands, examples, or version metadata.
- Update the `version` field in the frontmatter (e.g., `"1.0"` → `"1.1"`).

**Examples of when this applies**:
- Added a new Angular pattern → update `skills/angular/SKILL.md`
- Changed how commits are formatted → update `universal-skills/git-commit/SKILL.md`
- Modified DB migration process → update `app.trading.algoritmico.api/.agents/skills/manage-db/SKILL.md`

---

## Step 6 — Update README.md (if applicable)

Update the root `README.md` when:
- A user-facing feature was added (new section or command).
- The setup or run instructions changed.
- A new dependency was introduced that affects onboarding.

Do **not** update README for internal refactors, test changes, or documentation-only commits.

---

## Step 7 — Sync Engram Memory (MANDATORY)

After all previous steps pass, save a memory entry to Engram capturing what this commit introduced.

Call `mem_save` with:
- **title**: Short and searchable — same spirit as the commit subject line.
- **type**: `decision` | `pattern` | `bugfix` | `architecture` | `discovery` | `config`
- **project**: `app-trading-algoritmico`
- **topic_key**: Use a stable key for evolving topics (e.g., `workflow/pre-commit`, `feature/sqx-pipeline`, `architecture/batch-service`). Reuse the same key on subsequent commits to the same topic — this upserts instead of duplicating.
- **content** (structured):
  - **What**: One sentence — what this commit introduced or changed.
  - **Why**: What motivated it (user request, bug, performance issue, convention).
  - **Where**: Files or paths most affected.
  - **Learned**: Gotchas, edge cases, non-obvious decisions — omit if none.

### When to save vs. skip:
- **Save**: new feature, bug fix, architectural decision, new pattern, config change, non-obvious discovery.
- **Skip**: pure formatting, whitespace, dead code removal with no behavior change, typo fixes in docs.

### Example:
```
title: "AG Grid batch list in /sqx/workflow"
type: "pattern"
project: "app-trading-algoritmico"
topic_key: "feature/sqx-batch-grid"
content:
  What: Added AG Grid Community v35 batch list below asset cards with autoHeight, pagination 10/20/30/50, colored uppercase header.
  Why: Replace custom hand-rolled table with sortable, filterable, paginated grid.
  Where: asset-overview.component.ts/.html/.scss, app.config.ts
  Learned: AG Grid 35 requires ModuleRegistry.registerModules([AllCommunityModule]) — without it, error #272 and blank grid.
```

**Rule**: If `mem_save` fails, log the error and proceed with commit anyway — memory sync is not a blocker.

---

## Completion Gate

Before handing off to `git-commit` skill, confirm:

| Step | Status |
|------|--------|
| Dead code removed | ✅ / ❌ |
| Tests pass | ✅ / ❌ |
| CHANGELOG.md updated | ✅ / N/A |
| AGENTS.md updated | ✅ / N/A |
| SKILL.md files updated | ✅ / N/A |
| README.md updated | ✅ / N/A |
| Engram memory synced | ✅ / N/A |

If any mandatory step (1–6) shows ❌, **do not commit**. Step 7 failure is non-blocking.

---

## Anti-Patterns

- ❌ Skipping tests because "it's a small change" — size does not matter, behavior does.
- ❌ Adding a CHANGELOG entry without a version bump when a real feature landed.
- ❌ Creating a new skill without registering it in `AGENTS.md`.
- ❌ Updating a skill's behavior without bumping its version in frontmatter.
- ❌ Committing commented-out code "just in case" — use git history for that.
- ❌ Skipping Engram sync on significant changes — the next session starts blind without it.
