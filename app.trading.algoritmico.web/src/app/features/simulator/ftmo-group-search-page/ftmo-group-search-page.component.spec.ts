import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import { FtmoGroupCandidatesDto } from '../../../core/models/ftmo-group-simulation.model';
import {
  FtmoGroupSearchService,
  FtmoGroupSearchStartOutcome,
} from '../../../core/services/ftmo-group-search.service';
import { FtmoSimulationService } from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { searchJob, searchRequest, searchRow } from '../ftmo-group-search.fixtures';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const LOST = { key: 'SIMULATOR.FTMO_SEARCH.ERRORS.LOST', detail: null };

function candidates(needsFx = false): FtmoGroupCandidatesDto {
  return {
    tradingAccountId: 'a2',
    maxMembers: 4,
    candidates: [
      {
        strategyId: 's1',
        name: 'S1',
        symbol: 'EURUSD',
        deploy: needsFx ? ({ needsFxBand: true } as never) : null,
        evaluation: null,
        nameExistsOnOtherAccount: false,
      },
    ],
  };
}

interface Harness {
  fixture: ComponentFixture<FtmoGroupSearchPageComponent>;
  el: HTMLElement;
  search: {
    startGroupSearch: ReturnType<typeof vi.fn>;
    getGroupSearch: ReturnType<typeof vi.fn>;
    getCurrentGroupSearch: ReturnType<typeof vi.fn>;
    cancelGroupSearch: ReturnType<typeof vi.fn>;
  };
  component: FtmoGroupSearchPageComponent;
}

function setup(
  o: {
    current?: FtmoGroupSearchJobDto | null;
    poll?: () => Observable<FtmoGroupSearchJobDto>;
    start?: () => Observable<FtmoGroupSearchStartOutcome>;
    cancel?: () => Observable<void>;
    needsFx?: boolean;
    lang?: 'en' | 'es';
  } = {},
): Harness {
  const search = {
    startGroupSearch: vi.fn(o.start ?? (() => new Subject<FtmoGroupSearchStartOutcome>())),
    getGroupSearch: vi.fn(o.poll ?? (() => of(searchJob()))),
    getCurrentGroupSearch: vi.fn(() => of(o.current ?? null)),
    cancelGroupSearch: vi.fn(o.cancel ?? (() => of(undefined as void))),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSearchPageComponent, TranslateModule.forRoot()],
    providers: [
      { provide: FtmoGroupSearchService, useValue: search },
      {
        provide: TradingAccountService,
        useValue: {
          getAll: () =>
            of([
              { id: 'a1', name: 'Other' },
              { id: 'a2', name: 'SBDEMO2' },
            ]),
        },
      },
      {
        provide: FtmoSimulationService,
        useValue: { getGroupCandidates: () => of(candidates(o.needsFx)) },
      },
    ],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(o.lang ?? 'en');
  const fixture = TestBed.createComponent(FtmoGroupSearchPageComponent);
  fixture.detectChanges();
  return {
    fixture,
    el: fixture.nativeElement as HTMLElement,
    search,
    component: fixture.componentInstance,
  };
}

/** Advances the fake clock and lets signals render. */
function tick(h: Harness, ms: number): void {
  vi.advanceTimersByTime(ms);
  h.fixture.detectChanges();
}

function enterRisk(h: Harness, value: string): void {
  const field = h.el.querySelector<HTMLInputElement>('[name="targetRiskPerTrade"]')!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
  h.fixture.detectChanges();
}

function submit(h: Harness): void {
  h.el.querySelector('form')!.dispatchEvent(new Event('submit'));
  h.fixture.detectChanges();
}

