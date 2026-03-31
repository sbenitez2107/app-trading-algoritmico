---
name: git-pr
description: >
  Protocol for automated Pull Request (PR) creation with business justification, 
  testing steps, and branch alignment.
  Trigger: After successful push, finishing a feature, or via /pr command.
license: Apache-2.0
metadata:
  author: code-assistant
  version: "1.0"
  default_base: "main"
---

## When to Use

Use this skill when:
- Creating a Pull Request after completing a feature/fix
- User runs `/pr` command
- After pushing commits and ready for review

---

## Critical Patterns

### Pattern 1: Semantic Title

The PR title **MUST** follow the commit format:

```
<type>(<scope>): <imperative_description>
```

**Derive from:**
- Last commit message, OR
- Branch name (e.g., `feature/auth-login` → `feat(auth): add login functionality`)

### Pattern 2: Structured Description (The "Why")

Every PR **MUST** include these sections:

```markdown
## 📋 Context
<!-- What business problem does this solve? Reference tickets if applicable -->
Resolves #123

## 🔧 Changes
<!-- Technical summary - can be derived from commit bodies -->
- Create AuthService with login/logout methods
- Add HTTP interceptor for token management
- Integrate reactive forms in login component

## 🧪 Testing Steps
<!-- Exact steps for reviewer to validate -->
1. Run `pnpm start`
2. Navigate to `/login`
3. Enter valid credentials
4. Verify redirect to dashboard

## 📸 Screenshots (if UI changes)
<!-- Attach before/after if applicable -->

## ✅ Checklist
- [ ] Code follows project conventions
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] Self-reviewed the diff
```

### Pattern 3: Bitbucket URL Generation

Instead of CLI automation, generate a clickable URL for the user to open:

```bash
# Template for Bitbucket PR URL
https://bitbucket.org/<workspace>/<repo>/pull-requests/new?source=<branch>&t=<title>
```

**PowerShell Command to Generate Link:**

```powershell
$branch = git branch --show-current
$title = "feat(auth): login implementation" # Replace with actual title
$repo = "imox-team/app-trading-algoritmico" # derived from remote

Write-Host "🔗 Create PR Link:"
Write-Host "https://bitbucket.org/$repo/pull-requests/new?source=$branch&t=$title"
```

---

## Agent Reasoning Protocol

Execute these steps IN ORDER:

### Step 1: Verify Push Status
```bash
git status
git log origin/$(git branch --show-current)..HEAD
```
→ Ensure local commits are pushed to remote

### Step 2: Extract Context
```bash
# Get branch name for scope
git branch --show-current

# Get last commit for title/summary
git log -1 --pretty=format:"%s%n%n%b"
```

### Step 3: Prepare PR Content
1.  **Title**: Derive from last commit (Conventional Commit format).
2.  **Body**: Draft the PR description using the **Structured Description (Pattern 2)**.
3.  **Link**: Construct the Bitbucket URL.

### Step 4: Output to User
Provide the **Title**, **Body Markdown**, and the **Clickable Link**.

---

## Decision Tree

```
Is work complete and tested?
├─ Yes → Suggest PR creation
└─ No  → Mark as [WIP] in title

Does title follow conventional focus?
├─ Yes → Use directly
└─ No  → Rewrite using <type>(<scope>) format

Are there multiple commits?
├─ Yes → Summary in Body must cover all changes
└─ No  → Body can match commit body
```

---

## Anti-Patterns (Guardrails)

### ❌ Prohibited: Empty Description
```
❌ PR with only title and no body
✅ PR with Context, Changes, and Testing Steps
```

### ❌ Prohibited: Missing Test Steps
```
❌ "Please review"
✅ "1. Run app  2. Navigate to X  3. Verify Y"
```

### ❌ Prohibited: Broken Links
→ ALWAYS verify branch name in the generated URL matches `git branch --show-current`

---

## Output Format

### PR Proposal

**Title:** `<type>(<scope>): <imperative_description>`

**Body:**
```markdown
## 📋 Context
...
```

**🔗 Create PR:**
[Click here to create PR on Bitbucket](https://bitbucket.org/...)

---

## Examples

### Example 1: Feature PR

```text
**Title:** feat(auth): add login and 2FA verification

**Body:**
## 📋 Context
Implements user authentication. Resolves #45

## 🔧 Changes
...

**🔗 Create PR:**
https://bitbucket.org/imox-team/app-trading-algoritmico/pull-requests/new?source=feature/auth-integration&t=feat(auth)%3A%20add%20login%20and%202FA%20verification
```

---

## Commands Reference

```bash
# Verify push status
git status

# Get context
git branch --show-current
git log -1 --pretty=format:"%s"

# Generate Link (Mental Model)
# https://bitbucket.org/imox-team/app-trading-algoritmico/pull-requests/new?source={branch}&t={title}
```


