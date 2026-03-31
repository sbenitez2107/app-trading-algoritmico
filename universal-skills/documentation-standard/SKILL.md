---
name: documentation-standard
description: >
  Strict standards for code documentation, READMEs, and architectural records.
  Enforces TSDoc for all exported members, structural documentation for features, and formal ADRs.
trigger: Writing comments, creating READMEs, documenting functions/classes, explaining code, or making architectural decisions.
license: Apache-2.0
metadata:
  author: generic-user
  version: "1.1"
  strict_mode: true
---

## When to Use

Use this skill when:
- Writing TSDoc/JSDoc comments for classes, interfaces, or functions.
- Creating or updating `README.md` files for features or libraries.
- Documenting architectural decisions (ADRs).
- Explaining complex logic in code.

---

## Critical Patterns

### Pattern 1: TSDoc with Angular 21 Reactivity
> **Rule**: Every exported member MUST have TSDoc. For Signals or Observables, you MUST use the `@reactive` tag to indicate if it matches the Angular 21 Signal-based architecture.

```typescript
/**
 * calculatedTotal
 * 
 * Derives the total price including tax from the base price signal.
 * Updates automatically when price or tax rate changes.
 * 
 * @reactive Derived Signal
 * @returns Signal<number> The reactive total price.
 */
readonly total = computed(() => this.price() * (1 + this.taxRate()));
```

### Pattern 2: Feature README with Business Rules
> **Rule**: Feature READMEs must include a "Business Rules" section using domain terminology (e.g., SKU, Asset, Attribute) to align technical implementation with business value.

```markdown
# Product Catalog Feature

## Overview
Manages the lifecycle of standard Products and Variants (SKUs).

## Business Rules
- **SKU Uniqueness**: Every Variant must have a unique SKU within the Tenant.
- **Asset Linkage**: Products generally have at least one primary Asset (Image).
- **Attribute Inheritance**: Variants inherit Attributes from the parent Product unless overridden.

## Key Components
- `ProductListComponent`: Displays the grid of SKUs.
- `ProductService`: Handles CRUD operations for Assets and Attributes.
```

### Pattern 3: Architecture Decision Records (ADR)
> **Rule**: Significant architectural changes (infrastructure, new libraries, pattern changes) MUST be recorded using the strict ADR template.

---

## Reasoning Protocol

Before documenting or implementing, the agent MUST perform the following checks:

1.  **Reactivity Check**: Does this variable or function involve state changes?
    - *If YES*: Verify it uses Signals (or Observables if legacy) and document with `@reactive`.
    - *If NO*: Document as a pure function or static value.
2.  **Business Alignment**: Are we using the correct business terms?
    - *Check*: Replace generic terms (Item, Record, Object) with specific Domain terms (SKU, Asset, Attribute, Wave, Replenishment).
3.  **Completeness Check**: Did we cover *Why* this exists, not just *What* it does?

---

## Decision Tree (Documentation Level)

```mermaid
graph TD
    A[Code Entity] --> B{Is it Exported?}
    B -- Yes --> C[Require TSDoc + @reactive check]
    B -- No --> D{Is logic complex?}
    D -- Yes --> E[Inline Comment (Why, not What)]
    D -- No --> F[No Comment Needed]
```

---

## Anti-Patterns
- ❌ **Forbidden**: "Self-documenting code" excuse for public APIs. Exported members ALWAYS need context.
- ❌ **Forbidden**: Leaving `TODO` comments without a ticket number or owner.
- ❌ **Forbidden**: Using generic terms like "data" or "item" in documentation when "SKU" or "Wave" applies.
- ❌ **Forbidden**: Missing the `@reactive` tag on public Signals.

---

## ADR Template

When creating a new ADR, use exactly this structure:

```markdown
# ADR-[Number]: [Title]

- **Status**: [Proposed | Accepted | Deprecated | Superseded]
- **Date**: [YYYY-MM-DD]
- **Author**: [Name/Team]

## Context
[Describe the problem or context that necessitates this decision.]

## Decision
[Describe the decision made. Be specific about technologies, patterns, or changes.]

## Consequences
- **Positive**: [Benefit 1], [Benefit 2]
- **Negative**: [Drawback 1], [Drawback 2]
- **Compliance**: [How will we ensure this is followed?]
```
