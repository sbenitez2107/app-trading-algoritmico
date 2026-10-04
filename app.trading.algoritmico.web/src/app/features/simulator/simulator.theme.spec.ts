import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { FtmoSimulationService } from '../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page/ftmo-group-simulation-page.component';

/**
 * Names the FTMO modal shell used originally. None is declared in `styles/_variables.scss`, so every
 * `var(--color-surface, #1e1e2e)` silently resolved to its hardcoded fallback in BOTH themes.
 */
const UNDECLARED_LEGACY_VARIABLES = [
  '--color-surface',
  '--color-surface-alt',
  '--color-surface-2',
  '--color-border',
  '--color-text-primary',
  '--color-text-secondary',
  '--color-accent',
  '--color-error',
];

/** Colour literals, fallbacks included: hex, rgb(a), hsl(a). */
const COLOUR_LITERAL = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/;

/**
 * Variables declared in `src/styles/_variables.scss` (`:root` and `[data-theme="dark"]`), read from the
 * file on disk so the set can never drift from it. `node:fs` is imported through a non-literal
 * specifier because the test builder has no node typings and no scss text loader; the spec runs in
 * node, with the web project directory as the working directory.
 */
const VARIABLES_FILE = '/src/styles/_variables.scss';

async function readThemeFile(): Promise<string> {
  const specifier = 'node:fs';
  const fs = (await import(/* @vite-ignore */ specifier)) as {
    readFileSync: (path: string, encoding: 'utf8') => string;
  };
  const proc = (globalThis as unknown as { process: { cwd: () => string } }).process;
  return fs.readFileSync(proc.cwd() + VARIABLES_FILE, 'utf8');
}

/** Custom-property declarations (`--name: value;`), as opposed to `var(--name)` reads. */
function parseDeclaredVariables(scss: string): Set<string> {
  return new Set(Array.from(scss.matchAll(/^\s*(--[\w-]+)\s*:/gm)).map((m) => m[1]));
}

let DECLARED: Set<string>;

function declaredVariables(): Set<string> {
  return DECLARED;
}

function readVariables(css: string): string[] {
  return Array.from(css.matchAll(/var\(\s*(--[\w-]+)/g)).map((m) => m[1]);
}

function readsVariable(css: string, name: string): boolean {
  return readVariables(css).includes(name);
}

/** The compiled CSS Angular injects into `document.head` for the components rendered so far. */
function injectedCss(): string {
  return Array.from(document.head.querySelectorAll('style'))
    .map((s) => s.textContent ?? '')
    .join('\n');
}

/** Only the rules that belong to the simulator components. */
function simulatorCss(): string {
  return injectedCss()
    .split('}')
    .filter((rule) => /simulator-page|ftmo-group/.test(rule))
    .join('}\n');
}

describe('simulator theming', () => {
  beforeAll(async () => {
    DECLARED = parseDeclaredVariables(await readThemeFile());
  });

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: TradingAccountService,
          useValue: { getAll: () => of([{ id: 'a', name: 'SBDEMO2' }]) },
        },
        {
          provide: FtmoSimulationService,
          useValue: {
            getGroupCandidates: () =>
              of({
                tradingAccountId: 'a',
                maxMembers: 1,
                candidates: [
                  {
                    strategyId: 's',
                    name: 'S',
                    symbol: null,
                    deploy: null,
                    evaluation: null,
                    nameExistsOnOtherAccount: false,
                  },
                ],
              }),
          },
        },
      ],
    });
    TestBed.createComponent(FtmoGroupSimulationPageComponent).detectChanges();
  });

  it('theCompiledSimulatorCss_IsPresentInTheTestDocument', () => {
    expect(simulatorCss()).toContain('.ftmo-group-page');
  });

  it('theDeclaredSetIsParsedFromTheThemeFileOnDisk', () => {
    const declared = declaredVariables();
    expect(declared.size).toBeGreaterThan(30);
    expect(declared.has('--bg-surface')).toBe(true);
    expect(declared.has('--color-warning')).toBe(true);
  });

  it('thePickerStylesheetIsPartOfTheCheckedCss', () => {
    expect(simulatorCss()).toContain('.ftmo-group-picker');
  });

  it('noSimulatorStylesheet_ContainsAColourLiteral_FallbacksIncluded', () => {
    expect(COLOUR_LITERAL.test(simulatorCss())).toBe(false);
  });

  it('everyVariableTheSimulatorStylesheetReads_IsDeclaredInTheThemeFile', () => {
    const declared = declaredVariables();
    const used = readVariables(simulatorCss());
    expect(used.length).toBeGreaterThan(0);
    expect(used.filter((name) => !declared.has(name))).toEqual([]);
  });

  it('noSimulatorStylesheet_ReadsAnUndeclaredLegacyVariable', () => {
    const css = simulatorCss();
    expect(UNDECLARED_LEGACY_VARIABLES.filter((name) => readsVariable(css, name))).toEqual([]);
  });

  it('falsification_TheParserSeesDeclarationsButNotReads', () => {
    const scss = ['.root {', '  --a-one: 1px;', '}', '.x { color: var(--not-declared); }'].join(
      '\n',
    );
    expect([...parseDeclaredVariables(scss)]).toEqual(['--a-one']);
  });

  it('falsification_TheDetectorsFlagAHardcodedColourAndAnUndeclaredVariable', () => {
    expect(COLOUR_LITERAL.test('.x { background: #fff; }')).toBe(true);
    expect(COLOUR_LITERAL.test('.x { color: var(--text-main, rgb(0, 0, 0)); }')).toBe(true);
    const original = '.x { background: var(--color-surface, #1e1e2e); }';
    expect(UNDECLARED_LEGACY_VARIABLES.filter((n) => readsVariable(original, n))).toEqual([
      '--color-surface',
    ]);
    expect(readVariables(original).filter((n) => !declaredVariables().has(n))).toEqual([
      '--color-surface',
    ]);
  });
});
