import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it, vi } from 'vitest';
import {
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
} from '../../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationRefusal } from '../../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import {
  THREE_MEMBERS,
  coverage,
  diagnosticsFixture,
  groupResult,
  groupWideRefusal,
  MEMBERS,
  memberRefusal,
  refusedKind,
  successKind,
} from '../ftmo-group-simulation.result.fixtures';
import { toGroupResultVm } from '../ftmo-group-simulation.result.mappers';
import { FtmoGroupResultComponent } from './ftmo-group-result.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

const DEPLOY = BacktestRunKind.Deploy;
const EVAL = BacktestRunKind.Evaluation;

interface Rendered {
  fixture: ComponentFixture<FtmoGroupResultComponent>;
  el: HTMLElement;
  text: string;
  t: (key: string, params?: Record<string, unknown>) => string;
}

function render(
  dto: FtmoGroupSimulationDto,
  lang: 'en' | 'es' = 'en',
  broker: string | null = 'FTMO',
): Rendered {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupResultComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupResultComponent);
  fixture.componentRef.setInput('vm', toGroupResultVm(dto));
  fixture.componentRef.setInput('broker', broker);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    el,
    text: el.textContent ?? '',
    t: (key, params) => translate.instant(key, params) as string,
  };
}

function occurrences(haystack: string, needle: string): number {
  return haystack.split(needle).length - 1;
}

const RESULT = 'SIMULATOR.FTMO_GROUP.RESULT.';

describe('FtmoGroupResultComponent panels (F3.4.1)', () => {
  it('rendersDeployAndEvaluationAsTwoSeparatelyLabelledPanels_InOrder', () => {
    const r = render(groupResult([successKind(EVAL), successKind(DEPLOY)]));
    const panels = Array.from(r.el.querySelectorAll('app-ftmo-run-panel .ftmo-run-panel'));
    expect(panels.map((p) => p.getAttribute('aria-label'))).toEqual([
      r.t('FTMO_SIMULATION.KIND.DEPLOY'),
      r.t('FTMO_SIMULATION.KIND.EVALUATION'),
    ]);
    expect(r.el.querySelector('.ftmo-group-result__group-refusal')).toBeNull();
  });

  it('neverRendersTheStart1Note_ForAGroup', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(groupResult([successKind(DEPLOY), successKind(EVAL)]), lang);
      expect(r.el.querySelector('.ftmo-run-panel__start1-differs'), lang).toBeNull();
      expect(r.text, lang).not.toContain(r.t('FTMO_SIMULATION.START1_DIFFERS_DISCLOSURE'));
    }
  });

  it('aRefusedKindShowsItsRefusalInItsSlot_WhileTheOtherKindStillRenders', () => {
    const r = render(
      groupResult([
        successKind(DEPLOY),
        refusedKind(EVAL, FtmoGroupRefusal.MemberMissingKind, [
          memberRefusal('b', 'Beta', FtmoGroupRefusal.MemberMissingKind),
        ]),
      ]),
    );
    expect(r.el.querySelectorAll('app-ftmo-run-panel')).toHaveLength(1);
    const refused = r.el.querySelector('[data-kind="2"]') as HTMLElement;
    expect(refused.textContent).toContain(r.t('FTMO_SIMULATION.KIND.EVALUATION'));
    expect(refused.textContent).toContain('Beta');
    expect(refused.querySelector('.ftmo-run-panel')).toBeNull();
    expect(refused.querySelector('.ftmo-outcome-bars')).toBeNull();
  });

  it('anOmittedKindShowsTheNoRunMarker', () => {
    const r = render(groupResult([successKind(DEPLOY)]));
    const slot = r.el.querySelector('[data-kind="2"]') as HTMLElement;
    expect(slot.textContent).toContain(r.t('FTMO_SIMULATION.NO_RUN_HELD'));
  });
});

