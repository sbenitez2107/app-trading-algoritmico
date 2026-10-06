import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import {
  FtmoGroupSearchIneligibleDto,
  FtmoGroupSearchIneligibleReason,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import { FtmoGroupRefusal } from '../../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { searchKind, searchRow } from '../ftmo-group-search.fixtures';
import { refusedKind } from '../ftmo-group-simulation.result.fixtures';
import { FtmoSearchTableComponent } from './ftmo-search-table.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const D = BacktestRunKind.Deploy;
const E = BacktestRunKind.Evaluation;

interface Inputs {
  rows?: FtmoGroupSearchRowDto[];
  status?: FtmoGroupSearchStatus;
  ineligible?: FtmoGroupSearchIneligibleDto[];
  defaultCeilingPercent?: number;
}

function render(inputs: Inputs = {}, lang: 'en' | 'es' = 'en') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoSearchTableComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture: ComponentFixture<FtmoSearchTableComponent> =
    TestBed.createComponent(FtmoSearchTableComponent);
  fixture.componentRef.setInput('rows', inputs.rows ?? [searchRow()]);
  fixture.componentRef.setInput('status', inputs.status ?? FtmoGroupSearchStatus.Completed);
  fixture.componentRef.setInput('ineligible', inputs.ineligible ?? []);
  if (inputs.defaultCeilingPercent !== undefined)
    fixture.componentRef.setInput('defaultCeilingPercent', inputs.defaultCeilingPercent);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, text: () => el.textContent ?? '' };
}

