---
name: clean-architecture
description: >
  Guardrails for enforcing Clean Architecture principles. Ensures proper dependency directions and separation of concerns.
  Trigger: Use when reviewing code structure, creating new features/modules, or refactoring business logic.
license: Apache-2.0
metadata:
  author: code-assistant
  version: "1.0"
---

## When to Use

Use this skill when:
- Designing folder structures for new features.
- Reviewing PRs for architectural violations.
- Deciding where to place business logic vs. UI logic.
- Detecting circular dependencies or "God classes".

---

## Critical Patterns

The following patterns are MANDATORY. Violations are architectural bugs.

### Pattern 1: The Dependency Rule
Dependencies MUST point inwards. The core (Domain/Entities) must NOT know about the outer layers (UI, Database, Presenters).

```{mermaid}
graph TD
    UI[UI / Presentation] --> App[Application UseCases]
    Infra[Infrastructure / Data] --> App
    App --> Domain[Domain / Entities]
```

### Pattern 2: No Business Logic in UI
UI Components (Angular Components) must ONLY handle view logic (state, inputs, outputs). UseCases or Services must handle business rules.

```typescript
// BAD: Logic in Component
submitOrder() {
  if (this.order.total > 100 && this.user.isPrime) { // Business Logic leaking
    this.http.post('/api/orders', ...).subscribe();
  }
}

// GOOD: Delegating to UseCase/Service
submitOrder() {
  this.createOrderUseCase.execute(this.order);
}
```

### Pattern 3: Abstraction over Implementation
The Application Core should depend on interfaces (Gateways/Repositories), not concrete Infrastructure implementations.

```typescript
// BAD: Direct dependency on concrete class
constructor(private api: GoogleMapsApi) {}

// GOOD: Dependency on Domain Interface
constructor(private mapService: IMapProvider) {}
```

---

## Decision Tree

```
Where does this code go?
  ├── Is it a Core Business Rule? (e.g., Tax calculation) → DOMAIN (Entity/Service)
  ├── Is it application flow? (e.g., Get user -> Validate -> Save) → APPLICATION (UseCase)
  ├── Is it formatting/display? (e.g., Date string, Colors) → PRESENTATION (UI/ViewModel)
  └── Is it I/O or 3rd Party? (e.g., API Call, LocalStorage) → INFRASTRUCTURE
```

---

## Anti-Patterns (Guardrails)

- **Circular Dependencies**: If Module A imports Module B, Module B CANNOT import Module A. Use a shared abstractions module.
- **God Services**: Services with >10 public methods or mixed responsibilities. Split them by Feature or UseCase.
- **Leaking DTOs**: Database DTOs should not reach the UI. Map them to Domain Models or ViewModels at the boundary.

---

## Verification Commands

```bash
# Verify circular dependencies (if madge is installed)
npx madge --circular src/
```