describe('per-member reasons (F3.4.2)', () => {
  const REASONS = [
    FtmoSimulationRefusal.RiskNotEstimable,
    FtmoSimulationRefusal.PointValueNotCalibrated,
    FtmoSimulationRefusal.InstrumentSpecMissing,
    FtmoSimulationRefusal.FxRateNotDeclared,
    FtmoSimulationRefusal.InvalidFxBand,
  ];

  it('listsEachMemberByNameWithItsOwnDistinctTranslatedReason_InEnAndEs', () => {
    for (const lang of ['en', 'es'] as const) {
      const members = [
        ...REASONS.map((reason, i) =>
          memberRefusal(`m${i}`, `Member${i}`, FtmoGroupRefusal.MemberRunRefused, reason),
        ),
        memberRefusal('x', 'MemberX', FtmoGroupRefusal.MemberMissingKind),
        memberRefusal('y', 'MemberY', FtmoGroupRefusal.MemberHasNoTradesInWindow),
      ];
      const r = render(
        groupResult([
          refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, members),
          successKind(EVAL),
        ]),
        lang,
      );
      const rows = Array.from(r.el.querySelectorAll('[data-kind="1"] .ftmo-group-result__member'));
      expect(rows, lang).toHaveLength(7);
      const texts = rows.map((row) => (row.textContent ?? '').replace(/\s+/g, ' ').trim());
      expect(new Set(texts.map((t) => t.replace(/^Member\w+:\s*/, ''))).size, lang).toBe(7);
      texts.forEach((text, i) => {
        expect(text, lang).toContain(i < 5 ? `Member${i}` : i === 5 ? 'MemberX' : 'MemberY');
      });
    }
  });

  it('aSymbolLevelRefusalAppearsInBothSlots_AndTheMixedListShowsEachOwnReason', () => {
    const symbol = memberRefusal(
      'b',
      'Beta',
      FtmoGroupRefusal.MemberRunRefused,
      FtmoSimulationRefusal.InstrumentSpecMissing,
    );
    const other = memberRefusal(
      'c',
      'Gamma',
      FtmoGroupRefusal.MemberRunRefused,
      FtmoSimulationRefusal.RiskNotEstimable,
    );
    const r = render(
      groupResult([
        refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, [symbol, other]),
        refusedKind(EVAL, FtmoGroupRefusal.MemberRunRefused, [symbol]),
      ]),
    );
    const spec = r.t('FTMO_SIMULATION.REFUSAL.INSTRUMENT_SPEC_MISSING');
    const risk = r.t('FTMO_SIMULATION.REFUSAL.RISK_NOT_ESTIMABLE');
    const deploy = (r.el.querySelector('[data-kind="1"]') as HTMLElement).textContent ?? '';
    const evaluation = (r.el.querySelector('[data-kind="2"]') as HTMLElement).textContent ?? '';
    expect(deploy).toContain(spec);
    expect(deploy).toContain(risk);
    expect(evaluation).toContain(spec);
    expect(evaluation).not.toContain(risk);
  });

  it('theInnerReasonZeroRendersItsLabel_NotAMissingReason', () => {
    const r = render(
      groupResult([
        refusedKind(DEPLOY, FtmoGroupRefusal.MemberRunRefused, [
          memberRefusal(
            'a',
            'Alpha',
            FtmoGroupRefusal.MemberRunRefused,
            FtmoSimulationRefusal.InvalidRequest,
          ),
        ]),
      ]),
    );
    expect(r.text).toContain(r.t('FTMO_SIMULATION.REFUSAL.INVALID_REQUEST'));
  });

  it('noCommonWindowShowsEveryMembersCoverageAndBlamesNoOne', () => {
    const r = render(
      groupResult([
        refusedKind(
          DEPLOY,
          FtmoGroupRefusal.NoCommonWindow,
          [],
          [coverage('a', 'Alpha'), coverage('b', 'Beta')],
        ),
        successKind(EVAL),
      ]),
    );
    const slot = r.el.querySelector('[data-kind="1"]') as HTMLElement;
    expect(slot.textContent).toContain(r.t(`${RESULT}REFUSAL.NO_COMMON_WINDOW`));
    expect(slot.querySelectorAll('.ftmo-group-result__member')).toHaveLength(0);
    expect(slot.querySelectorAll('.ftmo-group-result__coverage-row')).toHaveLength(2);
  });
});

