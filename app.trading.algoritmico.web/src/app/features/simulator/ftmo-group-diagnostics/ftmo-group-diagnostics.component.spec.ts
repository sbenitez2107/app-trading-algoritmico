import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import { FtmoGroupDiagnosticsDto } from '../../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import {
  THREE_MEMBERS,
  contribution,
  diagnosticsFixture,
  memberAttribution,
} from '../ftmo-group-simulation.result.fixtures';
import { toDiagnosticsVm } from '../ftmo-group-simulation.result.mappers';
import { FtmoGroupDiagnosticsComponent } from './ftmo-group-diagnostics.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

const RAW_KEY = /\b[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)+\b/;
const D = 'SIMULATOR.FTMO_GROUP.DIAGNOSTICS.';

function render(diagnostics: FtmoGroupDiagnosticsDto, lang: 'en' | 'es' = 'en') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupDiagnosticsComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupDiagnosticsComponent);
  fixture.componentRef.setInput(
    'vm',
    toDiagnosticsVm(BacktestRunKind.Deploy, diagnostics, THREE_MEMBERS),
  );
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return {
    el,
    text: el.textContent ?? '',
    t: (key: string, params?: Record<string, unknown>) => translate.instant(key, params) as string,
    rows: (cls: string): string[] =>
      Array.from(el.querySelectorAll(`.ftmo-group-diagnostics__${cls}`)).map((n) => {
        const clean = (x: Element): string => (x.textContent ?? '').replace(/\s+/g, ' ').trim();
        const cells = Array.from(n.querySelectorAll('th, td'));
        return cells.length > 0 ? cells.map(clean).join(' ') : clean(n);
      }),
  };
}

