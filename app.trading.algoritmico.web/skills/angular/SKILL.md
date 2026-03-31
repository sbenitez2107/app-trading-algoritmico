---
name: angular
description: >
  Best practices for Angular 21 development in app-trading-algoritmico, focusing on Signals, Standalone Components, and modern TypeScript patterns.
  Trigger: Use when writing or refactoring Angular code, components, services, or templates.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Creating new Angular components (`@Component`), directives, or pipes.
- Writing Services or managing state.
- Refactoring legacy Angular code (NgModules) to modern standards.
- Fixing accessibility issues in templates.

---

## Critical Patterns

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

## Decision Tree

```
State Management?
  ├── Local State? → Use signal()
  ├── Derived State? → Use computed()
  └── Global/Complex? → Use Service with signals (or NGRX if installed)

Component Compositon?
  ├── Needs child components? → Add to `imports: []` array
  └── Passing data down? → Use input()
  └── Passing events up? → Use output()

Performance?
  └── Always set changeDetection: ChangeDetectionStrategy.OnPush
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

