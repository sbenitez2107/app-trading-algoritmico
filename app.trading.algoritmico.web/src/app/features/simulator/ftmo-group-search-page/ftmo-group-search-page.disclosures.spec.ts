import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import { FtmoGroupSearchService } from '../../../core/services/ftmo-group-search.service';
import { FtmoSimulationService } from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { searchJob } from '../ftmo-group-search.fixtures';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page.component';

const DICTIONARIES = { en, es };

function render(lang: 'en' | 'es', withJob: boolean): HTMLElement {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSearchPageComponent, TranslateModule.forRoot()],
    providers: [
      {
        provide: FtmoGroupSearchService,
        useValue: {
          startGroupSearch: vi.fn(),
          getGroupSearch: vi.fn(() => of(searchJob())),
          getCurrentGroupSearch: vi.fn(() => of(withJob ? searchJob() : null)),
          cancelGroupSearch: vi.fn(() => of(undefined as void)),
        },
      },
      {
        provide: TradingAccountService,
        useValue: { getAll: () => of([{ id: 'a2', name: 'SBDEMO2' }]) },
      },
      {
        provide: FtmoSimulationService,
        useValue: {
          getGroupCandidates: () => of({ tradingAccountId: 'a2', maxMembers: 4, candidates: [] }),
        },
      },
    ],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupSearchPageComponent);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('FtmoGroupSearchPageComponent group simulator disclosures', () => {
  it.each(['en', 'es'] as const)('rendersTheThreeGroupDisclosuresWithResults_%s', (lang) => {
    const el = render(lang, true);
    const block = el.querySelector('.ftmo-search-page__group-disclosure');
    expect(block).not.toBeNull();
    const text = block?.textContent ?? '';
    const d = DICTIONARIES[lang].SIMULATOR.FTMO_GROUP.RESULT.DISCLOSURE;
    expect(text).toContain(d.TITLE);
    expect(text).toContain(d.CONCURRENT);
    expect(text).toContain(d.SAME_CLOSE);
    expect(text).toContain(d.ELIGIBILITY);
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  });

  it('falsification_NoGroupDisclosuresBeforeAnyResultExists', () => {
    const el = render('en', false);
    expect(el.querySelector('.ftmo-search-page__group-disclosure')).toBeNull();
  });
});
