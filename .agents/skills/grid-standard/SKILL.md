---
name: Grid Standard
description: Standardized protocol for building scalable, high-performance, and reactive data grids (Prizm Grid Protocol).
---

# 🦸 Skill: Grid Standard

This skill defines the mandatory standard for all data grids within the Prizm system. It ensures consistency across modules, optimized performance through server-side processing, and a premium user experience.

## 📋 Usage
Use this skill whenever you are:
1. Creating a new list-view or monitor page.
2. Refactoring an existing grid.
3. Adding filtering, sorting, or pagination functionality.

## ⚙️ Process / Steps

### 1. Backend: Server-Side Processing
All grids with potentially large datasets MUST implement server-side processing.

- **DTOs**: Use `GridRequestDto` for incoming requests and `GridResponseDto<T>` for outgoing data.
- **Persistence**: Use `QueryableExtensions.ToGridResponseAsync<T>` to apply sorting, paging, and filters at the SQL level.
- **Filtering**: Implement `ApplyFilters` logic to handle `FilterClauseDto` items (eq, contains, gt, lt).

### 2. Frontend: Reactive DataSource
Integrate with Angular Material using a server-side strategy.

- **Data Services**: Services MUST accept `GridRequestDto` (handling `PageIndex`, `PageSize`, `SortField`, `SortOrder`, `GlobalFilter`, and `Filters` array).
- **Component**:
    - Use `MatSort` and `MatPaginator`.
    - Maintain a `columnFilters` object to track specific field values and operators.
    - Trigger reloads by merging `sortChange`, `page`, global `filterSubject`, and a specific `columnFilterSubject`.
- **Filtering UI**:
    - **Global Filter**: Search input with 400ms debounce.
    - **Column Filters (The standard)**: Use `mat-icon-button` with `matMenuTriggerFor` inside table headers.
    - Icons MUST use `.material-symbols-outlined .filter_alt`.
    - Icons MUST turn blue (`.active-filter`) when a filter is active for that column.
    - **Crucial UI Standard**: All filter inputs MUST have `border: none !important`, `outline: none !important`, and `box-shadow: none !important` applied to the inner `input` element to avoid the "box-inside-box" effect.
    - **Standard Actions**: Use `.filter-actions` container with:
        - `Apply` button: `.btn-apply` (Primary style, small, rounded).
        - `Reset` button: `.btn-reset` (Transparent/Text style, small).

### 3. Aesthetics: The "WOW" Look
- **Status Badges**: Use unified CSS classes (`status-badge`) with `data-status` attributes for consistent coloring.
- **Skeleton Loading**: Show grid skeletons or a persistent progress bar while `loading()` is true.
- **Modal Details**: Use a premium modal with backdrop blur and animations for order/item details.
- **Header Design**: Column headers with filters should use a flex container to align text and filter triggers.
- **Filter Menu Appearance**: Menus (`.grid-filter-menu`) MUST have a minimum width (e.g., 240px), rounded corners (12px+), and subtle elevation.

## ⚙️ Backend Protocol for Filters
The backend `Handle` method for grid queries MUST:
1.  Apply `GlobalFilter` searching across multiple relevant text fields.
2.  Iterate through the `Filters` array from the request.
3.  Support `contains` for text fields and exact matches for enums/status.
4.  Use `ToGridResponseAsync` for optimized materialization.

## 🎨 Global CSS Pattern for Filter Menus (Mandatory)
```scss
/* --- Prizm Grid Protocol: Standard Filter Menu Styles --- */

.grid-filter-menu.mat-mdc-menu-panel {
  min-width: 280px !important;
  border-radius: 16px !important;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1) !important;
  margin-top: 12px !important;
  background: white !important;
  overflow: hidden !important;
}

.filter-menu-content {
  padding: 1.5rem 1.25rem !important;

  .compact-field {
    width: 100% !important;

    // 🛑 TOTAL ANNIHILATION OF MATERIAL DEFAULTS
    .mat-mdc-form-field-flex, .mat-mdc-form-field-outline, .mdc-notched-outline,
    .mat-mdc-form-field-focus-overlay, .mdc-line-ripple {
        display: none !important;
        border: none !important;
        opacity: 0 !important;
    }

    input.mat-mdc-input-element, input {
      display: block !important;
      border: 1.5px solid #e2e8f0 !important;
      background: #f8fafc !important; 
      padding: 12px 14px !important;
      font-size: 0.95rem !important;
      border-radius: 10px !important;
      width: 100% !important;
      transition: all 0.2s ease;

      &:focus {
          border-color: #3248c3 !important;
          background: white !important;
          box-shadow: 0 0 0 4px rgba(50, 72, 195, 0.1) !important;
      }
    }
  }

  .filter-actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
    border-top: 1px solid #f1f5f9;
    padding-top: 1.25rem;

    .btn-apply {
      background: #3248c3 !important; 
      color: white !important;
      border-radius: 10px !important;
      font-weight: 700 !important;
      font-size: 0.85rem !important;
      height: 40px !important;
      padding: 0 1.5rem !important;
      box-shadow: 0 4px 6px -1px rgba(50, 72, 195, 0.25) !important;
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
      
      &:hover { transform: translateY(-1.5px); box-shadow: 0 10px 15px -3px rgba(50, 72, 195, 0.3) !important; }
    }

    .btn-reset {
      background: #f1f5f9 !important;
      color: #64748b !important;
      border: 1px solid #e2e8f0 !important;
      border-radius: 10px !important;
      font-weight: 600 !important;
      height: 40px !important;
      padding: 0 1.25rem !important;
      transition: all 0.2s;
      
      &:hover { background: #e2e8f0 !important; color: #1e293b !important; }
    }
  }
}
```

## ⚠️ Standards & Rules
- **NEVER** fetch the entire dataset and filter in memory.
- **ALWAYS** include a search-on-type debounce (min 400ms) for all filters.
- **CLEAN UI**: Hide raw JSON payloads behind a toggle buttons in detail modals.
- **CONSISTENCY**: All filter icons must be positioned to the right of the header text.
- **INTERACTION**: Buttons MUST have visible hover states (transform or background change).
