import { SIMULATOR_ROUTES } from './simulator.routes';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page/ftmo-group-simulation-page.component';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page/ftmo-group-search-page.component';

/**
 * The first dynamic import of a lazy route chunk pays the module transform/load cost (the search page
 * pulls a large component tree), which can exceed the 5 s default when the full suite saturates the CPU.
 * Resolve every loader once up front with a generous hook budget so the tests only assert identity.
 */
const LAZY_LOAD_WARMUP_MS = 60_000;

describe('simulator search route', () => {
  beforeAll(async () => {
    await Promise.all(
      SIMULATOR_ROUTES.filter((r) => r.loadComponent).map((r) =>
        (r.loadComponent as () => Promise<unknown>)(),
      ),
    );
  }, LAZY_LOAD_WARMUP_MS);

  it('theFtmoSearchPath_LazyLoadsTheSearchPage', async () => {
    const search = SIMULATOR_ROUTES.find((r) => r.path === 'ftmo/search');
    expect(search?.loadComponent).toBeDefined();
    const component = await (search!.loadComponent as () => Promise<unknown>)();
    expect(component).toBe(FtmoGroupSearchPageComponent);
  });

  it('theExistingFtmoEntryIsUnchanged', async () => {
    const ftmo = SIMULATOR_ROUTES.find((r) => r.path === 'ftmo');
    const component = await (ftmo!.loadComponent as () => Promise<unknown>)();
    expect(component).toBe(FtmoGroupSimulationPageComponent);
    expect(SIMULATOR_ROUTES.filter((r) => r.path === 'ftmo')).toHaveLength(1);
  });

  it('theEmptyPathStillRedirectsToTheGroupPage', () => {
    const empty = SIMULATOR_ROUTES.find((r) => r.path === '');
    expect(empty?.redirectTo).toBe('ftmo');
  });
});
