import { Route } from '@angular/router';
import { routes } from '../../app.routes';
import { authGuard } from '../../core/guards/auth.guard';
import { SIMULATOR_ROUTES } from './simulator.routes';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page/ftmo-group-simulation-page.component';

/** The guarded layout route: the one with an empty path that hosts the application shell. */
function layoutRoute(): Route {
  const layout = routes.find((r) => r.path === '' && r.canActivate?.includes(authGuard));
  if (!layout) throw new Error('guarded layout route not found');
  return layout;
}

describe('simulator routes', () => {
  it('theEmptyPath_RedirectsToFtmo', () => {
    const empty = SIMULATOR_ROUTES.find((r) => r.path === '');
    expect(empty?.redirectTo).toBe('ftmo');
    expect(empty?.pathMatch).toBe('full');
  });

  it('theFtmoPath_LazyLoadsTheGroupSimulationPage', async () => {
    const ftmo = SIMULATOR_ROUTES.find((r) => r.path === 'ftmo');
    expect(ftmo?.loadComponent).toBeDefined();
    const component = await (ftmo!.loadComponent as () => Promise<unknown>)();
    expect(component).toBe(FtmoGroupSimulationPageComponent);
  });

  it('appRoutes_HaveATopLevelSimulatorPathInsideTheGuardedLayout', async () => {
    const simulator = layoutRoute().children?.find((r) => r.path === 'simulator');
    expect(simulator?.loadChildren).toBeDefined();
    const loaded = await (simulator!.loadChildren as () => Promise<unknown>)();
    expect(loaded).toBe(SIMULATOR_ROUTES);
  });

  it('theSimulatorPath_IsNotNestedUnderTheFtmoOrBrokerRoutes_NorDeclaredOutsideTheLayout', () => {
    const ftmo = layoutRoute().children?.find((r) => r.path === 'ftmo');
    expect(ftmo?.children?.some((r) => r.path === 'simulator') ?? false).toBe(false);
    expect(routes.some((r) => r.path === 'simulator')).toBe(false);
  });
});
