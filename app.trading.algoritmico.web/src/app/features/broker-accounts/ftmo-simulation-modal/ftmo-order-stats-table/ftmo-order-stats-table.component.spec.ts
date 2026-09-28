import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FtmoOrderStatsTableComponent } from './ftmo-order-stats-table.component';
import { FtmoOrderStatRowVm } from '../ftmo-simulation.mappers';
import en from '../../../../../../public/assets/i18n/en.json';
import es from '../../../../../../public/assets/i18n/es.json';

/** Collapses template whitespace so the assertion reads the text a user sees. */
function visibleText(el: Element): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}

/** Cell-by-cell text: Angular strips the whitespace between `<th>`/`<td>` siblings. */
function cellTexts(row: Element): string[] {
  return Array.from(row.querySelectorAll('th, td')).map((cell) => visibleText(cell));
}

function makeRows(overrides: Partial<FtmoOrderStatRowVm>[] = []): FtmoOrderStatRowVm[] {
  const base: FtmoOrderStatRowVm[] = [
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.PHASE1_TARGET',
      n: 12,
      min: 3,
      q1: 5,
      median: 7,
      q3: 9,
      max: 20,
    },
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.PHASE2_TARGET',
      n: 10,
      min: 2,
      q1: 4,
      median: 6,
      q3: 8,
      max: 15,
    },
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.BOTH_TARGETS',
      n: 8,
      min: 1,
      q1: 3,
      median: 5,
      q3: 7,
      max: 12,
    },
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.FUNDED_FROM_FUNDED_START',
      n: 6,
      min: 1,
      q1: 2,
      median: 3,
      q3: 4,
      max: 5,
    },
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.FUNDED_FROM_CHAIN_START',
      n: 6,
      min: 30,
      q1: 31,
      median: 33,
      q3: 34,
      max: 36,
      secondary: true,
    },
    {
      labelKey: 'FTMO_SIMULATION.ORDER_STATS.ROW.CENSORED_RUNWAY',
      n: 4,
      min: 0,
      q1: 1,
      median: 2,
      q3: 3,
      max: 4,
    },
  ];
  return base.map((row, index) => ({ ...row, ...(overrides[index] ?? {}) }));
}

describe('FtmoOrderStatsTableComponent', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoOrderStatsTableComponent, TranslateModule.forRoot()],
    });
  });

  function create(rows: FtmoOrderStatRowVm[]): ComponentFixture<FtmoOrderStatsTableComponent> {
    const fixture = TestBed.createComponent(FtmoOrderStatsTableComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    return fixture;
  }

  it('renders_APlainSemanticTable_WithNMinQ1MedianQ3MaxColumns_ForAllSixRows', () => {
    const fixture = create(makeRows());
    const table = fixture.nativeElement.querySelector('table');
    expect(table).toBeTruthy();
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(6);
    // The funded-from-chain-start row is the secondary/muted row (headline is the funded-start row).
    expect(bodyRows[4].classList.contains('ftmo-order-stats-table__row--secondary')).toBe(true);
    expect(bodyRows[3].classList.contains('ftmo-order-stats-table__row--secondary')).toBe(false);
  });

  it('anNEqualsZeroRow_RendersEveryQuantileAsAbsent_NeverZero', () => {
    const rows = makeRows([{ min: null, q1: null, median: null, q3: null, max: null, n: 0 }]);
    const fixture = create(rows);
    const firstRow = fixture.nativeElement.querySelectorAll('tbody tr')[0] as HTMLElement;
    const cells = firstRow.querySelectorAll('td');
    // labelKey, n, min, q1, median, q3, max
    expect(cells[1].textContent?.trim()).toBe('0');
    expect(cells[2].textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ABSENT');
    expect(cells[3].textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ABSENT');
    expect(cells[4].textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ABSENT');
    expect(cells[5].textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ABSENT');
    expect(cells[6].textContent).toContain('FTMO_SIMULATION.ORDER_STATS.ABSENT');
  });

  it('aMinEqualsZeroRowWithNGreaterThanZero_Renders0_NotAbsent', () => {
    const rows = makeRows([{ min: 0, n: 4 }]);
    const fixture = create(rows);
    const firstRow = fixture.nativeElement.querySelectorAll('tbody tr')[0] as HTMLElement;
    const minCell = firstRow.querySelectorAll('td')[2];
    expect(minCell.textContent?.trim()).toBe('0');
    expect(minCell.textContent).not.toContain('ABSENT');
  });

  describe('with the real en/es dictionaries', () => {
    // The REAL dictionaries (Dual-Entry precedent: portfolio-detail.component.spec.ts): only a real
    // dictionary exposes a `{{param}}` placeholder the template forgot to fill.
    beforeEach(() => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('en', en);
      translate.setTranslation('es', es);
      translate.use('en');
    });

    it('headersRowLabelsAndAbsentCells_RenderAsFinalText_WithNoPlaceholderLeak', () => {
      const rows = makeRows([{ min: null, q1: null, median: null, q3: null, max: null, n: 0 }]);
      const fixture = create(rows);
      const host = fixture.nativeElement as HTMLElement;
      expect(cellTexts(host.querySelector('thead tr') as Element)).toEqual([
        '',
        'N',
        'Min',
        'Q1',
        'Median',
        'Q3',
        'Max',
      ]);
      const bodyRows = host.querySelectorAll('tbody tr');
      expect(cellTexts(bodyRows[0])).toEqual([
        'Days to Phase 1 target',
        '0',
        '—',
        '—',
        '—',
        '—',
        '—',
      ]);
      expect(cellTexts(bodyRows[1])).toEqual([
        'Days to Phase 2 target',
        '10',
        '2',
        '4',
        '6',
        '8',
        '15',
      ]);
      expect(visibleText(host)).not.toContain('{{');
      expect(visibleText(host)).not.toContain('FTMO_SIMULATION.');
    });
  });
});
