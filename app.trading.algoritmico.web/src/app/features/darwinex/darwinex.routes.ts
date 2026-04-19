import { Routes } from '@angular/router';

export const DARWINEX_ROUTES: Routes = [
  {
    path: 'demo/:accountId',
    loadComponent: () =>
      import('./account-detail/account-detail.component').then((m) => m.AccountDetailComponent),
  },
  {
    path: 'demo',
    loadComponent: () =>
      import('./accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
    data: { accountType: 0, broker: 'Darwinex', title: 'Darwinex — Cuentas Demo' },
  },
  {
    path: 'live',
    loadComponent: () =>
      import('./accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
    data: { accountType: 1, broker: 'Darwinex', title: 'Darwinex — Cuentas Live' },
  },
  { path: '', redirectTo: 'demo', pathMatch: 'full' },
];