const T = (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['TABLE'];

function bodyRows(el: HTMLElement): HTMLElement[] {
  return Array.from(el.querySelectorAll<HTMLElement>('tbody tr.ftmo-search-table__row'));
}

function setCeiling(
  fixture: ComponentFixture<FtmoSearchTableComponent>,
  el: HTMLElement,
  value: string,
) {
  const input = el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

function ceilingRows(shares: [number, number][]): FtmoGroupSearchRowDto[] {
  return shares.map(([d, e], i) =>
    searchRow({
      rank: i + 1,
      kinds: [searchKind(D, { breached: d * 1000 }), searchKind(E, { breached: e * 1000 })],
    }),
  );
}

describe('FtmoSearchTableComponent', () => {
  it('rendersOneRowPerGroup_InBackendOrder', () => {
    const { el } = render({
      rows: [searchRow({ rank: 3 }), searchRow({ rank: 1 }), searchRow({ rank: 2 })],
    });
    const ranks = bodyRows(el).map((r) => r.querySelector('.ftmo-search-table__rank')?.textContent);
    expect(ranks?.map((x) => x?.trim())).toEqual(['3', '1', '2']);
  });

  it('showsMembersSymbolsPeak_AndEachKindOnItsOwn', () => {
    const { el, text } = render();
    const t = text();
    for (const s of ['Alpha', 'Beta', 'EURUSD', 'GBPJPY']) expect(t).toContain(s);
    const cells = Array.from(el.querySelectorAll('.ftmo-search-table__peak')).map((c) =>
      c.textContent?.trim(),
    );
    expect(cells).toEqual(['3']);
    // Deploy: 4.5 / 45 / 80 / 30 / 70 / 120; Evaluation: 3 / 10 / 60 / 25 / 65 / 0
    const row = bodyRows(el)[0];
    const kindCells = (kind: string) =>
      Array.from(row.querySelectorAll(`[data-kind="${kind}"]`)).map((c) => c.textContent?.trim());
    expect(kindCells('deploy')).toEqual(['4.5%', '45%', '80%', '30%', '70%', '120']);
    expect(kindCells('evaluation')).toEqual(['3%', '10%', '60%', '25%', '65%', '0']);
  });

  it('theHeaderNamesBothKindsAndEveryMetric', () => {
    const { el } = render();
    const head = el.querySelector('thead')?.textContent ?? '';
    expect(head).toContain('Deploy');
    expect(head).toContain('Evaluation');
    for (const key of [
      'BREACH',
      'WORST_DAILY',
      'WORST_DRAWDOWN',
      'MEDIAN_DRAWDOWN',
      'FUNDED_NO_BREACH',
      'MEDIAN_DAYS',
    ]) {
      expect(head).toContain(T[key]);
    }
  });

  it('aNullMedianShowsTheAbsentMarker_AndARealZeroShowsZero', () => {
    const { el } = render({
      rows: [
        searchRow({
          kinds: [searchKind(D, { medianDays: null }), searchKind(E, { medianDays: 0 })],
        }),
      ],
    });
    const row = bodyRows(el)[0];
    const median = (kind: string) =>
      row
        .querySelector(`[data-kind="${kind}"].ftmo-search-table__median-days`)
        ?.textContent?.trim();
    expect(median('deploy')).toBe('Not reported');
    expect(median('evaluation')).toBe('0');
  });

  it('aRefusedKindShowsItsTranslatedReason_NotZeroNorBlank', () => {
    const { el } = render({
      rows: [
        searchRow({ kinds: [searchKind(D), refusedKind(E, FtmoGroupRefusal.NoCommonWindow)] }),
      ],
    });
    const refusal = el.querySelector('.ftmo-search-table__refusal[data-kind="evaluation"]');
    expect(refusal?.textContent?.trim()).toBe(
      (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['RESULT']['REFUSAL']['NO_COMMON_WINDOW'],
    );
    expect(el.querySelector('.ftmo-search-table__refusal[data-kind="deploy"]')).toBeNull();
  });

  it('theIdenticalFlagShowsOnlyOnFlaggedRows', () => {
    const { el } = render({
      rows: [searchRow({ identicalDeployEval: true }), searchRow({ rank: 2 })],
    });
    const flags = bodyRows(el).map((r) => r.querySelector('.ftmo-search-table__identical'));
    expect(flags[0]).not.toBeNull();
    expect(flags[0]?.textContent).toContain(T['IDENTICAL_YES']);
    expect(flags[1]).toBeNull();
  });

  it('showsAnEmptyState_WhenThereAreNoRows_OnATerminalStatus', () => {
    const { el, text } = render({ rows: [] });
    expect(text()).toContain(T['EMPTY']);
    expect(el.querySelector('table')).toBeNull();
  });

  it('whileRunningWithNoRowsShowsThePlaceholderNotTheEmptyState', () => {
    const { text } = render({ rows: [], status: FtmoGroupSearchStatus.Running });
    expect(text()).toContain((en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['TABLE_PLACEHOLDER']);
    expect(text()).not.toContain(T['EMPTY']);
  });

  it('cancelledAndStoppedAtBudgetShowThePartialTable_WithANote', () => {
    for (const status of [FtmoGroupSearchStatus.Cancelled, FtmoGroupSearchStatus.StoppedAtBudget]) {
      const { el, text } = render({ status });
      expect(bodyRows(el), String(status)).toHaveLength(1);
      expect(text(), String(status)).toContain(T['PARTIAL']);
    }
    expect(render({ status: FtmoGroupSearchStatus.Completed }).text()).not.toContain(T['PARTIAL']);
  });

  it('eachRowOffersAnOpenInGroupSimulatorOutput', () => {
    const rows = [searchRow({ rank: 1 }), searchRow({ rank: 2, memberIds: ['s3', 's4'] })];
    const { fixture, el } = render({ rows });
    const emitted: FtmoGroupSearchRowDto[] = [];
    fixture.componentInstance.openInGroup.subscribe((r) => emitted.push(r));
    const buttons = el.querySelectorAll<HTMLButtonElement>('.ftmo-search-table__open');
    expect(buttons).toHaveLength(2);
    buttons[1].click();
    expect(emitted).toEqual([rows[1]]);
  });

  it('listsTheExcludedStrategiesByReason_WithCounts', () => {
    const ineligible = [
      FtmoGroupSearchIneligibleReason.MissingKind,
      FtmoGroupSearchIneligibleReason.MissingKind,
      FtmoGroupSearchIneligibleReason.SymbolRefused,
    ].map((reason, i) => ({ strategyId: `x${i}`, name: `x${i}`, reason, refusal: null }));
    const { el } = render({ ineligible });
    const items = Array.from(el.querySelectorAll('.ftmo-search-table__excluded li')).map((li) =>
      li.textContent?.trim(),
    );
    expect(items).toEqual(['Missing Deploy or Evaluation run: 2', 'Instrument not usable: 1']);
  });

  it('noExcludedBlockWhenNothingWasExcluded', () => {
    expect(render().el.querySelector('.ftmo-search-table__excluded')).toBeNull();
  });
});

describe('FtmoSearchTableComponent ceiling', () => {
  it('highlightsByTheWorseKind_AtTheDefaultFivePercent', () => {
    const { el } = render({
      rows: ceilingRows([
        [0.03, 0.06],
        [0.03, 0.04],
      ]),
    });
    const flags = bodyRows(el).map((r) => r.classList.contains('ftmo-search-table__row--within'));
    expect(flags).toEqual([false, true]);
  });

  it('editingTheCeilingToTenPercentReEvaluatesWithoutDroppingRows', () => {
    const { fixture, el } = render({
      rows: ceilingRows([
        [0.03, 0.06],
        [0.03, 0.04],
      ]),
    });
    setCeiling(fixture, el, '10');
    const flags = bodyRows(el).map((r) => r.classList.contains('ftmo-search-table__row--within'));
    expect(flags).toEqual([true, true]);
  });

  it('theFilterListsOnlyHighlightedRows_AndTurningItOffRestoresEveryRow', () => {
    const { fixture, el } = render({
      rows: ceilingRows([
        [0.03, 0.06],
        [0.03, 0.04],
      ]),
    });
    const toggle = el.querySelector<HTMLInputElement>('.ftmo-search-table__only-within')!;
    toggle.click();
    fixture.detectChanges();
    expect(bodyRows(el)).toHaveLength(1);
    setCeiling(fixture, el, '10');
    expect(bodyRows(el)).toHaveLength(2);
    toggle.click();
    fixture.detectChanges();
    expect(bodyRows(el)).toHaveLength(2);
  });

  it('theCeilingInputIsAnyStep_AndDefaultsToFive', () => {
    const input = render().el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
    expect(input.getAttribute('step')).toBe('any');
    expect(input.value).toBe('5');
  });

  it('theFilterEmptyingTheTableShowsTheNoMatchMessage_AndKeepsTheControls', () => {
    const { fixture, el, text } = render({ rows: ceilingRows([[0.06, 0.06]]) });
    el.querySelector<HTMLInputElement>('.ftmo-search-table__only-within')!.click();
    fixture.detectChanges();
    expect(bodyRows(el)).toHaveLength(0);
    expect(text()).toContain(T['NO_MATCH']);
    expect(el.querySelector('.ftmo-search-table__ceiling-input')).not.toBeNull();
  });
});

describe('FtmoSearchTableComponent requested ceiling', () => {
  it('defaultsToTheRequestedCeiling_AndStaysEditable', () => {
    const { fixture, el } = render({
      rows: ceilingRows([
        [0.03, 0.06],
        [0.03, 0.04],
      ]),
      defaultCeilingPercent: 3.5,
    });
    const input = el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
    expect(input.value).toBe('3.5');
    let flags = bodyRows(el).map((r) => r.classList.contains('ftmo-search-table__row--within'));
    expect(flags).toEqual([false, false]);
    setCeiling(fixture, el, '10');
    flags = bodyRows(el).map((r) => r.classList.contains('ftmo-search-table__row--within'));
    expect(flags).toEqual([true, true]);
  });

  it('aNewRequestedCeilingResetsTheInput', () => {
    const { fixture, el } = render({ defaultCeilingPercent: 3 });
    setCeiling(fixture, el, '10');
    fixture.componentRef.setInput('defaultCeilingPercent', 7);
    fixture.detectChanges();
    expect(el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!.value).toBe(
      '7',
    );
  });
});

describe('FtmoSearchTableComponent real dictionaries', () => {
  const states: [string, Inputs][] = [
    ['rows', {}],
    ['empty', { rows: [] }],
    ['running', { rows: [], status: FtmoGroupSearchStatus.Running }],
    ['partial', { status: FtmoGroupSearchStatus.Cancelled }],
    ['stopped', { status: FtmoGroupSearchStatus.StoppedAtBudget }],
    ['unknownStatus', { status: FtmoGroupSearchStatus.Unknown }],
    [
      'refused',
      {
        rows: [
          searchRow({
            identicalDeployEval: true,
            symbols: null,
            kinds: [
              refusedKind(D, FtmoGroupRefusal.InvalidRequest),
              searchKind(E, { medianDays: null }),
            ],
          }),
        ],
        ineligible: [
          {
            strategyId: 'x',
            name: 'x',
            reason: FtmoGroupSearchIneligibleReason.Unknown,
            refusal: null,
          },
          {
            strategyId: 'y',
            name: 'y',
            reason: FtmoGroupSearchIneligibleReason.IdenticalDeployEval,
            refusal: null,
          },
        ],
      },
    ],
  ];

  it('noStateLeaksAPlaceholderOrARawKey_InEitherLocale', () => {
    for (const lang of ['en', 'es'] as const) {
      for (const [name, inputs] of states) {
        const text = render(inputs, lang).text();
        expect(text, `${lang}:${name}`).not.toContain('{{');
        expect(text, `${lang}:${name}`).not.toMatch(/SIMULATOR\.|FTMO_SIMULATION\./);
      }
    }
  });

  it('theTwoLocalesRenderDifferentText', () => {
    for (const [name, inputs] of states) {
      expect(render(inputs, 'en').text(), name).not.toBe(render(inputs, 'es').text());
    }
  });

  it('theSpanishHeaderUsesTheSpanishKindAndMetricNames', () => {
    const head = render({}, 'es').el.querySelector('thead')?.textContent ?? '';
    expect(head).toContain('Evaluación');
    expect(head).toContain((es as JsonTree)['SIMULATOR']['FTMO_SEARCH']['TABLE']['BREACH']);
  });
});
