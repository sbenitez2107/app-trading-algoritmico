import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { FtmoGroupSearchService } from '../../core/services/ftmo-group-search.service';
import { FtmoSimulationService } from '../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../core/services/trading-account.service';
import { FtmoGroupSearchStatus } from '../../core/models/ftmo-group-search.model';
import { searchJob } from './ftmo-group-search.fixtures';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page/ftmo-group-search-page.component';

/** Colour literals, fallbacks included: hex, rgb(a), hsl(a). */
const COLOUR_LITERAL = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/;

const VARIABLES_FILE = '/src/styles/_variables.scss';

/** Reads the theme file from disk (same non-literal `node:fs` technique as `simulator.theme.spec.ts`). */
async function readThemeFile(): Promise<string> {
  const specifier = 'node:fs';
  const fs = (await import(/* @vite-ignore */ specifier)) as {
    readFileSync: (path: string, encoding: 'utf8') => string;
  };
  const proc = (globalThis as unknown as { process: { cwd: () => string } }).process;
  return fs.readFileSync(proc.cwd() + VARIABLES_FILE, 'utf8');
}

function parseDeclaredVariables(scss: string): Set<string> {
  return new Set(Array.from(scss.matchAll(/^\s*(--[\w-]+)\s*:/gm)).map((m) => m[1]));
}

function readVariables(css: string): string[] {
  return Array.from(css.matchAll(/var\(\s*(--[\w-]+)/g)).map((m) => m[1]);
}

function injectedCss(): string {
  return Array.from(document.head.querySelectorAll('style'))
    .map((s) => s.textContent ?? '')
    .join('\n');
}

/** Only the rules that belong to the search components. */
function searchCss(): string {
  return injectedCss()
    .split('}')
    .filter((rule) => /ftmo-search/.test(rule))
    .join('}\n');
}

describe('ftmo group search theming', () => {
  let declared: Set<string>;

  beforeAll(async () => {
    declared = parseDeclaredVariables(await readThemeFile());
  });

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSearchPageComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: FtmoGroupSearchService,
          useValue: {
            getCurrentGroupSearch: () => of(searchJob({ status: FtmoGroupSearchStatus.Completed })),
            getGroupSearch: () => of(searchJob()),
          },
        },
        {
          provide: TradingAccountService,
          useValue: { getAll: () => of([{ id: 'a', name: 'A' }]) },
        },
        {
          provide: FtmoSimulationService,
          useValue: {
            getGroupCandidates: () => of({ tradingAccountId: 'a', maxMembers: 4, candidates: [] }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(FtmoGroupSearchPageComponent);
    fixture.detectChanges();
  });

  it('theCompiledSearchCssIsPresentInTheTestDocument', () => {
    expect(searchCss()).toContain('.ftmo-search-page');
  });

  it('theFormAndProgressStylesheetsAreCompiledAndChecked', () => {
    const css = searchCss();
    expect(css).toContain('.ftmo-search-form');
    expect(css).toContain('.ftmo-search-progress');
  });

  it('theTableStylesheetIsCompiled_AndScrollsInsideItsOwnContainer', () => {
    const css = searchCss();
    expect(css).toContain('.ftmo-search-table');
    expect(css).toMatch(/\.ftmo-search-table__scroll[^{]*\{[^}]*overflow-x:\s*auto/);
    expect(css).toMatch(/\.ftmo-search-table__table[^{]*\{[^}]*min-width:/);
  });

  it('theScatterStylesheetIsCompiled_AndChecked', () => {
    const css = searchCss();
    expect(css).toContain('.ftmo-search-scatter');
    expect(css).toMatch(/\.ftmo-search-scatter__point--frontier[^{]*\{[^}]*fill:\s*var\(--/);
    expect(css).toMatch(/\.ftmo-search-scatter__point--within[^{]*\{[^}]*stroke:\s*var\(--/);
  });

  it('theScatterUsesNeutralAccents_NoGainOrLossColours', () => {
    const scatter = searchCss()
      .split('}')
      .filter((rule) => /ftmo-search-scatter/.test(rule))
      .join('}');
    expect(scatter).not.toMatch(/--color-(gain|loss)/);
    expect(scatter).toMatch(/\.ftmo-search-scatter__point--within[^{]*\{[^}]*stroke-dasharray/);
  });

  it('noSearchStylesheetContainsAColourLiteral_FallbacksIncluded', () => {
    expect(COLOUR_LITERAL.test(searchCss())).toBe(false);
  });

  it('everyVariableTheSearchStylesheetReadsIsDeclaredInTheThemeFile', () => {
    const used = readVariables(searchCss());
    expect(used.length).toBeGreaterThan(0);
    expect(used.filter((name) => !declared.has(name))).toEqual([]);
  });

  it('falsification_TheDetectorsFlagAColourAndAnUndeclaredVariable', () => {
    expect(COLOUR_LITERAL.test('.ftmo-search-page { background: #fff; }')).toBe(true);
    expect(
      readVariables('.x { color: var(--not-declared); }').filter((n) => !declared.has(n)),
    ).toEqual(['--not-declared']);
  });
});
