import { Routes } from '@angular/router';

/**
 * Portfolio routes for a single platform/broker. Mounted under each broker (e.g. /darwinex/portfolios),
 * so a portfolio is scoped to that broker's accounts. `broker` drives filtering + candidate scoping;
 * `portfoliosBase` is the absolute base path the components use for internal navigation.
 */
export function portfolioRoutes(broker: string, portfoliosBase: string): Routes {
  const data = { broker, portfoliosBase };
  return [
    {
      path: 'new',
      loadComponent: () =>
        import('./portfolio-builder/portfolio-builder.component').then(
          (m) => m.PortfolioBuilderComponent,
        ),
      data,
    },
    {
      path: ':id',
      loadComponent: () =>
        import('./portfolio-detail/portfolio-detail.component').then(
          (m) => m.PortfolioDetailComponent,
        ),
      data,
    },
    {
      path: '',
      loadComponent: () =>
        import('./portfolios-list/portfolios-list.component').then(
          (m) => m.PortfoliosListComponent,
        ),
      data,
    },
  ];
}
