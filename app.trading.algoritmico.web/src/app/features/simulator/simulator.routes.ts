import { Routes } from '@angular/router';

export const SIMULATOR_ROUTES: Routes = [
  { path: '', redirectTo: 'ftmo', pathMatch: 'full' },
  {
    path: 'ftmo',
    loadComponent: () =>
      import('./ftmo-group-simulation-page/ftmo-group-simulation-page.component').then(
        (m) => m.FtmoGroupSimulationPageComponent,
      ),
  },
  {
    path: 'ftmo/search',
    loadComponent: () =>
      import('./ftmo-group-search-page/ftmo-group-search-page.component').then(
        (m) => m.FtmoGroupSearchPageComponent,
      ),
  },
];
