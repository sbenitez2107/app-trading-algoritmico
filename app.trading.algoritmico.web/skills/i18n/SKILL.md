---
name: i18n
description: >
  Best practices for internationalization using ngx-translate in Angular applications.
  Trigger: When creating new components, screens, or adding user-facing text to templates.
license: Apache-2.0
metadata:
  author: code-assistant
  version: "2.0"
  path_i18n: "src/assets/i18n/"
---

## When to Use

Use this skill when:
- Creating new Angular components or screens
- Adding user-facing text to templates
- Managing translation files in `assets/i18n/`
- Refactoring hardcoded strings to use translations

---

## Critical Patterns

The following patterns are MANDATORY for internationalization.

### Pattern 1: Key Naming Convention
ALWAYS use hierarchical UPPERCASE dot notation: `FEATURE.COMPONENT.ELEMENT`

```json
{
  "DASHBOARD": {
    "HEADER": {
      "TITLE": "Dashboard",
      "SUBTITLE": "Welcome back"
    }
  },
  "AUTH": {
    "LOGIN": {
      "TITLE": "Sign In",
      "SUBMIT_BUTTON": "Login"
    }
  }
}
```

### Pattern 2: Mandatory Interpolation
NEVER concatenate strings. Use ngx-translate parameters for dynamic values.

```html
<!-- BAD - String concatenation -->
<p>{{ 'CATALOG.ITEMS' | translate }}: {{ totalItems() }}</p>

<!-- GOOD - Interpolation -->
<p>{{ 'CATALOG.ITEMS_COUNT' | translate:{ count: totalItems() } }}</p>
```

```json
{
  "CATALOG": {
    "ITEMS_COUNT": "Items: {{count}}"
  }
}
```

### Pattern 3: Angular 21 Standalone Integration
Import `TranslateModule` in standalone components. Prefer **pipe over service** for Zoneless performance.

```typescript
@Component({
  standalone: true,
  imports: [TranslateModule],
  template: `<h1>{{ 'DASHBOARD.HEADER.TITLE' | translate }}</h1>`
})
export class DashboardComponent {}
```

### Pattern 4: Programmatic Translations (TypeScript only)
Use `TranslateService.instant()` ONLY when pipe is not possible.

```typescript
private translate = inject(TranslateService);

showMessage() {
  const msg = this.translate.instant('COMMON.SUCCESS_MESSAGE');
  this.notification.show(msg);
}
```

### Pattern 5: Never Hardcode Strings
All user-visible text MUST use translation keys. NO EXCEPTIONS.

---

## Agent Protocol

### Dual-Entry Protocol
When adding a new key:
1. Add it to `en.json` first
2. **Simultaneously** add to `es.json` (use Spanish value or English as placeholder)
3. Never leave a key missing in any language file

### Search Before Creating
Before creating a new key:
1. Search existing JSON files for similar terms
2. Reuse generic keys from `COMMON` section when possible
3. Only create new keys if no suitable match exists

---

## UI vs Data Distinction

| Type | Source | Translation Method |
|------|--------|-------------------|
| **UI Strings** | Labels, buttons, placeholders | `ngx-translate` (JSON files) |
| **Product Data** | Names, descriptions, attributes | API (backend) - DO NOT use i18n |
| **Business Terms** | SKU, Asset, Attribute, Variant | Use PIM glossary (from API) |

```html
<!-- UI String - USE i18n -->
<label>{{ 'PRODUCTS.FORM.NAME_LABEL' | translate }}</label>

<!-- Product Data - DO NOT use i18n, comes from API -->
<span>{{ product.name }}</span>
```

---

## Anti-Patterns (PROHIBITED)

❌ **snake_case or lowercase keys**
```json
// BAD
{ "error_message": "Error" }

// GOOD  
{ "ERROR": { "MESSAGE": "Error" } }
```

❌ **Logic in key names**
```json
// BAD - describes visual state
{ "STATUS": { "IS_RED": "Error", "IS_GREEN": "Success" } }

// GOOD - describes meaning
{ "STATUS": { "ERROR": "Error", "SUCCESS": "Success" } }
```

❌ **String concatenation**
```html
<!-- BAD -->
{{ 'LABEL' | translate }} + value

<!-- GOOD -->
{{ 'LABEL_WITH_VALUE' | translate:{ value: value } }}
```

❌ **Hardcoding business terms**
Always use the PIM glossary terms from the backend.

---

## Decision Tree

```
Adding text to a template?
  ├── Is it UI text? → Use translation key with pipe
  ├── Is it product data? → Use API value directly (NO i18n)
  ├── Is it in TypeScript? → Use TranslateService.instant()
  └── Is it a placeholder/title? → Use [attr.placeholder]="'KEY' | translate"

Creating a new key?
  ├── Search existing keys first
  ├── Can reuse COMMON key? → Reuse it
  ├── Create new key → Add to BOTH en.json AND es.json
  └── Use UPPERCASE.DOT.NOTATION
```

---

## File Structure

```
src/
└── assets/
    └── i18n/
        ├── en.json    # English (default)
        └── es.json    # Spanish
```

---

## Common Keys Template

```json
{
  "COMMON": {
    "ACTIONS": {
      "SAVE": "Save",
      "CANCEL": "Cancel",
      "DELETE": "Delete",
      "EDIT": "Edit",
      "CREATE": "Create",
      "SEARCH": "Search"
    },
    "STATUS": {
      "LOADING": "Loading...",
      "SUCCESS": "Success",
      "ERROR": "Error"
    },
    "VALIDATION": {
      "REQUIRED": "This field is required",
      "INVALID_EMAIL": "Invalid email address"
    }
  }
}
```

