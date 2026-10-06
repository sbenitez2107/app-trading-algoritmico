import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import { FtmoGroupCandidatesDto } from '../../../core/models/ftmo-group-simulation.model';
import { FtmoSimulationService } from '../../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const NOTICES = (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['DEEPLINK'];
const NOTICES_ES = (es as JsonTree)['SIMULATOR']['FTMO_GROUP']['DEEPLINK'];

function candidatesFor(account: string, maxMembers = 3): FtmoGroupCandidatesDto {
  return {
    tradingAccountId: account,
    maxMembers,
    candidates: ['s1', 's2', 's3', 's4', 's5'].map((id) => ({
      strategyId: id,
      name: `Name ${id}`,
      symbol: 'XAUUSD',
      deploy: null,
      evaluation: null,
      nameExistsOnOtherAccount: false,
    })),
  };
}

interface Opts {
  params?: Record<string, string> | null;
  maxMembers?: number;
  lang?: 'en' | 'es';
  withRoute?: boolean;
  failFirstLoad?: boolean;
}

function create(o: Opts = {}) {
  let calls = 0;
  const getGroupCandidates = vi.fn((id: string) =>
    o.failFirstLoad && calls++ === 0
      ? throwError(() => new Error('boom'))
      : of(candidatesFor(id, o.maxMembers)),
  );
  const simulateGroup = vi.fn(() => of({}));
  const providers: unknown[] = [
    {
      provide: TradingAccountService,
      useValue: {
        getAll: () =>
          of([
            { id: 'a', name: 'Other' },
            { id: 'b', name: 'SBDEMO2' },
          ]),
      },
    },
    { provide: FtmoSimulationService, useValue: { getGroupCandidates, simulateGroup } },
  ];
  if (o.withRoute !== false && o.params !== null) {
    providers.push({
      provide: ActivatedRoute,
      useValue: { snapshot: { queryParamMap: convertToParamMap(o.params ?? {}) } },
    });
  }
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
    providers: providers as never[],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(o.lang ?? 'en');
  const fixture: ComponentFixture<FtmoGroupSimulationPageComponent> = TestBed.createComponent(
    FtmoGroupSimulationPageComponent,
  );
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    el,
    component: fixture.componentInstance,
    getGroupCandidates,
    simulateGroup,
    notices: () =>
      Array.from(el.querySelectorAll('.ftmo-group-page__deeplink-notice')).map((n) =>
        (n.textContent ?? '').trim(),
      ),
    selected: () => [...fixture.componentInstance.selectedIds()].sort(),
    runButton: () => el.querySelector<HTMLButtonElement>('button[type="submit"]')!,
    accountSelect: () => el.querySelector<HTMLSelectElement>('select.ftmo-group-page__account')!,
  };
}

const VALID = {
  account: 'a',
  members: 's2,s1',
  risk: '0.5',
  capital: '25000',
  fxLow: '1.05',
  fxHigh: '1.2',
};

