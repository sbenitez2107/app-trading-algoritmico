# Frontend Data — Services, i18n & Security

Consolidated from: `data-services` + `i18n` + `security` (web) skills.
Applies to: `core/services/`, `app.config.ts`, `assets/i18n/`, guards, interceptors.

---

## Data Services — Transformation Pipeline

### DTO → Domain Model (always)

```typescript
// 1. DTO: exact backend response
export interface ProductDTO { p_id: string; price_val: number; }

// 2. Domain Model: clean for UI
export interface Product { id: string; price: number; }

// 3. Mapper: pure function
export const mapProduct = (dto: ProductDTO): Product => ({
  id: dto.p_id, price: dto.price_val
});
```

### Signal-Based Data Fetching

```typescript
@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private http = inject(HttpClient);
  private baseUrl = inject(API_BASE_URL);

  getProducts(): Signal<Product[]> {
    return toSignal(
      this.http.get<ProductDTO[]>(`${this.baseUrl}/products`).pipe(
        map(dtos => dtos.map(mapProduct)),
        catchError(() => of([]))
      ), { initialValue: [] }
    );
  }
}
```

### Error Handling Decision Tree

```
Background sync? → Silent fail + log
Critical for UI? → Global error signal + toast
Not critical? → Return empty/default value
```

---

## i18n — ngx-translate

### Key Naming: UPPERCASE.DOT.NOTATION

```json
{ "DASHBOARD": { "HEADER": { "TITLE": "Dashboard" } } }
```

### Interpolation (never concatenation)

```html
<!-- GOOD -->
<p>{{ 'CATALOG.ITEMS_COUNT' | translate:{ count: totalItems() } }}</p>
```

### Dual-Entry Protocol

When adding a key: add to BOTH `en.json` AND `es.json` simultaneously.

### Search Before Creating

Reuse `COMMON` section keys when possible.

### UI vs Data

| Type | Source | Method |
|------|--------|--------|
| UI strings (labels, buttons) | i18n JSON | `translate` pipe |
| Product data (names, descriptions) | API | Direct binding |

---

## Frontend Security

### Functional Route Guard

```typescript
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isAuthenticated()) return true;
  return router.createUrlTree(['/login']);
};
```

ALL feature routes MUST be protected by `authGuard` on the parent layout route.

### Auth Interceptor

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).getToken();
  if (token) return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  return next(req);
};
```

### XSS Prevention

Angular sanitizes by default. Use `DomSanitizer.bypassSecurityTrustHtml()` ONLY for verified trusted content.

### Token Storage

Prefer `HttpOnly Cookies`. If not available, use `localStorage` with clear-on-logout.

### CSP — configure in `index.html` meta tag

---

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| `any` for API responses | Always define a DTO interface |
| Mapping data in Component | Map in Service or Mapper file |
| Hardcoded API base URL | Use InjectionToken or environment |
| Nested subscriptions | Use RxJS operators or Signals |
| Token in URL query params | Bearer header only |
| Secrets in frontend code | NEVER — all JS is public |
| snake_case i18n keys | UPPERCASE.DOT.NOTATION |
| String concatenation for i18n | Use translate interpolation |
