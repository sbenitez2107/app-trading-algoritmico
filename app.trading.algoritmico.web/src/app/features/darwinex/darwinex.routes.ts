import { Routes } from '@angular/router';
import { brokerAccountsRoutes } from '../broker-accounts/broker-accounts.routes';

export const DARWINEX_ROUTES: Routes = brokerAccountsRoutes({
  broker: 'Darwinex',
  basePath: '/darwinex',
});