describe('FtmoGroupSimulationPage deep link', () => {
  it('preselectsTheAccountMembersRiskCapitalAndFx_WithoutRunning', () => {
    const h = create({ params: VALID });
    expect(h.getGroupCandidates).toHaveBeenCalledTimes(1);
    expect(h.getGroupCandidates).toHaveBeenCalledWith('a');
    expect(h.accountSelect().value).toBe('a');
    expect(h.selected()).toEqual(['s1', 's2']);
    const form = h.component.form();
    expect(form.targetRiskPerTrade).toBe(0.5);
    expect(form.initialCapital).toBe(25000);
    expect(form.fxLow).toBe(1.05);
    expect(form.fxHigh).toBe(1.2);
    expect(h.notices()).toEqual([]);
    // Run is ready but nothing was requested and no result is shown.
    expect(h.runButton().disabled).toBe(false);
    expect(h.simulateGroup).not.toHaveBeenCalled();
    expect(h.component.result()).toBeNull();
    expect(h.component.running()).toBe(false);
  });

  it('showsTheCheckedMembersInThePicker', () => {
    const h = create({ params: VALID });
    const checked = h.el.querySelectorAll('tbody input[type="checkbox"]:checked');
    expect(checked).toHaveLength(2);
  });

  it('dropsUnknownIdsWithATranslatedNoticeNamingTheCount', () => {
    const h = create({ params: { ...VALID, members: 's1,zzz,nope,s2' } });
    expect(h.selected()).toEqual(['s1', 's2']);
    expect(h.notices()).toEqual([String(NOTICES['UNKNOWN_MEMBERS']).replace('{{count}}', '2')]);
  });

  it('truncatesAnOverCapListInLinkOrder_UsingTheCandidatesMax_WithACapNotice', () => {
    const h = create({ params: { ...VALID, members: 's5,s4,s3,s2,s1' }, maxMembers: 3 });
    expect(h.selected()).toEqual(['s3', 's4', 's5']);
    expect(h.notices()).toEqual([String(NOTICES['OVER_CAP']).replaceAll('{{max}}', '3')]);
  });

  it('anUnknownAccountFallsBackToTheDefault_DropsTheMembers_AndSaysSo', () => {
    const h = create({ params: { ...VALID, account: 'ghost' } });
    expect(h.getGroupCandidates).toHaveBeenCalledTimes(1);
    expect(h.getGroupCandidates).toHaveBeenCalledWith('b');
    expect(h.selected()).toEqual([]);
    expect(h.notices()).toEqual([NOTICES['ACCOUNT_FALLBACK']]);
    expect(h.component.form().targetRiskPerTrade).toBe(0.5);
  });

  it('aMissingAccountBehavesTheSameWay', () => {
    const h = create({ params: { members: 's1' } });
    expect(h.getGroupCandidates).toHaveBeenCalledWith('b');
    expect(h.selected()).toEqual([]);
    expect(h.notices()).toEqual([NOTICES['ACCOUNT_FALLBACK']]);
  });

  it.each(['-5', 'abc', '0'])(
    'anInvalidRiskOf_%s_LeavesTheFieldEmpty_WithANotice_AndRunDisabled',
    (risk) => {
      const h = create({ params: { ...VALID, risk } });
      expect(h.component.form().targetRiskPerTrade).toBeNull();
      const field = h.el.querySelector<HTMLInputElement>('input[name="targetRiskPerTrade"]')!;
      expect(field.value).toBe('');
      expect(h.notices()).toEqual([NOTICES['INVALID_VALUES']]);
      expect(h.selected()).toEqual(['s1', 's2']);
      expect(h.runButton().disabled).toBe(true);
    },
  );

  it('emptyMembersLeaveNoSelection', () => {
    const h = create({ params: { ...VALID, members: '' } });
    expect(h.selected()).toEqual([]);
    expect(h.notices()).toEqual([]);
  });

  it('theLinkIsAppliedOnce_EditsAndAccountChangesAreNotOverwritten', () => {
    const h = create({ params: VALID });
    h.component.onFormChange({ ...h.component.form(), targetRiskPerTrade: 2 });
    h.component.onSelectionChange(new Set(['s3']));
    h.fixture.detectChanges();
    expect(h.component.form().targetRiskPerTrade).toBe(2);
    expect(h.selected()).toEqual(['s3']);
    h.component.selectAccount('b');
    h.fixture.detectChanges();
    expect(h.selected()).toEqual([]);
    expect(h.component.form().targetRiskPerTrade).toBe(2);
    expect(h.component.form().initialCapital).toBe(25000);
    h.component.selectAccount('a');
    h.fixture.detectChanges();
    expect(h.selected()).toEqual([]);
    expect(h.simulateGroup).not.toHaveBeenCalled();
  });

  it.each([
    ['en', NOTICES],
    ['es', NOTICES_ES],
  ] as const)('noticesUseTheRealDictionary_NoBracesNoRawKey_%s', (lang, dict) => {
    const h = create({
      params: { ...VALID, members: 's5,s4,s3,s2,zzz', risk: 'abc' },
      maxMembers: 2,
      lang,
    });
    const text = h.notices().join(' | ');
    expect(h.notices()).toHaveLength(3);
    expect(text).not.toContain('{{');
    expect(text).not.toMatch(/SIMULATOR\./);
    expect(text).toContain(String(dict['INVALID_VALUES']));
  });

  it('theTwoLocalesRenderDifferentNotices', () => {
    const params = { ...VALID, members: 'zzz,s1' };
    expect(create({ params, lang: 'en' }).notices()).not.toEqual(
      create({ params, lang: 'es' }).notices(),
    );
  });

  it('withoutARouterTheDefaultBehaviourIsUnchanged', () => {
    const h = create({ params: null });
    expect(h.getGroupCandidates).toHaveBeenCalledWith('b');
    expect(h.selected()).toEqual([]);
    expect(h.notices()).toEqual([]);
    expect(h.component.form().targetRiskPerTrade).toBeNull();
  });

  it('aFailedFirstLoadConsumesTheLink_ALaterLoadDoesNotOverwriteTheForm', () => {
    const h = create({ params: VALID, failFirstLoad: true });
    expect(h.getGroupCandidates).toHaveBeenCalledWith('a');
    expect(h.component.error()).not.toBeNull();
    h.component.selectAccount('b');
    h.fixture.detectChanges();
    expect(h.getGroupCandidates).toHaveBeenCalledTimes(2);
    expect(h.selected()).toEqual([]);
    expect(h.component.form().targetRiskPerTrade).toBeNull();
    expect(h.component.form().initialCapital).not.toBe(25000);
    expect(h.notices()).toEqual([]);
  });

  it('aRouteWithoutParamsAlsoBehavesAsBefore', () => {
    const h = create({ params: {} });
    expect(h.getGroupCandidates).toHaveBeenCalledWith('b');
    expect(h.notices()).toEqual([]);
  });
});
