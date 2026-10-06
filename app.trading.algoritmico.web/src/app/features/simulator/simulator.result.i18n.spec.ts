import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import {
  FtmoGroupCandidatesDto,
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
} from '../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationRefusal } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { FtmoSimulationService } from '../../core/services/ftmo-simulation.service';
import {
  TradingAccountDto,
  TradingAccountService,
} from '../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page/ftmo-group-simulation-page.component';
import {
  THREE_MEMBERS,
  coverage,
  diagnosticsFixture,
  groupResult,
  groupWideRefusal,
  memberRefusal,
  refusedKind,
  successKind,
} from './ftmo-group-simulation.result.fixtures';
import en from '../../../../public/assets/i18n/en.json';
import es from '../../../../public/assets/i18n/es.json';

/**
 * F3.5 sweep: the whole group page rendered with the REAL dictionaries, in EN and ES, in every state
 * (before a run, group refusal, kind refusal, success). Nothing may show a `{{` placeholder or a raw key.
 */

const DEPLOY = BacktestRunKind.Deploy;
const EVAL = BacktestRunKind.Evaluation;

const CANDIDATES: FtmoGroupCandidatesDto = {
  tradingAccountId: 'acc',
  maxMembers: 4,
  candidates: ['a', 'b'].map((id) => ({
    strategyId: id,
    name: `Name ${id}`,
    symbol: 'EURUSD',
    deploy: null,
    evaluation: null,
    nameExistsOnOtherAccount: false,
  })),
};

function page(
  lang: 'en' | 'es',
  dto: FtmoGroupSimulationDto | null,
): ComponentFixture<FtmoGroupSimulationPageComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
    providers: [
      {
        provide: TradingAccountService,
        useValue: { getAll: () => of([{ id: 'acc', name: 'SBDEMO2' } as TradingAccountDto]) },
      },
      {
        provide: FtmoSimulationService,
        useValue: {
          getGroupCandidates: () => of(CANDIDATES),
          simulateGroup: (): Observable<FtmoGroupSimulationDto> =>
            of(dto as FtmoGroupSimulationDto),
        },
      },
    ],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupSimulationPageComponent);
  fixture.detectChanges();
  if (dto !== null) {
    fixture.componentInstance.onSelectionChange(new Set(['a', 'b']));
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const risk = root.querySelector<HTMLInputElement>('input[name="targetRiskPerTrade"]')!;
    risk.value = '25';
    risk.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();
  }
  return fixture;
}

/** A dotted, upper-case token (`SIMULATOR.FTMO_GROUP.X`, `FTMO_SIMULATION.REFUSAL.Y`) is a raw key. */
const RAW_KEY = /\b[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)+\b/;