function startButton(h: Harness): HTMLButtonElement {
  return h.el.querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

describe('FtmoGroupSearchPageComponent', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  describe('init', () => {
    it('showsTheEmptyFormOn204_WithSBDEMO2Selected_AndNoProgress', () => {
      const h = setup({ current: null });
      expect(h.search.getCurrentGroupSearch).toHaveBeenCalledTimes(1);
      expect(h.el.querySelector('app-ftmo-search-progress')).toBeNull();
      expect(h.el.querySelector('form')).not.toBeNull();
      expect(h.el.querySelector<HTMLSelectElement>('select[name="account"]')!.value).toBe('a2');
      expect(h.search.getGroupSearch).not.toHaveBeenCalled();
      expect(vi.getTimerCount()).toBe(0);
    });

    it('reAttachesToARunningJobOn200_ShowsItAndResumesPolling', () => {
      const poll = vi.fn(() =>
        of(searchJob({ progress: { ...searchJob().progress, processed: 120 } })),
      );
      const h = setup({ current: searchJob({ jobId: 'running-9' }), poll });
      expect(h.el.querySelector('app-ftmo-search-progress')).not.toBeNull();
      tick(h, 1);
      expect(h.search.getGroupSearch).toHaveBeenCalledWith('running-9');
      tick(h, 1000);
      expect(h.search.getGroupSearch).toHaveBeenCalledTimes(2);
    });

    it('showsATerminalJobOn200_WithoutPolling', () => {
      const h = setup({ current: searchJob({ status: FtmoGroupSearchStatus.Completed }) });
      expect(h.el.querySelector('app-ftmo-search-progress')).not.toBeNull();
      tick(h, 5000);
      expect(h.search.getGroupSearch).not.toHaveBeenCalled();
      expect(vi.getTimerCount()).toBe(0);
    });

    it('showsTheFxInputsWhenThePoolHasANonUsdStrategy', () => {
      const h = setup({ needsFx: true });
      expect(h.el.querySelector('[name="fxLow"]')).not.toBeNull();
      expect(setup({ needsFx: false }).el.querySelector('[name="fxLow"]')).toBeNull();
    });
  });

  describe('start and polling', () => {
    it('startIsDisabledWithoutARisk_AndEnabledOnceTyped', () => {
      const h = setup();
      expect(startButton(h).disabled).toBe(true);
      enterRisk(h, '25');
      expect(startButton(h).disabled).toBe(false);
    });

    it('on202SendsTheBody_ShowsTheJob_AndPollsEverySecondUntilTerminal', () => {
      let n = 0;
      const poll = vi.fn(() => {
        n++;
        return of(
          searchJob({
            status: n >= 3 ? FtmoGroupSearchStatus.Completed : FtmoGroupSearchStatus.Running,
            progress: { ...searchJob().progress, processed: n * 50 },
          }),
        );
      });
      const h = setup({
        poll,
        start: () => of({ outcome: 'started', jobId: 'job-1', job: searchJob() }),
      });
      enterRisk(h, '25');
      submit(h);
      expect(h.search.startGroupSearch).toHaveBeenCalledTimes(1);
      const body = h.search.startGroupSearch.mock.calls[0][0];
      expect(body).toMatchObject({
        tradingAccountId: 'a2',
        targetRiskPerTrade: 25,
        eliminationCeiling: 0.05,
        maxPerInstrument: 2,
        includeIdenticalDeployEval: false,
        broker: 'FTMO',
      });
      expect(h.el.querySelector('app-ftmo-search-progress')).not.toBeNull();
      tick(h, 1);
      expect(poll).toHaveBeenCalledTimes(1);
      expect(h.component.job()!.progress.processed).toBe(50);
      tick(h, 1000);
      expect(h.component.job()!.progress.processed).toBe(100);
      tick(h, 1000);
      expect(h.component.job()!.status).toBe(FtmoGroupSearchStatus.Completed);
      tick(h, 10000);
      expect(poll).toHaveBeenCalledTimes(3);
      expect(vi.getTimerCount()).toBe(0);
    });

    it('startIsDisabledWhileAJobRuns', () => {
      const h = setup({ current: searchJob() });
      enterRisk(h, '25');
      expect(startButton(h).disabled).toBe(true);
    });

    it('on409AttachesToTheRunningIdWithANotice', () => {
      const h = setup({
        start: () => of({ outcome: 'alreadyRunning', runningJobId: 'other-7' }),
      });
      enterRisk(h, '25');
      submit(h);
      tick(h, 1);
      expect(h.search.getGroupSearch).toHaveBeenCalledWith('other-7');
      const notice = h.el.querySelector('.ftmo-search-page__notice')!.textContent;
      expect(notice).toContain(
        (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['NOTICE']['ALREADY_RUNNING'],
      );
      expect(h.el.querySelector('app-ftmo-search-progress')).not.toBeNull();
    });

    it('on400ShowsATranslatedErrorWithTheServerDetail', () => {
      const h = setup({
        start: () => throwError(() => ({ key: 'ERRORS.INVALID_QUERY', detail: 'risk too high' })),
      });
      enterRisk(h, '25');
      submit(h);
      const alert = h.el.querySelector('[role="alert"]')!.textContent!;
      expect(alert).toContain('risk too high');
      expect(alert).not.toContain('{{');
      expect(alert).not.toContain('ERRORS.');
      expect(h.el.querySelector('app-ftmo-search-progress')).toBeNull();
      expect(startButton(h).disabled).toBe(false);
    });

    it('a404OnPollShowsTheLostNotice_StopsPolling_AndEnablesStart', () => {
      const h = setup({
        current: searchJob(),
        poll: () => throwError(() => LOST),
      });
      enterRisk(h, '25');
      tick(h, 1);
      const text = h.el.textContent!;
      expect(text).toContain((en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['ERRORS']['LOST']);
      expect(h.el.querySelector('app-ftmo-search-progress')).toBeNull();
      expect(startButton(h).disabled).toBe(false);
      tick(h, 5000);
      expect(h.search.getGroupSearch).toHaveBeenCalledTimes(1);
      expect(vi.getTimerCount()).toBe(0);
    });
  });

  describe('cancel', () => {
    it('sendsDelete_ShowsCancelled_StopsPolling_AndKeepsThePartialRows', () => {
      const rows = [searchRow({ rank: 1 })];
      const h = setup({ current: searchJob({ rows }), poll: () => of(searchJob({ rows })) });
      tick(h, 1);
      h.el.querySelector<HTMLButtonElement>('.ftmo-search-progress__cancel')!.click();
      h.fixture.detectChanges();
      expect(h.search.cancelGroupSearch).toHaveBeenCalledWith('job-1');
      expect(h.search.cancelGroupSearch).toHaveBeenCalledTimes(1);
      expect(h.component.job()!.status).toBe(FtmoGroupSearchStatus.Cancelled);
      expect(h.component.job()!.rows).toBe(rows);
      expect(h.el.textContent).toContain(
        (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['PROGRESS']['CANCELLED_MESSAGE'],
      );
      const polls = h.search.getGroupSearch.mock.calls.length;
      tick(h, 5000);
      expect(h.search.getGroupSearch).toHaveBeenCalledTimes(polls);
      expect(vi.getTimerCount()).toBe(0);
    });
  });

  describe('slow polls', () => {
    it('aPollSlowerThanThePeriodStillUpdatesTheUi_WithoutOverlappingRequests', () => {
      let n = 0;
      let inFlight = 0;
      let maxInFlight = 0;
      const poll = vi.fn(() => {
        n++;
        inFlight++;
        maxInFlight = Math.max(maxInFlight, inFlight);
        const job = searchJob({ progress: { ...searchJob().progress, processed: n * 10 } });
        return new Observable<FtmoGroupSearchJobDto>((sub) => {
          const t = setTimeout(() => {
            inFlight--;
            sub.next(job);
            sub.complete();
          }, 2500);
          return () => clearTimeout(t);
        });
      });
      const h = setup({ current: searchJob(), poll });
      tick(h, 1);
      expect(poll).toHaveBeenCalledTimes(1);
      tick(h, 2000);
      expect(poll).toHaveBeenCalledTimes(1);
      tick(h, 600);
      expect(h.component.job()!.progress.processed).toBe(10);
      tick(h, 4000);
      expect(h.component.job()!.progress.processed).toBeGreaterThanOrEqual(20);
      expect(maxInFlight).toBe(1);
      h.fixture.destroy();
    });
  });

  describe('cancel snapshot', () => {
    it('afterDeleteFetchesTheFinalSnapshotOnce_WithServerRowsAndNotComputed', () => {
      const rows = [searchRow({ rank: 1 })];
      const finalRows = [searchRow({ rank: 1 }), searchRow({ rank: 2 })];
      let cancelled = false;
      const poll = vi.fn(() =>
        of(
          cancelled
            ? searchJob({
                status: FtmoGroupSearchStatus.Cancelled,
                rows: finalRows,
                notComputed: 7,
              })
            : searchJob({ rows }),
        ),
      );
      const h = setup({
        current: searchJob({ rows }),
        poll,
        cancel: () => {
          cancelled = true;
          return of(undefined as void);
        },
      });
      tick(h, 1);
      const before = poll.mock.calls.length;
      h.el.querySelector<HTMLButtonElement>('.ftmo-search-progress__cancel')!.click();
      h.fixture.detectChanges();
      expect(poll).toHaveBeenCalledTimes(before + 1);
      expect(poll).toHaveBeenLastCalledWith('job-1');
      expect(h.component.job()!.status).toBe(FtmoGroupSearchStatus.Cancelled);
      expect(h.component.job()!.rows).toBe(finalRows);
      expect(h.component.job()!.notComputed).toBe(7);
      tick(h, 5000);
      expect(poll).toHaveBeenCalledTimes(before + 1);
      expect(vi.getTimerCount()).toBe(0);
    });

    it('aFailedFinalFetchKeepsTheLocalCancelledState', () => {
      const rows = [searchRow({ rank: 1 })];
      let cancelled = false;
      const h = setup({
        current: searchJob({ rows }),
        poll: () =>
          cancelled
            ? throwError(() => ({ key: 'ERRORS.REQUEST_FAILED', detail: null }))
            : of(searchJob({ rows })),
        cancel: () => {
          cancelled = true;
          return of(undefined as void);
        },
      });
      tick(h, 1);
      h.el.querySelector<HTMLButtonElement>('.ftmo-search-progress__cancel')!.click();
      h.fixture.detectChanges();
      expect(h.component.job()!.status).toBe(FtmoGroupSearchStatus.Cancelled);
      expect(h.component.job()!.rows).toBe(rows);
      expect(h.el.querySelector('[role="alert"]')).toBeNull();
    });
  });

  describe('requested ceiling', () => {
    it('theTableDefaultsToTheCeilingTheSearchWasStartedWith', () => {
      const h = setup({
        poll: () => new Subject<FtmoGroupSearchJobDto>(),
        start: () =>
          of({
            outcome: 'started',
            jobId: 'job-1',
            job: searchJob({ request: searchRequest({ eliminationCeiling: 0.03 }) }),
          }),
      });
      enterRisk(h, '25');
      const ceiling = h.el.querySelector<HTMLInputElement>('[name="ceilingPercent"]')!;
      ceiling.value = '3';
      ceiling.dispatchEvent(new Event('input'));
      h.fixture.detectChanges();
      submit(h);
      expect(h.search.startGroupSearch.mock.calls[0][0].eliminationCeiling).toBe(0.03);
      const table = h.el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
      expect(table.value).toBe('3');
      h.fixture.destroy();
    });

    it('aReattachedJobTakesTheCeilingFromItsRequest_FractionToPercentOnce', () => {
      const h = setup({
        current: searchJob({ request: searchRequest({ eliminationCeiling: 0.07 }) }),
        poll: () => new Subject<FtmoGroupSearchJobDto>(),
      });
      const table = h.el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
      expect(table.value).toBe('7');
      h.fixture.destroy();
    });

    it('aRequestWithoutCeilingFallsBackToTheDefault', () => {
      const h = setup({
        current: searchJob({ request: searchRequest({ eliminationCeiling: null }) }),
        poll: () => new Subject<FtmoGroupSearchJobDto>(),
      });
      const table = h.el.querySelector<HTMLInputElement>('.ftmo-search-table__ceiling-input')!;
      expect(table.value).toBe('5');
      h.fixture.destroy();
    });
  });

  describe('leaving the page', () => {
    it('destroyStopsPollingAndSendsNoDelete', () => {
      const h = setup({ current: searchJob() });
      tick(h, 1);
      tick(h, 1000);
      const polls = h.search.getGroupSearch.mock.calls.length;
      expect(polls).toBeGreaterThan(1);
      h.fixture.destroy();
      expect(vi.getTimerCount()).toBe(0);
      vi.advanceTimersByTime(10000);
      expect(h.search.getGroupSearch).toHaveBeenCalledTimes(polls);
      expect(h.search.cancelGroupSearch).not.toHaveBeenCalled();
    });

    it('destroyWhileAStartIsInFlightSendsNoDelete', () => {
      const h = setup();
      enterRisk(h, '25');
      submit(h);
      h.fixture.destroy();
      expect(h.search.cancelGroupSearch).not.toHaveBeenCalled();
    });
  });

  describe('real dictionaries', () => {
    it.each(['en', 'es'] as const)('everyPageStateHasNoBracesNoRawKeys_%s', (lang) => {
      const states: NonNullable<Parameters<typeof setup>[0]>[] = [
        { current: null },
        { current: searchJob() },
        { current: searchJob({ status: FtmoGroupSearchStatus.Completed }) },
        { current: searchJob({ status: FtmoGroupSearchStatus.Failed, errorMessage: 'bad' }) },
        { current: searchJob(), poll: () => throwError(() => LOST) },
        {
          start: () => throwError(() => ({ key: 'ERRORS.REQUEST_FAILED', detail: null })),
        },
        { start: () => of({ outcome: 'alreadyRunning', runningJobId: 'x' }) },
      ];
      for (const state of states) {
        const h = setup({ ...state, lang });
        enterRisk(h, '25');
        if (state.start) submit(h);
        tick(h, 1);
        const text = h.el.textContent ?? '';
        expect(text).not.toContain('{{');
        expect(text).not.toContain('SIMULATOR.');
        expect(text).not.toContain('ERRORS.');
        h.fixture.destroy();
      }
    });
  });
});
