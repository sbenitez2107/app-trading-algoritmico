# Frontend Grid Standard

Consolidated from: `grid-standard` skill (Prizm Grid Protocol).
Applies to: list views, monitor pages, data tables.

---

## Server-Side Processing (mandatory for large datasets)

### Backend

- `GridRequestDto`: PageIndex, PageSize, SortField, SortOrder, GlobalFilter, Filters[]
- `GridResponseDto<T>`: Items, TotalCount, PageIndex, PageSize
- `QueryableExtensions.ToGridResponseAsync<T>`: sorting, paging, and filters at SQL level
- `ApplyFilters`: handles `FilterClauseDto` items (eq, contains, gt, lt)

### Frontend

- Use `MatSort` + `MatPaginator`
- Maintain `columnFilters` object for field-level filters
- Trigger reloads by merging `sortChange`, `page`, `filterSubject`, `columnFilterSubject`

---

## Filtering

### Global Filter: search input with 400ms debounce

### Column Filters

- `mat-icon-button` with `matMenuTriggerFor` in headers
- Icons: `.material-symbols-outlined .filter_alt`
- Active filter: icon turns blue (`.active-filter`)
- Filter input: `border: none !important`, `outline: none !important`, `box-shadow: none !important`
- Actions: `.btn-apply` (primary) + `.btn-reset` (transparent)

---

## Aesthetics

- **Status Badges**: `.status-badge` with `data-status` attributes
- **Skeleton Loading**: show while `loading()` is true
- **Modal Details**: backdrop blur + animations
- **Header Design**: flex container for text + filter trigger alignment
- **Filter Menu**: `.grid-filter-menu` — min-width 240px, border-radius 12px+, subtle elevation

---

## Rules

- NEVER fetch entire dataset and filter in memory.
- ALWAYS include 400ms debounce on all filter inputs.
- All filter icons positioned right of header text.
- Buttons MUST have visible hover states.
