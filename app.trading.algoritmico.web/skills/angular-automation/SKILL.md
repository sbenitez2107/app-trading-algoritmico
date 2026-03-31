---
name: angular-automation
description: >
  Angular CLI automation for compilation, testing, and linting.
  Enables the agent to execute `ng build`, `ng test`, and `ng lint`, 
  interpreting TypeScript and Jasmine/Karma failures.
  Trigger: When executing builds, tests, or validating Angular code integrity.
license: Apache-2.0
metadata:
  author: prizm-team
  version: "1.0"
  capabilities:
    - build-execution
    - test-execution
    - output-parsing
    - self-healing
  related_skills:
    - universal-skills/testing-standards/skill.md
---

## When to Use

Use this skill when:
- Building the Web project (`ng build`)
- Executing component and service tests (`ng test`)
- Verifying linting (`ng lint`)
- Interpreting TypeScript or Jasmine/Karma errors
- **Self-healing**: Automatically correcting code when a test or build fails

---

## Capabilities

### Capability 1: Build Execution

Execute `ng build` to detect typing or template errors.

**Command**:
```bash
pnpm run build
# or directly:
ng build
```

**Output interpretation**:
| Indicator | Meaning | Action |
|-----------|---------|--------|
| `Build complete` | Successful compilation | ✅ Continue |
| `error TS...` | TypeScript error | Fix types/interfaces |
| `error NG...` | Angular error (template) | Review bindings/directives |
| `Cannot find module` | Missing import | Add import or install dependency |

### Capability 2: Test Execution

Execute unit tests with Karma/Jasmine.

**Standard command (Health Check)**:
```bash
pnpm run test -- --watch=false --browsers=ChromeHeadless
```

**Output interpretation**:
| Output Pattern | Failure Type | Action |
|----------------|--------------|--------|
| `Expected X to be Y` | Assertion failure | Review logic in component/service |
| `Cannot read property of undefined` | Null reference | Add null checks or initialization |
| `No provider for X` | DI Error | Add provider in TestBed |
| `Error: Template parse errors` | Invalid template | Review component HTML |

### Capability 3: Output Parsing Rules

The agent MUST follow these rules when parsing output:

1. **Look for summary line**: `X specs, Y failures`
2. **If failures > 0**: Look for `FAILED` followed by spec name
3. **If there are TS/NG errors**: It's a compilation problem, NOT logic

---

## Error Interpretation Tables

### Table 1: TypeScript Errors (TS)

| Code | Common Cause | Solution |
|------|--------------|----------|
| `TS2304` | Name not found | Import or declare |
| `TS2339` | Property doesn't exist | Verify interface/type |
| `TS2345` | Incorrect argument type | Fix type |
| `TS2322` | Type not assignable | Cast or fix type |
| `TS7006` | Parameter has implicit `any` type | Add explicit type |

### Table 2: Angular Errors (NG)

| Code | Common Cause | Solution |
|------|--------------|----------|
| `NG0100` | Expression changed after check | Use `ChangeDetectorRef` or `setTimeout` |
| `NG0200` | Circular dependency | Restructure imports |
| `NG0300` | Multiple components match selector | Review selectors |
| `NG0301` | Export not found | Verify module exports |
| `NG8001` | Unknown element | Import standalone component |

---

## Critical Patterns

### Pattern 1: Build Before Test

ALWAYS build before testing to catch template errors.

```bash
# Step 1: Build
pnpm run build

# Step 2: Only if build succeeds, run tests
pnpm run test -- --watch=false --browsers=ChromeHeadless
```

### Pattern 2: TestBed Configuration

Configure TestBed correctly with mocks for DataServices.

```typescript
// ✅ CORRECT: Service mock
beforeEach(async () => {
  const authServiceMock = jasmine.createSpyObj('AuthService', ['logout']);
  authServiceMock.logout.and.returnValue(of(void 0));

  await TestBed.configureTestingModule({
    imports: [ComponentUnderTest],
    providers: [
      { provide: AuthService, useValue: authServiceMock }
    ]
  }).compileComponents();
});
```

### Pattern 3: Signal Testing

Testing Signals in Angular 19+.

```typescript
it('should update signal on action', () => {
  // Arrange
  const component = fixture.componentInstance;
  
  // Act
  component.doAction();
  
  // Assert - use () to read signal
  expect(component.mySignal()).toBe(expectedValue);
});
```

---

## Decision Tree

```
What to validate in Web?
├─ Changes in Components/Signals → ng build + ng test
├─ UI/Design error → ng build (verify templates)
├─ Service refactor → ng test --filter "ServiceName"
└─ Runtime error → Review console.error in browser

Did the build fail?
├─ Error TS... → Fix types in source code
├─ Error NG... → Review template HTML
└─ Cannot find module → pnpm install or add import

Did tests fail?
├─ Expected/toBe → Incorrect logic in component
├─ No provider → Add mock in TestBed
└─ Template parse errors → Import component/module
```

---

## Self-Healing Protocol

When a test or build fails, the agent MAY attempt to automatically correct:

### Step 1: Classify the Error

```
Is it a compilation error (TS/NG)?
├─ Yes → Correct source code
└─ No → Continue to Step 2

Is it a test failure?
├─ Yes, in the test (No provider) → Add mock in TestBed
├─ Yes, in the code (Expected) → Correct component logic
└─ It's a runtime exception → Add null checks
```

### Step 2: Apply Correction

1. **Read the complete error message**
2. **Identify the file** (path in stack trace)
3. **Apply minimal fix** according to error table
4. **Re-execute** with `pnpm run test -- --watch=false`

### Step 3: Validate

- ✅ If passes: Report success
- ❌ If fails: Maximum 2 attempts, then report to user

---

## Commands Reference

```bash
# Production build
pnpm run build

# Development build (faster)
ng build --configuration=development

# Quick health check (STANDARD)
pnpm run test -- --watch=false --browsers=ChromeHeadless

# Test with watch (development)
pnpm run test

# Filtered test
ng test --include="**/auth.service.spec.ts"

# Lint
pnpm run lint
```

---

## Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Test with real dependencies** | Slow, flaky | Use mocks/spies |
| **--watch=true in CI** | Never ends | Use `--watch=false` |
| **Ignoring TS warnings** | Can be bugs | Treat as errors |
| **Infinite self-healing** | Correction loop | Maximum 2 attempts |
