import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidatesDto,
} from '../../../core/models/ftmo-group-simulation.model';
import {
  FtmoRequestError,
  FtmoSimulationService,
} from '../../../core/services/ftmo-simulation.service';
import {
  TradingAccountDto,
  TradingAccountService,
} from '../../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

function account(id: string, name: string): TradingAccountDto {
  return { id, name } as TradingAccountDto;
}

function strategy(strategyId: string, name: string): FtmoGroupCandidateDto {
  return {
    strategyId,
    name,
    symbol: 'XAUUSD',
    deploy: null,
    evaluation: null,
    nameExistsOnOtherAccount: false,
  };
}

function response(
  tradingAccountId: string,
  maxMembers: number,
  ...names: string[]
): FtmoGroupCandidatesDto {
  return {
    tradingAccountId,
    maxMembers,
    candidates: names.map((n, i) => strategy(`${tradingAccountId}-${i}`, n)),
  };
}

describe('FtmoGroupSimulationPageComponent picker wiring', () => {
  let getGroupCandidates: ReturnType<typeof vi.fn>;

  function create(
    accounts: TradingAccountDto[],
    candidatesFor: (id: string) => unknown = () => of(response('x', 2)),
    lang: 'en' | 'es' = 'en',
  ): ComponentFixture<FtmoGroupSimulationPageComponent> {
    getGroupCandidates = vi.fn(candidatesFor);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
      providers: [
        { provide: TradingAccountService, useValue: { getAll: () => of(accounts) } },
        { provide: FtmoSimulationService, useValue: { getGroupCandidates } },
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

  function rowNames(f: ComponentFixture<unknown>): string[] {
    return Array.from(el(f).querySelectorAll('tbody tr')).map((r) => r.textContent ?? '');
  }

  function accountSelect(f: ComponentFixture<unknown>): HTMLSelectElement {
    return el(f).querySelector<HTMLSelectElement>('select.ftmo-group-page__account')!;
  }

  it('selectsTheAccountNamedSBDEMO2ByDefaultAndLoadsItsCandidates', () => {
    const f = create([account('a', 'Other'), account('b', 'SBDEMO2')], (id) =>
      of(response(id, 4, 'Only On B')),
    );
    expect(getGroupCandidates).toHaveBeenCalledTimes(1);
    expect(getGroupCandidates).toHaveBeenCalledWith('b');
    expect(accountSelect(f).value).toBe('b');
    expect(rowNames(f)).toHaveLength(1);
    expect(rowNames(f)[0]).toContain('Only On B');
  });

  it('withoutSBDEMO2_SelectsTheFirstAccount', () => {
    const f = create([account('a', 'One'), account('b', 'Two')], (id) => of(response(id, 4, 'S')));
    expect(getGroupCandidates).toHaveBeenCalledWith('a');
    expect(accountSelect(f).value).toBe('a');
  });

  it('withNoAccounts_ShowsTheTranslatedMessageAndRequestsNothing', () => {
    const f = create([]);
    expect(getGroupCandidates).not.toHaveBeenCalled();
    expect(el(f).textContent).toContain(
      (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['PICKER']['NO_ACCOUNTS'],
    );
  });

  it('listsOnlyTheSelectedAccountsRows_AndSwitchingReloadsAndClearsTheSelection', () => {
    const f = create([account('a', 'SBDEMO2'), account('b', 'Other')], (id) =>
      of(id === 'a' ? response('a', 3, 'Alpha One', 'Alpha Two') : response('b', 3, 'Beta')),
    );
    expect(rowNames(f)).toHaveLength(2);

    const box = el(f).querySelector<HTMLInputElement>('tbody input[type="checkbox"]')!;
    box.click();
    f.detectChanges();
    expect(el(f).textContent).toContain('1 of 3 selected');

    const select = accountSelect(f);
    select.value = 'b';
    select.dispatchEvent(new Event('change'));
    f.detectChanges();

    expect(getGroupCandidates).toHaveBeenCalledTimes(2);
    expect(getGroupCandidates).toHaveBeenLastCalledWith('b');
    expect(rowNames(f)).toHaveLength(1);
    expect(rowNames(f)[0]).toContain('Beta');
    expect(el(f).textContent).toContain('0 of 3 selected');
  });

  it('theCapShownIsTheOneTheCandidatesResponseSupplied', () => {
    const f = create([account('a', 'SBDEMO2')], (id) => of(response(id, 1, 'One', 'Two')));
    const boxes = Array.from(
      el(f).querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]'),
    );
    boxes[0].click();
    f.detectChanges();
    expect(el(f).textContent).toContain('a group has at most 1 strategies');
    const after = Array.from(
      el(f).querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]'),
    );
    expect(after[1].disabled).toBe(true);
  });

  it('aSlowerStaleResponseIsDroppedWhenTheUserSwitchesAccount', () => {
    const first = new Subject<FtmoGroupCandidatesDto>();
    const f = create([account('a', 'SBDEMO2'), account('b', 'Other')], (id) =>
      id === 'a' ? first : of(response('b', 2, 'Beta')),
    );
    const select = accountSelect(f);
    select.value = 'b';
    select.dispatchEvent(new Event('change'));
    f.detectChanges();
    first.next(response('a', 2, 'Stale Alpha'));
    f.detectChanges();
    expect(rowNames(f)).toHaveLength(1);
    expect(rowNames(f)[0]).toContain('Beta');
  });

  it('aFailedLoadShowsATranslatedErrorAndNoRows_WithTheDetailParamFilled', () => {
    const failure: FtmoRequestError = { key: 'ERRORS.INVALID_QUERY', detail: 'bad account' };
    const f = create([account('a', 'SBDEMO2')], () => throwError(() => failure));
    const text = el(f).textContent ?? '';
    expect(text).toContain('The request was rejected: bad account');
    expect(text).not.toContain('{{');
    expect(text).not.toContain('FTMO_SIMULATION.');
    expect(rowNames(f)).toHaveLength(0);
  });

  it('selectingRowsNeverCallsAnythingButTheCandidatesRead', () => {
    const simulate = vi.fn();
    const f = create([account('a', 'SBDEMO2')], (id) => of(response(id, 3, 'One', 'Two')));
    const svc = TestBed.inject(FtmoSimulationService) as unknown as { simulateGroup?: unknown };
    svc.simulateGroup = simulate;
    el(f).querySelector<HTMLInputElement>('tbody input[type="checkbox"]')!.click();
    f.detectChanges();
    expect(simulate).not.toHaveBeenCalled();
    expect(getGroupCandidates).toHaveBeenCalledTimes(1);
  });

  it('rendersTheAccountLabelInSpanishWithNoLeak', () => {
    const f = create([account('a', 'SBDEMO2')], (id) => of(response(id, 3, 'One')), 'es');
    const text = el(f).textContent ?? '';
    expect(text).toContain((es as JsonTree)['SIMULATOR']['FTMO_GROUP']['PICKER']['ACCOUNT_LABEL']);
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  });
});