describe('F3b follow-ups (RELIABILITY-001/003)', () => {
  it('noWindowKindRendersADistinctNoWindowCount_NeverZeroTrades_InBothLocales', () => {
    for (const lang of ['en', 'es'] as const) {
      for (const refusal of [
        FtmoGroupRefusal.NoCommonWindow,
        FtmoGroupRefusal.MemberHasNoTradesInWindow,
      ]) {
        const r = render(
          groupResult([
            refusedKind(DEPLOY, refusal, [], [coverage('a', 'Alpha', { inWindowTrades: 0 })]),
          ]),
          lang,
        );
        const row = r.el.querySelector('.ftmo-group-result__coverage-row')!.textContent ?? '';
        expect(row, `${lang}/${refusal}`).toContain(
          r.t(`${RESULT}COVERAGE_ROW_NO_WINDOW`, {
            name: 'Alpha',
            first: '2020-02-01',
            last: '2021-06-30',
          }),
        );
        expect(row).toContain('2020-02-01');
        expect(row).not.toMatch(/\b0\b/);
      }
    }
  });

  it('aGenuineZeroInAnExistingWindowStillRendersZeroTrades', () => {
    const kind = successKind(DEPLOY);
    kind.coverage = [coverage('a', 'Alpha', { inWindowTrades: 0 })];
    const r = render(groupResult([kind]));
    const row = r.el.querySelector('.ftmo-group-result__coverage-row')!.textContent ?? '';
    expect(row).toContain(
      r.t(`${RESULT}COVERAGE_ROW`, {
        name: 'Alpha',
        first: '2020-02-01',
        last: '2021-06-30',
        trades: 0,
      }),
    );
  });

  it('twoMembersSharingANameBothRenderInTheZonesList_WithNoDuplicateTrackKeyWarning', () => {
    const twins = (first: string, second: string): FtmoGroupSimulationDto =>
      groupWideRefusal(FtmoGroupRefusal.MixedSourceTimeZones, {
        members: [
          { ...MEMBERS[0], strategyId: 'x1', name: 'Twin', sourceTimeZoneId: first },
          { ...MEMBERS[1], strategyId: 'x2', name: 'Twin', sourceTimeZoneId: second },
        ],
      });
    const r = render(twins('Europe/Berlin', 'America/New_York'));
    const rows = (): string[] =>
      Array.from(r.el.querySelectorAll('.ftmo-group-result__zones li')).map(
        (li) => li.textContent ?? '',
      );
    expect(rows()).toHaveLength(2);
    expect(rows()[0]).toContain('Europe/Berlin');
    expect(rows()[1]).toContain('America/New_York');

    // Angular only WARNS (NG0955) on a duplicate `track` key, and only when the list is reconciled, so
    // re-render with a changed list and treat the warning as a failure.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    r.fixture.componentRef.setInput('vm', toGroupResultVm(twins('Asia/Tokyo', 'Europe/London')));
    r.fixture.detectChanges();
    const logged = warn.mock.calls.map((c) => String(c[0]));
    warn.mockRestore();
    expect(rows()).toHaveLength(2);
    expect(rows()[0]).toContain('Asia/Tokyo');
    expect(rows()[1]).toContain('Europe/London');
    expect(logged.filter((m) => m.includes('NG0955'))).toEqual([]);
  });
});

