# Git Conventions -- Commits & Pull Requests

> **NOTE**: The `/commit` skill now handles the commit execution workflow. This convention file is for **reference patterns only** (conventional commits format, PR structure, branch naming). Do NOT include commit execution steps here -- those live in `.claude/skills/commit/SKILL.md`.

---

## Conventional Commit Format

### Imperative Mood & Length

- **ALWAYS** use present imperative verbs: `add`, `fix`, `refactor`, `update`
- **NEVER** use past tense: ~~`added`~~, ~~`fixed`~~, ~~`updated`~~
- **MAX 60 characters** for the subject line

```
feat(auth): add 2FA verification flow
fix(i18n): resolve translation JSON files not loading
refactor(sidebar): migrate to signal-based state
```

### Commit Types

| Impact Type | Commit Type |
|-------------|-------------|
| New functionality | `feat` |
| Bug resolution | `fix` |
| Code restructure (no behavior change) | `refactor` |
| Maintenance, deps, config | `chore` |
| Documentation only | `docs` |
| Tests only | `test` |

### Decision Tree

```
Changes add new capability?     -> feat
Changes fix broken behavior?    -> fix
Changes restructure code only?  -> refactor
Changes affect only docs?       -> docs
Changes affect only tests?      -> test
Changes are maintenance/config? -> chore
```

---

## Scope Inference (from Branch Name)

The `<scope>` MUST be derived from the current branch name:

| Branch Name | Inferred Scope |
|-------------|----------------|
| `feature/catalog-filters` | `catalog` |
| `fix/auth-token-refresh` | `auth` |
| `JIRA-123-user-profile` | `user` |
| `main` / `develop` | Use primary affected module |

---

## Commit Body (Diff-Based)

The commit body MUST be a technical bullet list generated from `git diff --cached --stat`:

```
feat(catalog): add product filtering

- Create FilterService with reactive state
- Add filter-panel.component with form controls
- Update catalog.component to consume filters
```

### Output Format

```text
<type>(<scope>): <imperative_description>

- <technical_change_1>
- <technical_change_2>
- <technical_change_3>
```

---

## Commit Anti-Patterns (Guardrails)

### Prohibited: Past Tense
```
"fixed the login bug"    -> WRONG
"fix login validation error" -> CORRECT
```

### Prohibited: Monster Commits
If `git diff --cached --stat` shows unrelated modules:
**STOP** and suggest splitting into multiple commits.

### Prohibited: Vague Messages
Reject these immediately:
- `fix bug`
- `updates`
- `minor changes`
- `WIP`
- `stuff`

### Prohibited: Missing Context
- Not running `git branch --show-current`
- Not running `git diff --cached --stat`

---

## Commit Examples

### Feature Commit
```bash
# Branch: feature/auth-integration
# Diff shows: auth.service.ts, auth.interceptor.ts, login.component.ts

git commit -m "feat(auth): add login and 2FA verification

- Create AuthService with login and login2fa methods
- Add HTTP interceptor for auth token headers
- Integrate reactive form validation in login component"
```

### Bug Fix Commit
```bash
# Branch: fix/translation-loading
# Diff shows: app.config.ts, angular.json

git commit -m "fix(i18n): resolve translation JSON files not loading

- Configure TranslateHttpLoader with correct provider pattern
- Add src/assets to angular.json build assets"
```

### Refactor Commit
```bash
# Branch: refactor/sidebar-signals
# Diff shows: sidebar.component.ts

git commit -m "refactor(sidebar): migrate to signal-based state

- Replace BehaviorSubject with Angular signals
- Remove unnecessary async pipe subscriptions"
```

---

## Pull Request Conventions

### PR Title

The PR title **MUST** follow the commit format:

```
<type>(<scope>): <imperative_description>
```

**Derive from:**
- Last commit message, OR
- Branch name (e.g., `feature/auth-login` -> `feat(auth): add login functionality`)

### Structured Description (The "Why")

Every PR **MUST** include these sections:

```markdown
## Context
<!-- What business problem does this solve? Reference tickets if applicable -->
Resolves #123

## Changes
<!-- Technical summary - can be derived from commit bodies -->
- Create AuthService with login/logout methods
- Add HTTP interceptor for token management
- Integrate reactive forms in login component

## Testing Steps
<!-- Exact steps for reviewer to validate -->
1. Run `pnpm start`
2. Navigate to `/login`
3. Enter valid credentials
4. Verify redirect to dashboard

## Screenshots (if UI changes)
<!-- Attach before/after if applicable -->

## Checklist
- [ ] Code follows project conventions
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] Self-reviewed the diff
```

### PR Decision Tree

```
Is work complete and tested?
+-- Yes -> Suggest PR creation
+-- No  -> Mark as [WIP] in title

Does title follow conventional focus?
+-- Yes -> Use directly
+-- No  -> Rewrite using <type>(<scope>) format

Are there multiple commits?
+-- Yes -> Summary in Body must cover all changes
+-- No  -> Body can match commit body
```

### Bitbucket URL Generation

Generate a clickable URL for the user to open:

```bash
# Template for Bitbucket PR URL
https://bitbucket.org/<workspace>/<repo>/pull-requests/new?source=<branch>&t=<title>
```

---

## PR Anti-Patterns (Guardrails)

### Prohibited: Empty Description
```
PR with only title and no body              -> WRONG
PR with Context, Changes, and Testing Steps -> CORRECT
```

### Prohibited: Missing Test Steps
```
"Please review"                             -> WRONG
"1. Run app  2. Navigate to X  3. Verify Y" -> CORRECT
```

### Prohibited: Broken Links
ALWAYS verify branch name in the generated URL matches `git branch --show-current`.

---

## PR Example

```text
**Title:** feat(auth): add login and 2FA verification

**Body:**
## Context
Implements user authentication. Resolves #45

## Changes
- Create AuthService with login and login2fa methods
- Add HTTP interceptor for auth token headers
- Integrate reactive form validation in login component

## Testing Steps
1. Run `pnpm start`
2. Navigate to `/login`
3. Enter valid credentials and verify redirect

**Create PR:**
https://bitbucket.org/imox-team/app-trading-algoritmico/pull-requests/new?source=feature/auth-integration&t=feat(auth)%3A%20add%20login%20and%202FA%20verification
```

---

## Commands Reference

```bash
# Branch and diff (commit context)
git branch --show-current
git diff --cached --stat
git add .
git commit -m "message"

# Push and PR context
git status
git log origin/$(git branch --show-current)..HEAD
git log -1 --pretty=format:"%s%n%n%b"
```
