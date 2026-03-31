---
name: git-commit
description: >
  Best practices for creating meaningful git commits with conventional commit format.
  Trigger: When making git commits (manual activation or /commit command).
license: Apache-2.0
metadata:
  author: code-assistant
  version: "2.0"
---

## When to Use

Use this skill when:
- Making a git commit after completing a task or feature
- User asks to commit changes
- User uses /commit slash command

---

## Critical Patterns

The following patterns are **MANDATORY** for all commits.

### Pattern 1: Imperative Mood & Length

- **ALWAYS** use present imperative verbs: `add`, `fix`, `refactor`, `update`
- **NEVER** use past tense: ~~`added`~~, ~~`fixed`~~, ~~`updated`~~
- **MAX 60 characters** for the subject line

```
✅ feat(auth): add 2FA verification flow
❌ feat(auth): added 2FA verification flow
❌ feat(auth): this commit adds the two factor authentication verification flow to the login
```

### Pattern 2: Automatic Scope Inference

The `<scope>` MUST be derived from the current branch name:

| Branch Name | Inferred Scope |
|-------------|----------------|
| `feature/catalog-filters` | `catalog` |
| `fix/auth-token-refresh` | `auth` |
| `JIRA-123-user-profile` | `user` |
| `main` / `develop` | Use primary affected module |

### Pattern 3: Diff-Based Body

The commit body MUST be a technical bullet list generated from `git diff --cached --stat`:

```
feat(catalog): add product filtering

- Create FilterService with reactive state
- Add filter-panel.component with form controls
- Update catalog.component to consume filters
```

---

## Agent Reasoning Protocol

Execute these steps IN ORDER before creating a commit:

### Step 1: Identify Branch
```bash
git branch --show-current
```
→ Extract module/scope from branch name

### Step 2: Analyze Staging
```bash
git diff --cached --stat
```
→ See exactly what files are being committed

### Step 3: Categorize Impact

| Impact Type | Commit Type |
|-------------|-------------|
| New functionality | `feat` |
| Bug resolution | `fix` |
| Code restructure (no behavior change) | `refactor` |
| Maintenance, deps, config | `chore` |
| Documentation only | `docs` |
| Tests only | `test` |

### Step 4: Technical Summary
Write 2-3 bullet points explaining **technical impact**:
- What was created/modified
- What problem was solved
- What patterns were applied

---

## Decision Tree

```
Changes add new capability?     → feat
Changes fix broken behavior?    → fix
Changes restructure code only?  → refactor
Changes affect only docs?       → docs
Changes affect only tests?      → test
Changes are maintenance/config? → chore
```

---

## Anti-Patterns (Guardrails)

### ❌ Prohibited: Past Tense
```
❌ "fixed the login bug"
✅ "fix login validation error"
```

### ❌ Prohibited: Monster Commits
If `git diff --cached --stat` shows unrelated modules:
→ **STOP** and suggest splitting into multiple commits

### ❌ Prohibited: Vague Messages
Reject these immediately:
- `fix bug`
- `updates`
- `minor changes`
- `WIP`
- `stuff`

### ❌ Prohibited: Missing Context
- Not running `git branch --show-current`
- Not running `git diff --cached --stat`

---

## Output Format

```text
<type>(<scope>): <imperative_description>

- <technical_change_1>
- <technical_change_2>
- <technical_change_3>
```

---

## Examples

### Example 1: Feature Commit

```bash
# Branch: feature/auth-integration
# Diff shows: auth.service.ts, auth.interceptor.ts, login.component.ts

git commit -m "feat(auth): add login and 2FA verification

- Create AuthService with login and login2fa methods
- Add HTTP interceptor for auth token headers
- Integrate reactive form validation in login component"
```

### Example 2: Bug Fix Commit

```bash
# Branch: fix/translation-loading
# Diff shows: app.config.ts, angular.json

git commit -m "fix(i18n): resolve translation JSON files not loading

- Configure TranslateHttpLoader with correct provider pattern
- Add src/assets to angular.json build assets"
```

### Example 3: Refactor Commit

```bash
# Branch: refactor/sidebar-signals
# Diff shows: sidebar.component.ts

git commit -m "refactor(sidebar): migrate to signal-based state

- Replace BehaviorSubject with Angular signals
- Remove unnecessary async pipe subscriptions"
```

---

## Commands

```bash
git branch --show-current  # Get branch for scope inference
git diff --cached --stat   # Analyze staged changes
git add .                  # Stage all changes
git commit -m "message"    # Commit with inline message
git commit                 # Open editor for multi-line message
```
