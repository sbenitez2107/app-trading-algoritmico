# Documentation Standards

Consolidated from: `documentation-standard` + `documentation-features` skills.
Applies to: all documentation, comments, ADRs, feature docs.

---

## TSDoc/JSDoc (exported members)

Every exported member MUST have TSDoc. For Signals/Observables, use `@reactive`:

```typescript
/**
 * Derives total price including tax. Updates when price or taxRate changes.
 * @reactive Derived Signal
 */
readonly total = computed(() => this.price() * (1 + this.taxRate()));
```

## Inline Comments

- Only for complex logic — explain WHY, not WHAT.
- Never leave `TODO` without a ticket number or owner.

## Feature Documentation

Location: `documentation/features/[feature-name-kebab-case].md`

Template:

```markdown
# [Feature Name]

## 1. Overview
[Business value, key capabilities]

## 2. Architecture & Patterns
- Frontend: [stack]
- Backend: [stack]
- Database: [relationships]

## 3. Backend Implementation
### 3.1 Domain Layer — Entity, location, key properties
### 3.2 Application Layer — Commands, Queries
### 3.3 API Layer — Controller, endpoints

## 4. Frontend Implementation
### 4.1 Components — location, features
### 4.2 Services — location
### 4.3 Models & State — signals, stores

## 5. Database & Migrations

## 6. Replication Steps (how to port)
```

## Architecture Decision Records (ADR)

Significant architectural changes MUST be recorded:

```markdown
# ADR-[Number]: [Title]

- **Status**: [Proposed | Accepted | Deprecated | Superseded]
- **Date**: [YYYY-MM-DD]
- **Author**: [Name/Team]

## Context
[Problem or context]

## Decision
[What was decided — be specific]

## Consequences
- **Positive**: [benefits]
- **Negative**: [drawbacks]
- **Compliance**: [enforcement method]
```

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| "Self-documenting" excuse for public APIs | Exported members always need TSDoc |
| TODO without ticket/owner | Always reference a ticket |
| Generic terms ("data", "item") | Use domain terms (SKU, Strategy, Batch) |
| Missing @reactive on Signals | Always tag reactive state |
