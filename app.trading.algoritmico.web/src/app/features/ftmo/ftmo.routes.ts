import { Routes } from '@angular/router';

export const FTMO_ROUTES: Routes = [
  {
    path: 'demo/:accountId',
    loadComponent: () =>
      import('../darwinex/account-detail/account-detail.component').then((m) => m.AccountDetailComponent),
  },
  {
    path: 'demo',
    loadComponent: () =>
      import('../darwinex/accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
    data: { accountType: 0, broker: 'FTMO', title: 'FTMO — Cuentas Demo' },
  },
  {
    path: 'live',
    loadComponent: () =>
      import('../darwinex/accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
    data: { accountType: 1, broker: 'FTMO', title: 'FTMO — Cuentas Live' },
  },
  { path: '', redirectTo: 'demo', pathMatch: 'full' },
];
