import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRequest,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import { FtmoGroupSearchService } from '../../../core/services/ftmo-group-search.service';
import { FtmoSimulationService } from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { searchJob, searchRequest, searchRow } from '../ftmo-group-search.fixtures';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const ROWS = [
  searchRow({ rank: 1, memberIds: ['s2', 's1'], memberNames: ['B', 'A'] }),
  searchRow({ rank: 2, memberIds: ['s3', 's1'], memberNames: ['C', 'A'] }),
];

function done(): FtmoGroupSearchJobDto {
  return searchJob({ status: FtmoGroupSearchStatus.Completed, rows: ROWS });
}

function setup(o: { current?: FtmoGroupSearchJobDto | null; lang?: 'en' | 'es' } = {}) {
  const navigate = vi.fn(() => Promise.resolve(true));
  const search = {
    startGroupSearch: vi.fn((request: FtmoGroupSearchRequest) =>
      of({ outcome: 'started', jobId: 'job-1', job: { ...done(), request } }),
    ),
    getGroupSearch: vi.fn(() => of(done())),
    getCurrentGroupSearch: vi.fn(() => of(o.current ?? null)),
    cancelGroupSearch: vi.fn(),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSearchPageComponent, TranslateModule.forRoot()],
    providers: [
      { provide: Router, useValue: { navigate } },
      { provide: FtmoGroupSearchService, useValue: search },
      {
        provide: TradingAccountService,
        useValue: { getAll: () => of([{ id: 'a2', name: 'SBDEMO2' }]) },
      },
      {
        provide: FtmoSimulationService,
        useValue: {
          getGroupCandidates: () =>
            of({
              tradingAccountId: 'a2',
              maxMembers: 4,
              candidates: [
                {
                  strategyId: 's1',
                  name: 'S1',
                  symbol: 'EURUSD',
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
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(o.lang ?? 'en');
  const fixture = TestBed.createComponent(FtmoGroupSearchPageComponent);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, navigate, search, component: fixture.componentInstance };
}

function typeInto(h: ReturnType<typeof setup>, name: string, value: string) {
  const field = h.el.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
  h.fixture.detectChanges();
}

function startSearch(h: ReturnType<typeof setup>) {
  typeInto(h, 'targetRiskPerTrade', '0.5');
  h.el.querySelector('form')!.dispatchEvent(new Event('submit'));
  h.fixture.detectChanges();
}

const OPEN = '.ftmo-search-table__open';
const POINT = '.ftmo-search-scatter__point';

describe('FtmoGroupSearchPageComponent deep link navigation', () => {
  it('aTableRowNavigatesToTheGroupSimulatorWithTheRequestOfTheSearch', () => {
    const h = setup();
    startSearch(h);
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    expect(h.navigate).toHaveBeenCalledTimes(1);
    expect(h.navigate).toHaveBeenCalledWith(['/simulator', 'ftmo'], {
      queryParams: { account: 'a2', members: 's1,s2', risk: 0.5, capital: 10000 },
    });
  });

  it('aScatterPointNavigatesToTheSameTarget', () => {
    const h = setup();
    startSearch(h);
    h.el.querySelector<SVGElement>(POINT)!.dispatchEvent(new Event('click'));
    expect(h.navigate).toHaveBeenCalledWith(['/simulator', 'ftmo'], {
      queryParams: { account: 'a2', members: 's1,s2', risk: 0.5, capital: 10000 },
    });
  });

  it('theSecondRowCarriesItsOwnMembers', () => {
    const h = setup();
    startSearch(h);
    h.el.querySelectorAll<HTMLButtonElement>(OPEN)[1].click();
    const args = h.navigate.mock.calls[0] as unknown as [string[], { queryParams: JsonTree }];
    expect(args[1].queryParams['members']).toBe('s1,s3');
  });

  it('navigatingNeverStartsOrPollsAnotherSearch', () => {
    const h = setup();
    startSearch(h);
    const starts = h.search.startGroupSearch.mock.calls.length;
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    expect(h.search.startGroupSearch.mock.calls.length).toBe(starts);
  });

  it('theLinkKeepsTheRequestOfTheSearch_NotLaterFormEdits', () => {
    const h = setup();
    startSearch(h);
    typeInto(h, 'targetRiskPerTrade', '3');
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    const args = h.navigate.mock.calls[0] as unknown as [string[], { queryParams: JsonTree }];
    expect(args[1].queryParams['risk']).toBe(0.5);
  });

  it('aReattachedJobLinksWithItsOwnRequest_EvenWithAnEmptyForm', () => {
    const h = setup({
      current: {
        ...done(),
        request: searchRequest({ targetRiskPerTrade: 100, initialCapital: 25000 }),
      },
    });
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    expect(h.navigate).toHaveBeenCalledWith(['/simulator', 'ftmo'], {
      queryParams: { account: 'a2', members: 's1,s2', risk: 100, capital: 25000 },
    });
  });

  it('aReattachedJobIgnoresLaterFormEditsInTheLink', () => {
    const h = setup({
      current: { ...done(), request: searchRequest({ targetRiskPerTrade: 100 }) },
    });
    typeInto(h, 'targetRiskPerTrade', '1.5');
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    const args = h.navigate.mock.calls[0] as unknown as [string[], { queryParams: JsonTree }];
    expect(args[1].queryParams['risk']).toBe(100);
  });

  it('theFxBandOfTheRequestTravelsInTheLink', () => {
    const h = setup({
      current: { ...done(), request: searchRequest({ fxLow: 0.9, fxHigh: 1.1 }) },
    });
    h.el.querySelector<HTMLButtonElement>(OPEN)!.click();
    const args = h.navigate.mock.calls[0] as unknown as [string[], { queryParams: JsonTree }];
    expect(args[1].queryParams['fxLow']).toBe(0.9);
    expect(args[1].queryParams['fxHigh']).toBe(1.1);
  });

  it('noLinkNoteExistsAnymore_NeitherReattachedNorStartedHere', () => {
    const reattached = setup({ current: done() });
    expect(reattached.el.querySelector('.ftmo-search-page__link-note')).toBeNull();
    const started = setup();
    startSearch(started);
    expect(started.el.querySelector('.ftmo-search-page__link-note')).toBeNull();
  });

  it('theLinkFromFormNoticeIsGoneFromBothLanguages', () => {
    expect(
      (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['NOTICE']['LINK_FROM_FORM'],
    ).toBeUndefined();
    expect(
      (es as JsonTree)['SIMULATOR']['FTMO_SEARCH']['NOTICE']['LINK_FROM_FORM'],
    ).toBeUndefined();
  });
});
