---
name: frontend-standards
description: Defines the standard UX/UI patterns for App Trading Algoritmico Frontend.
---

# Frontend Development Standards

## 1. Modals (Dialogs)
- **Pattern**: Use the standardized `modal-overlay` and `confirmation-modal` classes.
- **Logic**: Use `showConfirm(title, message, action)` method.
- **HTML Pattern**:
```html
<div class="modal-overlay" *ngIf="showConfirmModal" (click)="cancelAction()">
  <div class="confirmation-modal" (click)="$event.stopPropagation()">
    <div class="modal-header">
      <h3>{{ confirmModalTitle }}</h3>
      <button class="btn-close" (click)="cancelAction()">
        <span class="material-symbols-outlined">close</span>
      </button>
    </div>
    <div class="modal-body"><p>{{ confirmModalMessage }}</p></div>
    <div class="modal-footer">
      <button class="btn-secondary" (click)="cancelAction()">Cancel</button>
      <button class="btn-danger" (click)="confirmAction()">Confirm</button>
    </div>
  </div>
</div>
```

## 2. Notifications (Toasts)
- **Pattern**: Use `showSuccess` and `showError` booleans with scheduled timeouts.
- **HTML Pattern**:
```html
<div class="toast-container" *ngIf="showSuccess">
  <div class="toast-success">
    <span class="material-symbols-outlined">check_circle</span>
    <span>{{ successMessage }}</span>
  </div>
</div>
```

## 3. Global Buttons
- **Primary**: `.btn-primary` (Blue gradient/solid)
- **Secondary**: `.btn-secondary` (White/Bordered)
- **Danger**: `.btn-danger` (Red)

## 4. Visual Excellence
- **Cards**: Use `.c-card` for consistent surfacing.
- **Human Readable Data**: Always format technical data (cron expressions, IDs) into human-readable strings (e.g., "Daily at 14:00").



