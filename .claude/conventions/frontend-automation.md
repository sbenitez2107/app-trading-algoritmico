# Frontend Automation & Testing

Consolidated from: `angular-automation` skill.
Applies to: `app.trading.algoritmico.web/` builds, tests, and self-healing.

---

## Commands

```bash
pnpm run build                                  # Production build
ng build --configuration=development            # Dev build (faster)
npx ng test --watch=false                       # Quick test (CI mode)
pnpm run test                                   # Test with watch (dev)
```

## Error Classification

### TypeScript Errors (TS)

| Code | Cause | Action |
|------|-------|--------|
| `TS2304` | Name not found | Import or declare |
| `TS2339` | Property doesn't exist | Verify interface/type |
| `TS2345` | Incorrect argument type | Fix type |
| `TS2322` | Type not assignable | Cast or fix type |
| `TS7006` | Implicit `any` | Add explicit type |

### Angular Errors (NG)

| Code | Cause | Action |
|------|-------|--------|
| `NG0100` | Expression changed after check | ChangeDetectorRef or signal |
| `NG0200` | Circular dependency | Restructure imports |
| `NG8001` | Unknown element | Import standalone component |

### Test Failures

| Pattern | Cause | Action |
|---------|-------|--------|
| `Expected X to be Y` | Logic error | Review component/service |
| `Cannot read property of undefined` | Null reference | Add null checks |
| `No provider for X` | Missing DI | Add provider in TestBed |

## TestBed Configuration

```typescript
beforeEach(async () => {
  const authMock = jasmine.createSpyObj('AuthService', ['logout']);
  await TestBed.configureTestingModule({
    imports: [ComponentUnderTest],
    providers: [{ provide: AuthService, useValue: authMock }]
  }).compileComponents();
});
```

## Signal Testing

```typescript
it('should update signal', () => {
  component.doAction();
  expect(component.mySignal()).toBe(expectedValue); // read with ()
});
```

## Self-Healing Protocol

1. Classify: compilation (TS/NG) vs assertion vs runtime
2. Read complete error with file path
3. Apply minimal fix — correct CODE, not the test
4. Re-run: `npx ng test --watch=false`
5. Max 2 attempts, then report to user

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Real dependencies in tests | Use mocks/spies |
| --watch=true in CI | Use --watch=false |
| Modifying test to pass | Fix the code |
| Infinite self-healing | Max 2 attempts |