describe('diagnostics panels (F4.3)', () => {
  it('rendersOneDiagnosticsPanelPerSuccessfulKind_AndNoneForARefusedKind', () => {
    const r = render(
      groupResult(
        [
          successKind(DEPLOY, diagnosticsFixture()),
          refusedKind(EVAL, FtmoGroupRefusal.NoCommonWindow),
        ],
        { members: THREE_MEMBERS },
      ),
    );
    expect(r.el.querySelectorAll('app-ftmo-group-diagnostics')).toHaveLength(1);
    const deploy = r.el.querySelector('[data-kind="1"]') as HTMLElement;
    expect(deploy.querySelectorAll('app-ftmo-group-diagnostics')).toHaveLength(1);
    const both = render(
      groupResult(
        [successKind(DEPLOY, diagnosticsFixture()), successKind(EVAL, diagnosticsFixture())],
        {
          members: THREE_MEMBERS,
        },
      ),
    );
    expect(both.el.querySelectorAll('app-ftmo-group-diagnostics')).toHaveLength(2);
  });

  it('rendersNoDiagnosticsPanelWhenTheKindCarriesNone_AndNoneBesideAGroupRefusal', () => {
    expect(
      render(groupResult([successKind(DEPLOY)])).el.querySelector('app-ftmo-group-diagnostics'),
    ).toBeNull();
    expect(
      render(groupWideRefusal(FtmoGroupRefusal.InvalidRequest)).el.querySelector(
        'app-ftmo-group-diagnostics',
      ),
    ).toBeNull();
  });
});

describe('group-wide refusal (F3.4.3)', () => {
  it('rendersOnceAboveTheSlots_WithNoPanelFindingsAndNoRunNotes', () => {
    const r = render(groupWideRefusal(FtmoGroupRefusal.MixedSourceTimeZones));
    expect(r.el.querySelectorAll('.ftmo-group-result__group-refusal')).toHaveLength(1);
    expect(r.el.querySelector('app-ftmo-run-panel')).toBeNull();
    expect(r.el.querySelector('.ftmo-group-result__slot')).toBeNull();
    expect(r.text).not.toContain(r.t('FTMO_SIMULATION.DISCLOSURE_RESULT'));
    expect(r.text).toContain('Europe/Berlin');
    expect(r.text).toContain('America/New_York');
    expect(r.text).toContain('Alpha');
  });

  it('invalidRequestValueZeroRendersItsLabel_NotTreatedAsAbsent', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(groupWideRefusal(FtmoGroupRefusal.InvalidRequest), lang);
      expect(r.el.querySelectorAll('.ftmo-group-result__group-refusal'), lang).toHaveLength(1);
      expect(r.text, lang).toContain(r.t(`${RESULT}REFUSAL.INVALID_REQUEST`));
    }
  });

  it('sharedInputsRefusedShowsItsInnerReason_WithTheBrokerFilled', () => {
    const r = render(
      groupWideRefusal(FtmoGroupRefusal.SharedInputsRefused, {
        sharedRefusal: FtmoSimulationRefusal.LimitsNotConfigured,
      }),
      'en',
      'MyBroker',
    );
    expect(r.text).toContain(r.t(`${RESULT}REFUSAL.SHARED_INPUTS_REFUSED`));
    expect(r.text).toContain("The broker 'MyBroker' has no FTMO limits configured.");
    expect(r.text).not.toContain('{{');
  });

  it('memberNotFoundListsTheUnknownIds', () => {
    const r = render(
      groupWideRefusal(FtmoGroupRefusal.MemberNotFound, { unknownStrategyIds: ['u-1', 'u-2'] }),
    );
    expect(r.text).toContain('u-1');
    expect(r.text).toContain('u-2');
  });

  it('anUnknownValueRendersTheUnknownLabelWithTheRawValue_AndNoPlaceholder', () => {
    const r = render(groupWideRefusal(99 as FtmoGroupRefusal));
    expect(r.text).toContain('Unknown value (99)');
    expect(r.text).not.toContain('{{');
  });

  it('noTwoRefusalsRenderTheSameText_InEnAndEs', () => {
    for (const lang of ['en', 'es'] as const) {
      const texts: string[] = [];
      for (let value = 0; value <= 7; value++) {
        const r = render(groupWideRefusal(value as FtmoGroupRefusal), lang);
        texts.push(
          (r.el.querySelector('.ftmo-group-result__refusal-text')?.textContent ?? '').trim(),
        );
      }
      expect(
        texts.every((t) => t.length > 0),
        lang,
      ).toBe(true);
      expect(new Set(texts).size, lang).toBe(8);
    }
  });
});

