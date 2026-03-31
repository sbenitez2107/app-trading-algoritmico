---
name: security
description: >
  Security patterns for Angular. Covers Route Guards, Auth Interceptor,
  XSS sanitization, secure token handling, and Content Security Policy (CSP).
  Trigger: When configuring protected routes, handling tokens, or sanitizing inputs.
license: Apache-2.0
metadata:
  author: prizm-team
  version: "1.0"
---

## When to Use

Use this skill when:
- Protecting routes (Guards)
- Adding tokens to requests (Interceptors)
- Handling JWT storage (Local/Session Storage vs Cookies)
- Rendering dynamic HTML (`[innerHTML]`)
- Configuring security headers

---

## Critical Patterns

### Pattern 1: Route Guards (Functional)

Use Functional Guards (`CanActivateFn`) to protect routes.

```typescript
// auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Redirect with returnUrl
  return router.createUrlTree(['/login'], { 
    queryParams: { returnUrl: state.url } 
  });
};

// app.routes.ts
export const routes: Routes = [
  { 
    path: 'dashboard', 
    component: DashboardComponent, 
    canActivate: [authGuard] 
  }
];
```

### Pattern 2: Auth Interceptor (Functional)

Interceptor to attach JWT Token to outgoing requests.

```typescript
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  if (token) {
    const cloned = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
    return next(cloned);
  }

  return next(req);
};

// app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
```

### Pattern 3: XSS Sanitization

Angular sanitizes by default, but be careful with `[innerHTML]`. Use `DomSanitizer` only when strictly necessary and safe.

```typescript
// ❌ RISK: If 'untrustedHtml' comes from user
<div [innerHTML]="untrustedHtml"></div>

// ✅ BETTER: Use UI components or explicitly sanitize trusted content
export class SafeHtmlPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}
  transform(html: string) {
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}
```

### Pattern 4: Secure Token Storage

Prefer `HttpOnly Cookies` if backend supports it. If not, use `localStorage` carefully.

```typescript
// auth.service.ts
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';

  saveToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  // Clear on logout or 401 error
  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    this.router.navigate(['/login']);
  }
}
```

### Pattern 5: Content Security Policy (CSP)

Configure CSP in `index.html` or via meta tag to prevent XSS and Clickjacking.

```html
<!-- index.html -->
<meta http-equiv="Content-Security-Policy" content="
  default-src 'self'; 
  script-src 'self' 'unsafe-inline'; 
  style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; 
  font-src 'self' https://fonts.gstatic.com;
  img-src 'self' data: https:;
  connect-src 'self' https://api.mydomain.com;
">
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Token in URL** | Stays in history/logs | Never pass token via query params |
| **bypassSecurityTrust...** | Disables XSS protection | Avoid if possible, audit inputs |
| **Logic in Templates** | Exposes business logic | Logic in Component/Service |
| **Secrets in Frontend** | All JS code is public | NEVER put secret API Keys in Angular |
| **Eval()** | Arbitrary code execution | Strictly prohibited |

---

## Mandatory Standards

### Route Protection (Required)

> [!IMPORTANT]
> All feature routes under `/dashboard`, `/settings`, or any internal path MUST be protected by `authGuard`.

**Implementation:**

1. Apply `canActivate: [authGuard]` to the parent layout route
2. All child routes inherit the guard automatically
3. Only `/login` and public pages should be unprotected

```typescript
// app.routes.ts - MANDATORY PATTERN
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
    { path: 'login', ... }, // ✅ Public - no guard
    {
        path: '',
        component: MainLayoutComponent,
        canActivate: [authGuard], // ✅ REQUIRED
        children: [
            { path: 'dashboard', ... },
            { path: 'settings', ... },
            // All children are protected
        ]
    }
];
```

**Guard Implementation (Signals):**

```typescript
// core/guards/auth.guard.ts
export const authGuard: CanActivateFn = () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    // ✅ Uses Signal for reactive state
    if (authService.isAuthenticated()) {
        return true;
    }

    return router.createUrlTree(['/login']);
};
```

---

## Verification Commands

```bash
# Build check
pnpm build

# Lint for security issues
pnpm lint
```

