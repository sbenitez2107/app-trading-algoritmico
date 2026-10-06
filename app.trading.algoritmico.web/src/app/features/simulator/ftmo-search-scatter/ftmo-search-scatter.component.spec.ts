import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import { FtmoGroupSearchRowDto } from '../../../core/models/ftmo-group-search.model';
import { FtmoGroupRefusal } from '../../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { searchKind, searchRow } from '../ftmo-group-search.fixtures';
import { refusedKind } from '../ftmo-group-simulation.result.fixtures';
import { FtmoSearchScatterComponent } from './ftmo-search-scatter.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const D = BacktestRunKind.Deploy;
const E = BacktestRunKind.Evaluation;
const S = (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['SCATTER'];
const S_ES = (es as JsonTree)['SIMULATOR']['FTMO_SEARCH']['SCATTER'];

function row(rank: number, breached: number, days: number | null): FtmoGroupSearchRowDto {
  return searchRow({
    rank,
    memberIds: [`a${rank}`, `b${rank}`],
    memberNames: [`Alpha${rank}`, `Beta${rank}`],
    kinds: [
      searchKind(D, { breached, medianDays: days }),
      searchKind(E, { breached, medianDays: days }),
    ],
  });
}

function render(
  rows: FtmoGroupSearchRowDto[],
  ceilingPercent: number | null = 5,
  lang: 'en' | 'es' = 'en',
) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoSearchScatterComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture: ComponentFixture<FtmoSearchScatterComponent> = TestBed.createComponent(
    FtmoSearchScatterComponent,
  );
  fixture.componentRef.setInput('rows', rows);
  fixture.componentRef.setInput('ceilingPercent', ceilingPercent);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    el,
    text: () => el.textContent ?? '',
    points: () => Array.from(el.querySelectorAll<SVGElement>('.ftmo-search-scatter__point')),
  };
}

const FRONTIER = 'ftmo-search-scatter__point--frontier';
const WITHIN = 'ftmo-search-scatter__point--within';

describe('FtmoSearchScatterComponent', () => {
  it('plotsOnePointPerRow_WithAnAccessibleTitle', () => {
    const { points } = render([row(1, 40, 120), row(2, 30, 90)]);
    expect(points()).toHaveLength(2);
    const title = points()[0].querySelector('title')!.textContent!;
    expect(title).toContain('Alpha1, Beta1');
    expect(title).toContain('120');
    expect(title).toContain('4%');
    expect(points()[0].getAttribute('aria-label')).toBe(title);
  });

  it('theTitleIsTheSpanishTemplateFilledWithTheValues', () => {
    const { points } = render([row(1, 45, 120)], 5, 'es');
    const title = points()[0].querySelector('title')!.textContent!;
    expect(title).toBe(
      (S_ES['POINT_TITLE'] as string)
        .replace('{{members}}', 'Alpha1, Beta1')
        .replace('{{days}}', '120')
        .replace('{{breach}}', '4.5'),
    );
  });

  it('highlightsTheFrontier_AndOnlyTheFrontier', () => {
    // (100d,5%) (120d,3%) (130d,4%): the third is dominated.
    const { points } = render([row(1, 50, 100), row(2, 30, 120), row(3, 40, 130)]);
    expect(points().map((p) => p.classList.contains(FRONTIER))).toEqual([true, true, false]);
  });

  it('stylesPointsInsideTheCeilingDistinctly_UsingTheGivenCeiling', () => {
    const rows = [row(1, 50, 100), row(2, 30, 120), row(3, 40, 130)];
    expect(
      render(rows, 4)
        .points()
        .map((p) => p.classList.contains(WITHIN)),
    ).toEqual([false, true, true]);
    expect(
      render(rows, null)
        .points()
        .map((p) => p.classList.contains(WITHIN)),
    ).toEqual([false, false, false]);
  });

  it('placesTheTickLabelsOfTheAxes', () => {
    const { el } = render([row(1, 40, 120)]);
    const labels = Array.from(el.querySelectorAll('.ftmo-search-scatter__tick')).map((t) =>
      (t.textContent ?? '').trim(),
    );
    expect(labels).toContain('0');
    expect(labels).toContain('120');
  });

  it('aZeroDaysPointIsPlotted_AtTheOrigin', () => {
    const { points, el } = render([row(1, 0, 0)]);
    expect(points()).toHaveLength(1);
    expect(points()[0].getAttribute('cx')).toBe('56');
    expect(el.querySelector('.ftmo-search-scatter__not-plotted')).toBeNull();
  });

  it('listsRowsWithoutAMedianAsNotPlotted_NeverAtZero', () => {
    const { points, text } = render([row(1, 40, 120), row(2, 40, null), row(3, 40, null)]);
    expect(points()).toHaveLength(1);
    expect(text()).toContain(String(S['NOT_PLOTTED']).replace('{{count}}', '2'));
  });

  it('aRefusedKindMakesTheRowNotPlotted', () => {
    const refused = searchRow({
      rank: 9,
      kinds: [
        searchKind(D, { breached: 10, medianDays: 90 }),
        refusedKind(E, FtmoGroupRefusal.NoCommonWindow),
      ],
    });
    expect(render([refused]).points()).toHaveLength(0);
  });

  it('clickingAPointEmitsItsRow', () => {
    const rows = [row(1, 40, 120), row(2, 30, 90)];
    const { fixture, points } = render(rows);
    const emitted: FtmoGroupSearchRowDto[] = [];
    fixture.componentInstance.openInGroup.subscribe((r) => emitted.push(r));
    points()[1].dispatchEvent(new Event('click'));
    expect(emitted).toEqual([rows[1]]);
  });

  it('theEnterKeyOnAPointEmitsItsRow', () => {
    const rows = [row(1, 40, 120)];
    const { fixture, points } = render(rows);
    const emitted: FtmoGroupSearchRowDto[] = [];
    fixture.componentInstance.openInGroup.subscribe((r) => emitted.push(r));
    points()[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(emitted).toEqual([rows[0]]);
    expect(points()[0].getAttribute('tabindex')).toBe('0');
  });

  it.each(['en', 'es'] as const)('rendersNoPlaceholderAndNoRawKey_%s', (lang) => {
    const { text, el } = render([row(1, 40, 120), row(2, 40, null)], 5, lang);
    expect(text()).not.toContain('{{');
    expect(text()).not.toMatch(/SIMULATOR\.|FTMO_SIMULATION\./);
    expect(el.querySelector('svg')!.getAttribute('aria-label')).not.toMatch(/SIMULATOR\.|\{\{/);
  });

  it('theTwoLocalesRenderDifferentText', () => {
    const rows = [row(1, 40, 120), row(2, 40, null)];
    const a = render(rows, 5, 'en').text();
    const b = render(rows, 5, 'es').text();
    expect(a).not.toBe(b);
    expect(a).toContain(S['X_AXIS']);
    expect(b).toContain(S_ES['X_AXIS']);
  });

  it('showsAnEmptyMessageInsteadOfAnEmptyChart', () => {
    const { text, points } = render([row(1, 40, null)]);
    expect(points()).toHaveLength(0);
    expect(text()).toContain(S['EMPTY']);
  });
});
