import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
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
 * Variables declared in `src/styles/_variables.scss` (`:root` and `[data-theme="dark"]`). The test
 * builder cannot import the stylesheet's source (no scss text loader) and does not inject the global
 * stylesheet, so the names are mirrored here. A guard test below keeps the mirror honest in the one
 * direction it can check: every variable a simulator component reads must be in this list.
 */
const DECLARED_THEME_VARIABLES = new Set([
  '--bg-app',
  '--bg-login-header',
  '--bg-sidebar',
  '--bg-sidebar-active',
  '--bg-sidebar-hover',
  '--bg-surface',
  '--bg-surface-2',
  '--border-color',
  '--color-gain',
  '--color-loss',
  '--color-neutral',
  '--color-primary',
  '--color-primary-dark',
  '--color-warning',
  '--radius-lg',
  '--radius-md',
  '--radius-sm',
  '--radius-xl',
  '--select-chevron',
  '--shadow-lg',
  '--shadow-md',
  '--shadow-sm',
  '--spacing-lg',
  '--spacing-md',
  '--spacing-sm',
  '--spacing-xl',
  '--spacing-xs',
  '--text-main',
  '--text-muted',
  '--text-sidebar',
  '--text-sidebar-muted',
  '--transition-base',
  '--transition-fast',
]);

function declaredVariables(): Set<string> {
  return DECLARED_THEME_VARIABLES;
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
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
    });
    TestBed.createComponent(FtmoGroupSimulationPageComponent).detectChanges();
  });

  it('theCompiledSimulatorCss_IsPresentInTheTestDocument', () => {
    expect(simulatorCss()).toContain('.ftmo-group-page');
  });

  it('theDeclaredVariableSet_ExcludesTheLegacyUndeclaredNames', () => {
    const declared = declaredVariables();
    expect(declared.has('--bg-surface')).toBe(true);
    expect(UNDECLARED_LEGACY_VARIABLES.filter((name) => declared.has(name))).toEqual([]);
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
