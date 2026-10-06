import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from '../../../core/models/ftmo-group-search.model';
import { searchJob } from '../ftmo-group-search.fixtures';
import { FtmoSearchProgressComponent } from './ftmo-search-progress.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

function render(job: FtmoGroupSearchJobDto, lang: 'en' | 'es' = 'en') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoSearchProgressComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture: ComponentFixture<FtmoSearchProgressComponent> = TestBed.createComponent(
    FtmoSearchProgressComponent,
  );
  fixture.componentRef.setInput('job', job);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, text: el.textContent ?? '' };
}

const P = (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['PROGRESS'];
const STATUS = (en as JsonTree)['SIMULATOR']['FTMO_SEARCH']['STATUS'];

describe('FtmoSearchProgressComponent', () => {
  it('showsTheBarAndEveryFunnelCount', () => {
    const { el, text } = render(searchJob());
    const bar = el.querySelector<HTMLElement>('[role="progressbar"]')!;
    expect(bar.getAttribute('aria-valuenow')).toBe('20');
    for (const n of ['12,926', '11', '22', '33', '44', '55', '75']) expect(text).toContain(n);
    expect(text).toContain('1:05');
    expect(text).toContain('40');
    expect(text).toContain('200');
    expect(text).toContain('3 of 75');
  });

  it('showsTheStage_AndFullSimulationsDoneVersusBudget', () => {
    const { text } = render(searchJob());
    expect(text).toContain('Scoring candidates');
    expect(text).toContain('3 of 75');
  });

  it('cancelIsEnabledOnlyWhileRunning', () => {
    const running = render(searchJob()).el.querySelector<HTMLButtonElement>(
      '.ftmo-search-progress__cancel',
    );
    expect(running?.disabled).toBe(false);
    for (const status of [
      FtmoGroupSearchStatus.Completed,
      FtmoGroupSearchStatus.StoppedAtBudget,
      FtmoGroupSearchStatus.Cancelled,
      FtmoGroupSearchStatus.Failed,
      FtmoGroupSearchStatus.Unknown,
    ]) {
      const button = render(searchJob({ status })).el.querySelector<HTMLButtonElement>(
        '.ftmo-search-progress__cancel',
      );
      expect(button?.disabled, String(status)).toBe(true);
    }
  });

  it('cancelEmitsAnOutput', () => {
    const { fixture, el } = render(searchJob());
    let count = 0;
    fixture.componentInstance.cancel.subscribe(() => count++);
    el.querySelector<HTMLButtonElement>('.ftmo-search-progress__cancel')!.click();
    expect(count).toBe(1);
  });

  it('eachTerminalStateHasItsOwnTranslatedMessage', () => {
    const texts = new Map<string, string>();
    const cases: [string, FtmoGroupSearchJobDto][] = [
      ['completed', searchJob({ status: FtmoGroupSearchStatus.Completed })],
      [
        'stopped',
        searchJob({
          status: FtmoGroupSearchStatus.StoppedAtBudget,
          stopReason: FtmoGroupSearchStopReason.WallClock,
          notComputed: 37,
        }),
      ],
      ['cancelled', searchJob({ status: FtmoGroupSearchStatus.Cancelled })],
      [
        'failed',
        searchJob({ status: FtmoGroupSearchStatus.Failed, errorMessage: 'Pool too large (30)' }),
      ],
    ];
    for (const [name, job] of cases) {
      const { el } = render(job);
      const message = el.querySelector('.ftmo-search-progress__terminal')?.textContent?.trim();
      expect(message, name).toBeTruthy();
      texts.set(name, message!);
    }
    expect(new Set(texts.values()).size).toBe(4);
  });

  it('aBudgetStopNamesTheLimitAndTheNotComputedCount_AndIsNotShownAsCompleted', () => {
    const { el, text } = render(
      searchJob({
        status: FtmoGroupSearchStatus.StoppedAtBudget,
        stopReason: FtmoGroupSearchStopReason.MaxFullSimulations,
        notComputed: 37,
      }),
    );
    const message = el.querySelector('.ftmo-search-progress__terminal')!.textContent!;
    expect(message).toContain('Simulation limit reached');
    expect(message).toContain('37');
    expect(message).not.toContain(P['COMPLETED_MESSAGE']);
    expect(text).toContain(STATUS['STOPPED_AT_BUDGET']);
  });

  it('aFailedJobShowsItsRefusalMessage', () => {
    const { el } = render(
      searchJob({ status: FtmoGroupSearchStatus.Failed, errorMessage: 'Pool too large (30)' }),
    );
    expect(el.querySelector('.ftmo-search-progress__terminal')!.textContent).toContain(
      'Pool too large (30)',
    );
  });

  it('aZeroStatusRendersItsOwnLabelWithTheRawValue_NotCompletedNorAbsent', () => {
    const { el, text } = render(searchJob({ status: FtmoGroupSearchStatus.Unknown }));
    expect(text).toContain(STATUS['UNKNOWN']);
    expect(el.querySelector('.ftmo-search-progress__raw')!.textContent).toContain('0');
    expect(text).not.toContain(STATUS['COMPLETED']);
  });

  it('anUnmappedStatusShowsTheUnknownLabelWithItsRawValue', () => {
    const { el } = render(searchJob({ status: 99 as FtmoGroupSearchStatus }));
    expect(el.querySelector('.ftmo-search-progress__raw')!.textContent).toContain('99');
  });

  it('theBarIsZeroWhenTheTotalIsZero', () => {
    const base = searchJob();
    const { el } = render({ ...base, progress: { ...base.progress, processed: 0, total: 0 } });
    expect(el.querySelector('[role="progressbar"]')!.getAttribute('aria-valuenow')).toBe('0');
  });

  it('showsTheExaminedDisclosureOnce_AndNeverTheServerText', () => {
    const { text } = render(searchJob({ status: FtmoGroupSearchStatus.Completed }));
    expect(text).toContain('12,926 groups examined');
    expect(text.split('selection bias').length - 1).toBe(1);
    expect(text).not.toContain('server text that must never render');
  });

  it.each([
    ['en', en],
    ['es', es],
  ] as const)(
    'everyStateRendersWithTheRealDictionary_NoBracesNoRawKeys_%s',
    (lang, _dictionary) => {
      const states: FtmoGroupSearchJobDto[] = [
        searchJob(),
        searchJob({ status: FtmoGroupSearchStatus.Completed }),
        searchJob({
          status: FtmoGroupSearchStatus.StoppedAtBudget,
          stopReason: FtmoGroupSearchStopReason.WallClock,
          notComputed: 5,
        }),
        searchJob({ status: FtmoGroupSearchStatus.Cancelled }),
        searchJob({ status: FtmoGroupSearchStatus.Failed, errorMessage: 'x' }),
        searchJob({ status: FtmoGroupSearchStatus.Failed, errorMessage: null }),
        searchJob({ status: FtmoGroupSearchStatus.Unknown }),
        searchJob({ status: 77 as FtmoGroupSearchStatus }),
      ];
      for (const job of states) {
        const { text } = render(job, lang);
        expect(text, `${lang}:${job.status}`).not.toContain('{{');
        expect(text, `${lang}:${job.status}`).not.toContain('SIMULATOR.');
      }
    },
  );

  it('theTwoLocalesRenderDifferentText', () => {
    expect(render(searchJob(), 'en').text).not.toBe(render(searchJob(), 'es').text);
  });
});
