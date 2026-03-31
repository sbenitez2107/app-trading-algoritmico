# Trading Web (Angular 21) — Local Skills

## 🧠 ORCHESTRATION PROTOCOL
You are working in the **Frontend (Angular 21)** project of `app-trading-algoritmico`. Follow this process:

1. **ANALYZE**: Read the user's request
2. **CHECK GLOBAL**: First see `../AGENTS.md` for global skills (git-commit, git-pr, docs, deployment)
3. **TRIGGER**: If request matches a local skill, read it before coding
4. **EXECUTE**: Apply the Critical Patterns

---

## 📂 LOCAL SKILLS (Angular Specific)

| Skill | Trigger | Path |
|-------|---------|------|
| **angular** | Creating Angular components, services, directives | `skills/angular/skill.md` |
| **clean-architecture** | Designing folder structures, reviewing PRs | `skills/clean-architecture/skill.md` |
| **data-services** | Implementing HTTP data services | `skills/data-services/skill.md` |
| **design-core** | UI design patterns, trading dashboard styling | `skills/design-core/skill.md` |
| **i18n** | Internationalization with @ngx-translate | `skills/i18n/skill.md` |
| **security** | Implementing Auth Guards, Interceptors, JWT handling | `skills/security/skill.md` |
| **angular-automation** | Executing `ng build`, `ng test`, interpreting errors, or **Self-Healing** | `skills/angular-automation/skill.md` |

---

## 📂 INHERITED GLOBAL SKILLS
See `../AGENTS.md` for:
- **Universal Standards**: git-commit, git-pr, documentation-standard, testing-standards
- **Shared Capabilities**: root-orchestrator, analyst-requeriment, architecture-documenter, visual-qa, perform-testing

---

## 🔧 SPECIAL CAPABILITIES

### 🐳 Cloud Infrastructure & Containerization

The agent can manage:
- **Dockerfile**: Multi-stage builds for Angular (Node 22 + Nginx)
- **docker-compose**: Local orchestration with health checks
- **nginx.conf**: SPA routing, security headers, gzip compression
- **AWS ECS Fargate**: Task definitions, service configuration
- **AWS ECR**: Image push and tag management

### 🩺 Self-Healing (Auto-Recovery)

The Frontend agent can **automatically fix** code when a test or build fails:

1. **Classify the error**: Distinguish between TypeScript (TS), Angular (NG), and test failures
2. **Apply minimal fix**: Based on interpretation tables in `angular-automation` skill
3. **Validate**: Re-run with `pnpm run test -- --watch=false`
4. **Limit**: Maximum 2 automatic correction attempts before reporting to user

> **Required Skill**: `skills/angular-automation/skill.md`
> **Standards**: `../universal-skills/testing-standards/skill.md`
> **UI/UX**: `skills/design-core/skill.md`

---

## ⚡ INSTRUCTIONS
- **Do not guess**. If a skill seems relevant, read it.
- **Strict Adherence**: Rules in skill files are non-negotiable laws.
- **Fallback**: If no skill matches, follow Angular 21 / TypeScript best practices.
- **Self-Healing**: When a test or build fails, attempt to fix it before reporting to user.
- **Package Manager**: ALWAYS use `pnpm` for installing dependencies.

---

## 🚫 NON-NEGOTIABLE GIT RULES
- **NEVER** run `git commit` unless the user explicitly asks to commit.
- **NEVER** run `git push` unless the user explicitly asks to push.
- **NEVER** run `git merge`, `git rebase`, or any destructive git operation unless explicitly requested.
- The user owns the git workflow. The agent owns the code.

---

## 🌍 MANDATORY i18n CHECKLIST

Every component that contains **any user-facing text** MUST comply with all of the following before the task is considered complete. No exceptions.

| # | Rule | How to comply |
|---|------|--------------|
| 1 | `TranslateModule` imported | Add to the `imports` array of every standalone component |
| 2 | `TranslateService` injected | Use `inject(TranslateService)` for programmatic strings (toasts, confirm dialogs, dynamic messages) |
| 3 | All template strings use `\| translate` | Replace every hardcoded string in HTML with `{{ 'KEY' \| translate }}` or `[attr]="'KEY' \| translate"` |
| 4 | Keys follow `FEATURE.COMPONENT.ELEMENT` pattern | Use `UPPERCASE.DOT.NOTATION` hierarchy |
| 5 | Dual-Entry Protocol | Every new key must be added to **both** `en.json` AND `es.json` simultaneously |
| 6 | Dynamic values use interpolation | `{{ 'KEY' \| translate:{ param: value } }}` — never template literals or string concatenation |

> **Violation of any rule above is a blocking defect.** The agent must fix it before marking the task as done.
