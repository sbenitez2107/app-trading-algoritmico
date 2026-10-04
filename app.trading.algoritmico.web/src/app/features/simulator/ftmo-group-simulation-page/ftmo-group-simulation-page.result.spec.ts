import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidatesDto,
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
} from '../../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { FtmoSimulationService } from '../../../core/services/ftmo-simulation.service';
import {
  TradingAccountDto,
  TradingAccountService,
} from '../../../core/services/trading-account.service';
import {
  THREE_MEMBERS,
  diagnosticsFixture,
  groupResult,
  groupWideRefusal,
  refusedKind,
  successKind,
} from '../ftmo-group-simulation.result.fixtures';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

function candidate(id: string): FtmoGroupCandidateDto {
  const run = {
    runId: `r-${id}`,
    symbol: 'EURUSD',
    tradeCount: 5,
    firstOpen: '2020-01-01T00:00:00',
    lastClose: '2021-01-01T00:00:00',
    hasInstrumentSpec: true,
    isCalibrated: true,
    profitCurrency: 'USD',
    needsFxBand: false,
    sourceTimeZoneId: 'Europe/Berlin',
  };
  return {
    strategyId: id,
    name: `Name ${id}`,
    symbol: 'EURUSD',
    deploy: run,
    evaluation: run,
    nameExistsOnOtherAccount: false,
  };
}

const CANDIDATES: FtmoGroupCandidatesDto = {
  tradingAccountId: 'acc',
  maxMembers: 4,
  candidates: [candidate('a'), candidate('b')],
};

function create(
  simulate: () => Observable<FtmoGroupSimulationDto>,
  lang: 'en' | 'es' = 'en',
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
        useValue: { getGroupCandidates: () => of(CANDIDATES), simulateGroup: vi.fn(simulate) },
      },
    ],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture = TestBed.createComponent(FtmoGroupSimulationPageComponent);
  fixture.detectChanges();
  return fixture;
}

function el(f: ComponentFixture<unknown>): HTMLElement {
  return f.nativeElement as HTMLElement;
}

function run(f: ComponentFixture<FtmoGroupSimulationPageComponent>): void {
  f.componentInstance.onSelectionChange(new Set(['a', 'b']));
  f.detectChanges();
  const risk = el(f).querySelector<HTMLInputElement>('input[name="targetRiskPerTrade"]')!;
  risk.value = '25';
  risk.dispatchEvent(new Event('input'));
  f.detectChanges();
  el(f)
    .querySelector('form')!
    .dispatchEvent(new Event('submit', { cancelable: true }));
  f.detectChanges();
}

const HINT = 'Enter the risk per trade and press Run to see results.';
const OK = groupResult([
  successKind(BacktestRunKind.Deploy),
  refusedKind(BacktestRunKind.Evaluation, FtmoGroupRefusal.NoCommonWindow),
]);

describe('FtmoGroupSimulationPageComponent result area (F3.4)', () => {
  it('showsOneHintBeforeTheFirstRun_AndNoResultArea', () => {
    const f = create(() => of(OK));
    expect(el(f).textContent).toContain(HINT);
    expect(el(f).querySelector('app-ftmo-group-result')).toBeNull();
    expect(el(f).textContent?.split(HINT).length).toBe(2);
  });

  it('rendersTheResultAfterRun_AndTheHintGoesAway', () => {
    const f = create(() => of(OK));
    run(f);
    expect(el(f).querySelector('app-ftmo-group-result')).not.toBeNull();
    expect(el(f).querySelectorAll('app-ftmo-run-panel')).toHaveLength(1);
    expect(el(f).textContent).not.toContain(HINT);
  });

  it('rendersAGroupWideRefusalOnceAndNoPanel', () => {
    const f = create(() => of(groupWideRefusal(FtmoGroupRefusal.InvalidRequest)));
    run(f);
    expect(el(f).querySelectorAll('.ftmo-group-result__group-refusal')).toHaveLength(1);
    expect(el(f).querySelector('app-ftmo-run-panel')).toBeNull();
  });

  it('rendersOneDiagnosticsPanelPerSuccessfulKind_WithMembersNamedFromTheEnvelope', () => {
    const f = create(() =>
      of(
        groupResult(
          [
            successKind(BacktestRunKind.Deploy, diagnosticsFixture()),
            refusedKind(BacktestRunKind.Evaluation, FtmoGroupRefusal.NoCommonWindow),
          ],
          { members: THREE_MEMBERS },
        ),
      ),
    );
    run(f);
    expect(el(f).querySelectorAll('app-ftmo-group-diagnostics')).toHaveLength(1);
    expect(el(f).querySelector('.ftmo-group-diagnostics__peak-member')?.textContent).toContain(
      'Alpha',
    );
  });

  it('aFailedRunShowsTheErrorAndNeitherAResultNorTheHint', () => {
    const f = create(() => throwError(() => ({ key: 'ERRORS.REQUEST_FAILED', detail: null })));
    run(f);
    expect(el(f).querySelector('app-ftmo-group-result')).toBeNull();
    expect(el(f).querySelector('.ftmo-group-page__run-error')).not.toBeNull();
    expect(el(f).textContent).not.toContain(HINT);
  });

  it('editingTheRiskAfterARunDropsTheResultAndBringsTheHintBack', () => {
    const f = create(() => of(OK));
    run(f);
    const risk = el(f).querySelector<HTMLInputElement>('input[name="targetRiskPerTrade"]')!;
    risk.value = '30';
    risk.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(el(f).querySelector('app-ftmo-group-result')).toBeNull();
    expect(el(f).textContent).toContain(HINT);
  });

  it('passesTheSubmittedBrokerToTheResult_SoABrokerScopedRefusalNamesIt', () => {
    const f = create(() =>
      of(
        groupWideRefusal(FtmoGroupRefusal.SharedInputsRefused, {
          sharedRefusal: 2,
        }),
      ),
    );
    run(f);
    expect(el(f).textContent).toContain("The broker 'FTMO' has no FTMO limits configured.");
  });
});
