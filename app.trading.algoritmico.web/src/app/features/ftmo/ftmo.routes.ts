import { Routes } from '@angular/router';
import { brokerAccountsRoutes } from '../broker-accounts/broker-accounts.routes';

export const FTMO_ROUTES: Routes = brokerAccountsRoutes({
  broker: 'FTMO',
  basePath: '/ftmo',
});