describe('FtmoGroupDiagnosticsComponent (F4.2)', () => {
  it('rendersAContributionRowPerMember_WithTradesNetAndCounts', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(diagnosticsFixture(), lang);
      const rows = r.rows('contribution-row');
      expect(rows, lang).toHaveLength(3);
      expect(rows[0], lang).toBe('Alpha 20 18 150.5 210.25 2 1 2');
      expect(rows[1], lang).toBe('Beta 20 18 -42.5 0 2 1 2');
      expect(rows[2], lang).toBe('Gamma 20 18 150.5 210.25 0 0 0');
      for (const key of [
        'MEMBER',
        'TRADES',
        'SCALABLE',
        'NET_LOW',
        'NET_HIGH',
        'RAISED',
        'CAPPED',
        'UNSCALABLE',
      ]) {
        expect(r.text, `${lang}/${key}`).toContain(r.t(`${D}CONTRIBUTION.COLUMN.${key}`));
      }
    }
  });

  it('aNetIsShownAsMoney_NeverAsAPercent', () => {
    const r = render(
      diagnosticsFixture({
        contributions: [contribution('a', 'Alpha', { netLow: 0.05, netHigh: -3 })],
      }),
    );
    const row = r.rows('contribution-row')[0];
    expect(row).toContain('0.05');
    expect(row).not.toContain('%');
  });

  it('showsCountsBesideSharesByPhase_PlusSoleAndTied', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(diagnosticsFixture(), lang);
      const share = (count: number, pct: number): string =>
        r.t(`${D}COUNT_SHARE`, { count, share: pct });
      const rows = r.rows('attribution-row');
      expect(rows, lang).toHaveLength(3);
      expect(rows[0], lang).toBe(
        ['Alpha', share(4, 40), share(1, 10), share(1, 10), share(4, 40), share(2, 20)].join(' '),
      );
      expect(rows[2], lang).toBe(
        ['Gamma', share(0, 0), share(0, 0), share(1, 10), share(1, 10), share(0, 0)].join(' '),
      );
    }
  });

  it('labelsSharedClosesAsTiedAndExplainsWhyRowsDoNotAddUp', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(diagnosticsFixture(), lang);
      expect(r.text, lang).toContain(r.t(`${D}ATTRIBUTION.COLUMN.TIED`));
      expect(r.text, lang).toContain(r.t(`${D}ATTRIBUTION.COLUMN.SOLE`));
      expect(r.text, lang).toContain(r.t(`${D}ATTRIBUTION.NOTE`));
      expect(r.rows('total-tied')[0], lang).toContain(
        r.t(`${D}COUNT_SHARE`, { count: 2, share: 20 }),
      );
      expect(r.rows('total-tied')[0], lang).toContain(r.t(`${D}ATTRIBUTION.TOTAL.TIED`));
    }
  });

  it('showsTheKindTotals_IncludingUnattributedStarts', () => {
    const r = render(diagnosticsFixture());
    expect(r.rows('total-deciding')[0]).toContain('10');
    expect(r.rows('total-unattributed')[0]).toContain('0 (0%)');
    expect(r.el.querySelector('.ftmo-group-diagnostics__unattributed-note')).toBeNull();

    const data = diagnosticsFixture();
    data.attribution.unattributedStarts = 1;
    const withData = render(data);
    expect(withData.rows('total-unattributed')[0]).toContain('1 (10%)');
    expect(withData.text).toContain(withData.t(`${D}ATTRIBUTION.UNATTRIBUTED_NOTE`));
  });

  it('namesTheMembersAtAPeakOfThree', () => {
    for (const lang of ['en', 'es'] as const) {
      const r = render(diagnosticsFixture(), lang);
      expect(r.rows('peak-value')[0], lang).toContain('3');
      expect(r.rows('peak-member'), lang).toEqual(['Alpha', 'Beta', 'Gamma']);
      expect(r.text, lang).toContain('2020-05-04 10:30');
    }
  });

  it('aPeakOfZeroIsShownAsZero_WithNoMembersList', () => {
    const r = render(
      diagnosticsFixture({
        peak: { peakConcurrentOpen: 0, firstReachedSource: null, memberIdsAtPeak: [] },
      }),
    );
    expect(r.rows('peak-value')[0]).toContain('0');
    expect(r.rows('peak-value')[0]).not.toContain(r.t('FTMO_SIMULATION.NOT_REPORTED'));
    expect(r.el.querySelector('.ftmo-group-diagnostics__peak-members')).toBeNull();
  });

  it('missingValuesRenderTheAbsentMarker_AndZerosStayZero', () => {
    const data = diagnosticsFixture();
    data.contributions = [
      {
        ...contribution('a', 'Alpha', { unscalable: 0 }),
        netLow: null,
        inWindowTrades: undefined,
      } as unknown as FtmoGroupDiagnosticsDto['contributions'][number],
    ];
    data.attribution.members = [
      {
        ...memberAttribution('a', 'Alpha'),
        phase1Starts: null,
      } as unknown as FtmoGroupDiagnosticsDto['attribution']['members'][number],
    ];
    const r = render(data);
    const absent = r.t('FTMO_SIMULATION.NOT_REPORTED');
    expect(r.rows('contribution-row')[0]).toBe(`Alpha ${absent} 18 ${absent} 210.25 2 1 0`);
    expect(r.rows('attribution-row')[0].startsWith(`Alpha ${absent} `)).toBe(true);
  });

  it('aZeroDecidingBreachCountShowsCountsWithoutShares', () => {
    const data = diagnosticsFixture();
    data.attribution = {
      decidingBreachStarts: 0,
      sharedCloseStarts: 0,
      unattributedStarts: 0,
      members: [
        memberAttribution('a', 'Alpha', {
          phase1Starts: 0,
          phase2Starts: 0,
          fundedStarts: 0,
          soleContributorStarts: 0,
          sharedCloseStarts: 0,
        }),
      ],
    };
    const r = render(data);
    expect(r.rows('attribution-row')[0]).toBe('Alpha 0 0 0 0 0');
    expect(r.rows('total-tied')[0]).not.toContain('%');
  });

  it('usesNeutralWording_AndNeverMentionsCorrelation', () => {
    for (const [lang, locale] of [
      ['en', en],
      ['es', es],
    ] as const) {
      const r = render(diagnosticsFixture(), lang);
      const normalized = r.text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
      expect(normalized, lang).not.toContain('correl');
      const keys = Object.keys(
        (locale as unknown as { SIMULATOR: { FTMO_GROUP: { DIAGNOSTICS: object } } }).SIMULATOR
          .FTMO_GROUP.DIAGNOSTICS,
      );
      expect(keys.filter((k) => /CORREL/i.test(k))).toEqual([]);
    }
  });

  it('hasNoPlaceholderAndNoRawKey_InBothLocales', () => {
    for (const lang of ['en', 'es'] as const) {
      const data = diagnosticsFixture();
      data.attribution.unattributedStarts = 1;
      const text = render(data, lang).text;
      expect(text.length).toBeGreaterThan(0);
      expect(text, lang).not.toContain('{{');
      expect(text, lang).not.toContain('}}');
      expect(RAW_KEY.exec(text)?.[0] ?? null, lang).toBeNull();
    }
  });

  it('theTwoLocalesRenderDifferentText', () => {
    expect(render(diagnosticsFixture(), 'en').text).not.toBe(
      render(diagnosticsFixture(), 'es').text,
    );
  });
});
