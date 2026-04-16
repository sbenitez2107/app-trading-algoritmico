# Frontend Core — Angular 21 & Clean Architecture

## When to Use

Use these conventions when:
- Creating new Angular components (`@Component`), directives, or pipes.
- Writing Services or managing state.
- Refactoring legacy Angular code (NgModules) to modern standards.
- Fixing accessibility issues in templates.
- Designing folder structures for new features.
- Reviewing PRs for architectural violations.
- Deciding where to place business logic vs. UI logic.
- Detecting circular dependencies or "God classes".

---

## Critical Patterns — Angular 21

The following patterns are MANDATORY for Angular 21 development.

### Pattern 1: Signals for State
ALWAYS use Signals for local state and `computed()` for derived values.

```typescript
// BAD
count = 0;
doubleCount = 0;
increment() {
  this.count++;
  this.doubleCount = this.count * 2;
}

// GOOD
count = signal(0);
doubleCount = computed(() => this.count() * 2);
increment() {
  this.count.update(c => c + 1);
}
```

### Pattern 2: Standalone Components
All components MUST be standalone. DO NOT use NgModules. 
Note: In Angular 21+, `standalone: true` is practically default, but if explicit config is needed, ensure it is set.

```typescript
@Component({
  selector: 'app-user',
  // standalone: true, // Default in v20+
  imports: [CommonModule, MatButtonModule],
  templateUrl: './user.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserComponent {}
```

### Pattern 3: Modern Dependency Injection
Use `inject()` instead of constructor injection.

```typescript
// BAD
constructor(private http: HttpClient) {}

// GOOD
private http = inject(HttpClient);
```

### Pattern 4: Component Inputs/Outputs
Use the new Signal-based `input()` and `output()` API.

```typescript
// BAD
@Input() title: string;
@Output() save = new EventEmitter<void>();

// GOOD
title = input.required<string>();
save = output<void>();
```

### Pattern 5: Propriety Pattern (File Separation)
All components detailed logic, view, and styles MUST be separated into distinct files.
- `component.ts`: Logic and State (Signals/Inputs).
- `component.html`: Template and Control Flow.
- `component.scss`: Styles and Design Tokens.

```typescript
@Component({
  selector: 'app-example',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './example.component.html',
  styleUrl: './example.component.scss' // Note: Use styleUrl (singular) in v17+
})
export class ExampleComponent {}
```

---

## Decision Tree — Angular State & Composition

```
State Management?
  +-- Local State? -> Use signal()
  +-- Derived State? -> Use computed()
  +-- Global/Complex? -> Use Service with signals (or NGRX if installed)

Component Composition?
  +-- Needs child components? -> Add to `imports: []` array
  +-- Passing data down? -> Use input()
  +-- Passing events up? -> Use output()

Performance?
  +-- Always set changeDetection: ChangeDetectionStrategy.OnPush
```

---

## Templates

- **Control Flow**: Use `@if`, `@for`, `@switch`. Do NOT use `*ngIf`, `*ngFor`.
- **Bindings**: Use `[class.name]="bool"` instead of `[ngClass]`.
- **Optimization**: Use `NgOptimizedImage` (`ngSrc`) for images.

---

## Accessibility (A11y)

- **Strict**: Must pass AXE checks.
- **Forms**: Associate labels with controls (`for` + `id`).
- **Interactive**: Ensure only interactive elements (`<button>`, `<a>`) have click handlers.

---

## Critical Patterns — Clean Architecture

The following patterns are MANDATORY. Violations are architectural bugs.

### Pattern 1: The Dependency Rule
Dependencies MUST point inwards. The core (Domain/Entities) must NOT know about the outer layers (UI, Database, Presenters).

```
UI / Presentation --> Application UseCases
Infrastructure / Data --> Application UseCases
Application UseCases --> Domain / Entities
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

## Decision Tree — Code Placement

```
Where does this code go?
  +-- Is it a Core Business Rule? (e.g., Tax calculation) -> DOMAIN (Entity/Service)
  +-- Is it application flow? (e.g., Get user -> Validate -> Save) -> APPLICATION (UseCase)
  +-- Is it formatting/display? (e.g., Date string, Colors) -> PRESENTATION (UI/ViewModel)
  +-- Is it I/O or 3rd Party? (e.g., API Call, LocalStorage) -> INFRASTRUCTURE
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