describe('window and coverage (F3.4.4)', () => {
  it('showsTheWindowAndACoverageRowPerMemberInEachSuccessfulKind', () => {
    const r = render(groupResult([successKind(DEPLOY), successKind(EVAL)]));
    expect(r.el.querySelectorAll('.ftmo-group-result__window')).toHaveLength(2);
    expect(r.el.querySelectorAll('.ftmo-group-result__coverage-row')).toHaveLength(4);
    expect(r.text).toContain('2020-02-01');
    expect(r.text).toContain('2021-06-30');
    expect(r.el.querySelector('.ftmo-group-result__shortened')).toBeNull();
  });

  it('statesTheShortenedWindowWhenAMemberRangeIsWider', () => {
    const kind = successKind(DEPLOY);
    kind.coverage = [coverage('a', 'Alpha', { firstOpen: '2019-01-01T00:00:00' })];
    const r = render(groupResult([kind]));
    expect(r.el.querySelectorAll('.ftmo-group-result__shortened')).toHaveLength(1);
    expect(r.text).toContain(r.t(`${RESULT}SHORTENED`));
  });

  it('anEmptyMemberRendersTheAbsentMarker_AndItsZeroTrades', () => {
    const kind = successKind(DEPLOY);
    kind.coverage = [
      coverage('a', 'Alpha', { firstOpen: null, lastClose: null, inWindowTrades: 0 }),
    ];
    const r = render(groupResult([kind]));
    const row = (r.el.querySelector('.ftmo-group-result__coverage-row') as HTMLElement).textContent;
    expect(row).toContain(r.t('FTMO_SIMULATION.NOT_REPORTED'));
    expect(row).toContain('0');
  });
});

describe('disclosures (F3.4.5)', () => {
  it('showsTheThreeGroupPointsOnce_AndTheReusedResultNotesOnce_InEnAndEs', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(groupResult([successKind(DEPLOY), successKind(EVAL)]), lang);
      for (const key of ['CONCURRENT', 'SAME_CLOSE', 'ELIGIBILITY']) {
        expect(occurrences(r.text, r.t(`${RESULT}DISCLOSURE.${key}`)), `${lang} ${key}`).toBe(1);
      }
      expect(occurrences(r.text, r.t('FTMO_SIMULATION.DISCLOSURE_RESULT')), lang).toBe(1);
      expect(occurrences(r.text, r.t('FTMO_SIMULATION.NOT_MODELLED_HEADING')), lang).toBe(1);
      expect(occurrences(r.text, r.t('FTMO_SIMULATION.NOT_MODELLED.SWAP')), lang).toBe(1);
    }
  });

  it('neverRendersTheServerDisclosureText', () => {
    const dto = groupResult([successKind(DEPLOY), successKind(EVAL)]);
    expect(dto.disclosures.length).toBeGreaterThan(0);
    const r = render(dto);
    for (const text of dto.disclosures) expect(r.text).not.toContain(text);
    expect(r.text).not.toContain('server text, never rendered');
  });

  it('theGroupDisclosureAlsoAccompaniesAGroupWideRefusal', () => {
    const r = render(groupWideRefusal(FtmoGroupRefusal.InvalidRequest));
    expect(occurrences(r.text, r.t(`${RESULT}DISCLOSURE.CONCURRENT`))).toBe(1);
  });

  it('aDuplicateNameWarningNamesTheSharedNameAndBothMembers', () => {
    const r = render(
      groupResult([successKind(DEPLOY)], {
        duplicateNameWarnings: [{ name: 'Alpha', strategyIds: ['id-one', 'id-two'] }],
      }),
    );
    const warning = (r.el.querySelector('.ftmo-group-result__name-warning') as HTMLElement)
      .textContent;
    expect(warning).toContain('Alpha');
    expect(warning).toContain('id-one');
    expect(warning).toContain('id-two');
  });
});
