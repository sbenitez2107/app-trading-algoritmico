import { Routes } from '@angular/router';
import { portfolioRoutes } from '../portfolios/portfolios.routes';

/**
 * Configuration for a broker's account routes.
 * `basePath` is the absolute router path the components use for internal
 * navigation (e.g. clicking a demo account, the back button). It MUST match
 * the path this broker is mounted under in the root router config.
 */
export interface BrokerAccountsConfig {
  /** Broker/PropFirm display name (e.g. "Darwinex", "FTMO"). Drives titles + account filtering. */
  broker: string;
  /** Absolute base path this broker is mounted at (e.g. "/darwinex", "/ftmo"). */
  basePath: string;
}

/**
 * Builds the Demo/Live account routes for a broker. Both Darwinex and FTMO
 * (and any future prop firm) share the same components — the only difference
 * is the broker name and the base path injected via route data.
 */
export function brokerAccountsRoutes({ broker, basePath }: BrokerAccountsConfig): Routes {
  return [
    {
      path: 'demo/:accountId',
      loadComponent: () =>
        import('./account-detail/account-detail.component').then((m) => m.AccountDetailComponent),
      data: { broker, basePath },
    },
    {
      path: 'demo',
      loadComponent: () =>
        import('./accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
      data: { accountType: 0, broker, basePath, title: `${broker} — Cuentas Demo` },
    },
    {
      path: 'live',
      loadComponent: () =>
        import('./accounts-list/accounts-list.component').then((m) => m.AccountsListComponent),
      data: { accountType: 1, broker, basePath, title: `${broker} — Cuentas Live` },
    },
    // Portfolios scoped to this broker's accounts (e.g. /darwinex/portfolios).
    { path: 'portfolios', children: portfolioRoutes(broker, `${basePath}/portfolios`) },
    { path: '', redirectTo: 'demo', pathMatch: 'full' },
  ];
}