const STATES: Record<string, FtmoGroupSimulationDto | null> = {
  beforeRun: null,
  groupRefusalInvalidRequest: groupWideRefusal(FtmoGroupRefusal.InvalidRequest),
  groupRefusalShared: groupWideRefusal(FtmoGroupRefusal.SharedInputsRefused, {
    sharedRefusal: FtmoSimulationRefusal.LimitsNotConfigured,
  }),
  groupRefusalNotFound: groupWideRefusal(FtmoGroupRefusal.MemberNotFound, {
    unknownStrategyIds: ['u-1'],
  }),
  groupRefusalZones: groupWideRefusal(FtmoGroupRefusal.MixedSourceTimeZones),
  groupRefusalUnknown: groupWideRefusal(99 as FtmoGroupRefusal),
  kindRefusal: groupResult([
    successKind(DEPLOY),
    refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [
      memberRefusal(
        'a',
        'Alpha',
        FtmoGroupRefusal.MemberRunRefused,
        FtmoSimulationRefusal.FxRateNotDeclared,
      ),
      memberRefusal('b', 'Beta', FtmoGroupRefusal.MemberMissingKind),
    ]),
  ]),
  memberRunRefusedUnknownInnerReason: groupResult([
    successKind(DEPLOY),
    refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [
      memberRefusal('a', 'Alpha', FtmoGroupRefusal.MemberRunRefused, 99 as FtmoSimulationRefusal),
    ]),
  ]),
  memberRunRefusedNullRunReason: groupResult([
    successKind(DEPLOY),
    refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [
      memberRefusal('a', 'Alpha', FtmoGroupRefusal.MemberRunRefused, null),
    ]),
  ]),
  unknownPerMemberReason: groupResult([
    successKind(DEPLOY),
    refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [
      memberRefusal('a', 'Alpha', 99 as FtmoGroupRefusal),
    ]),
  ]),
  noCommonWindow: groupResult([
    refusedKind(
      DEPLOY,
      FtmoGroupRefusal.NoCommonWindow,
      [],
      [coverage('a', 'Alpha'), coverage('b', 'Beta')],
    ),
    refusedKind(EVAL, FtmoGroupRefusal.MemberHasNoTradesInWindow, [
      memberRefusal('b', 'Beta', FtmoGroupRefusal.MemberHasNoTradesInWindow),
    ]),
  ]),
  success: groupResult([successKind(DEPLOY), successKind(EVAL)], {
    duplicateNameWarnings: [{ name: 'Alpha', strategyIds: ['a', 'a2'] }],
  }),
  successWithDiagnostics: groupResult(
    [
      successKind(DEPLOY, diagnosticsFixture()),
      successKind(EVAL, {
        ...diagnosticsFixture(),
        peak: { peakConcurrentOpen: 0, firstReachedSource: null, memberIdsAtPeak: [] },
      }),
    ],
    { members: THREE_MEMBERS },
  ),
  successWithUnattributedStarts: groupResult(
    [
      successKind(DEPLOY, {
        ...diagnosticsFixture(),
        attribution: { ...diagnosticsFixture().attribution, unattributedStarts: 2 },
      }),
    ],
    { members: THREE_MEMBERS },
  ),
  successShortenedWindow: groupResult([
    {
      ...successKind(DEPLOY),
      coverage: [
        coverage('a', 'Alpha', { firstOpen: '2019-01-01T00:00:00' }),
        coverage('b', 'Beta', { firstOpen: null, lastClose: null, inWindowTrades: 0 }),
      ],
    },
    successKind(EVAL),
  ]),
};

/**
 * Each (locale, state) page is rendered once per file: the per-state sweep and the cross-locale
 * comparison share the cached text instead of building the whole component tree twice.
 */
const renderedText = new Map<string, string>();
function textOf(lang: 'en' | 'es', name: string): string {
  const key = `${lang}:${name}`;
  let text = renderedText.get(key);
  if (text === undefined) {
    text = (page(lang, STATES[name]).nativeElement as HTMLElement).textContent ?? '';
    renderedText.set(key, text);
  }
  return text;
}

/**
 * Rendering ~17 full pages per locale costs ~1s in isolation but several times that when the whole
 * suite saturates the CPU, so the comparison test gets a larger budget than the 5 s default.
 */
const CROSS_LOCALE_TIMEOUT_MS = 30_000;

describe('group page real-dictionary sweep (F3.5.1)', () => {
  for (const lang of ['en', 'es'] as const) {
    for (const name of Object.keys(STATES)) {
      it(`${lang}_${name}_hasNoPlaceholderAndNoRawKey`, () => {
        const text = textOf(lang, name);
        expect(text.length).toBeGreaterThan(0);
        expect(text).not.toContain('{{');
        expect(text).not.toContain('}}');
        expect(RAW_KEY.exec(text)?.[0] ?? null).toBeNull();
      });
    }
  }

  it('theRawKeyDetectorSeesAKeyButNotOrdinaryText', () => {
    expect(RAW_KEY.test('x SIMULATOR.FTMO_GROUP.RESULT.WINDOW y')).toBe(true);
    expect(
      RAW_KEY.test('Replay window: 2020-02-01 to 2021-06-30 (Europe/Berlin) Clean/Contingent'),
    ).toBe(false);
  });

  it(
    'theTwoLocalesRenderDifferentTextInEveryState',
    () => {
      for (const name of Object.keys(STATES)) {
        expect(textOf('en', name), name).not.toBe(textOf('es', name));
      }
    },
    CROSS_LOCALE_TIMEOUT_MS,
  );

  it('everyResultKeyHasADistinctRefusalTextInBothLocales', () => {
    const refusals = (l: Record<string, unknown>): string[] =>
      Object.values(
        (l['SIMULATOR'] as { FTMO_GROUP: { RESULT: { REFUSAL: Record<string, string> } } })
          .FTMO_GROUP.RESULT.REFUSAL,
      );
    for (const locale of [en, es] as Record<string, unknown>[]) {
      const texts = refusals(locale);
      expect(texts).toHaveLength(8);
      expect(new Set(texts).size).toBe(8);
    }
  });
});
