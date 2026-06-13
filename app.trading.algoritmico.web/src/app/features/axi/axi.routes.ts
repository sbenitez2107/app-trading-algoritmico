import { Routes } from '@angular/router';
import { brokerAccountsRoutes } from '../broker-accounts/broker-accounts.routes';

export const AXI_ROUTES: Routes = brokerAccountsRoutes({
  broker: 'Axi',
  basePath: '/axi',
});
