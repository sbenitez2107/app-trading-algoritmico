import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FtmoSimulationModalComponent } from './ftmo-simulation-modal.component';
import { FtmoRunPanelComponent } from './ftmo-run-panel/ftmo-run-panel.component';
import { FtmoOutcomeBarsComponent } from './ftmo-outcome-bars/ftmo-outcome-bars.component';
import { FtmoOrderStatsTableComponent } from './ftmo-order-stats-table/ftmo-order-stats-table.component';
import { API_BASE_URL } from '../../../app.config';

/**
 * Names the modal shell used originally. None of them is declared in `styles/_variables.scss`, so every
 * `var(--color-surface, #1e1e2e)` silently resolved to its hardcoded dark fallback in BOTH themes while
 * the panels (which read the real `--bg-surface`) followed the theme: white panels in a dark modal.
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

/** True when the stylesheet reads `var(<name>` — the variable name must match exactly. */
function readsVariable(css: string, name: string): boolean {
  return css.includes(`var(${name},`) || css.includes(`var(${name})`);
}

/** The compiled CSS Angular injects into `document.head` for the components rendered so far. */
function injectedCss(): string {
  return Array.from(document.head.querySelectorAll('style'))
    .map((s) => s.textContent ?? '')
    .join('\n');
}

/** Only the rules that belong to the FTMO components (their emulated-encapsulation attribute). */
function ftmoCss(): string {
  return injectedCss()
    .split('}')
    .filter((rule) => /ftmo-(sim|run-panel|outcome|order-stats)/.test(rule))
    .join('}\n');
}

describe('FTMO simulation theming (post-PR1 fix 3)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [
        FtmoSimulationModalComponent,
        FtmoRunPanelComponent,
        FtmoOutcomeBarsComponent,
        FtmoOrderStatsTableComponent,
        TranslateModule.forRoot(),
        HttpClientTestingModule,
      ],
      providers: [{ provide: API_BASE_URL, useValue: 'http://localhost/api-test' }],
    });
    const modal = TestBed.createComponent(FtmoSimulationModalComponent);
    modal.componentRef.setInput('strategyId', 's1');
    modal.componentRef.setInput('strategyName', 'S');
    modal.componentRef.setInput('symbol', 'EURUSD');
    modal.detectChanges();
    // The panels only inject their styles once rendered, so render each presentational component.
    const panel = TestBed.createComponent(FtmoRunPanelComponent);
    panel.componentRef.setInput('vm', {
      kind: 0,
      disclosure: 'd',
      notModelled: [],
      monthsWithoutStart: [],
      monthsWithoutStartCount: 0,
      start1Differs: false,
      fxLow: null,
      fxHigh: null,
      unscalableCount: 0,
      state: 'noStarts',
    });
    panel.detectChanges();
    const bars = TestBed.createComponent(FtmoOutcomeBarsComponent);
    bars.componentRef.setInput('rows', []);
    bars.detectChanges();
    const stats = TestBed.createComponent(FtmoOrderStatsTableComponent);
    stats.componentRef.setInput('rows', []);
    stats.detectChanges();
  });

  it('theCompiledFtmoCss_IsPresentInTheTestDocument', () => {
    expect(ftmoCss()).toContain('.ftmo-sim-modal');
    expect(ftmoCss()).toContain('.ftmo-run-panel');
  });

  it('theModalShell_PaintsWithTheThemeSurfaceVariables_SoItFollowsLightAndDark', () => {
    const modalRule = ftmoCss()
      .split('}')
      .find((r) => r.includes('.ftmo-sim-modal[') || /\.ftmo-sim-modal\s*\[/.test(r));
    expect(modalRule).toBeDefined();
    expect(modalRule).toContain('var(--bg-surface');
    expect(modalRule).toContain('var(--border-color');
  });

  it('noFtmoStylesheet_ReadsAnUndeclaredLegacyVariable', () => {
    const css = ftmoCss();
    const offenders = UNDECLARED_LEGACY_VARIABLES.filter((name) => readsVariable(css, name));
    expect(offenders).toEqual([]);
  });

  it('falsification_TheDetectorFlagsTheOriginalModalDeclaration', () => {
    const original = '.ftmo-sim-modal { background: var(--color-surface, #1e1e2e); }';
    const offenders = UNDECLARED_LEGACY_VARIABLES.filter((name) => readsVariable(original, name));
    expect(offenders).toEqual(['--color-surface']);
  });
});
