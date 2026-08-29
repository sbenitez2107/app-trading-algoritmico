import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CreatePortfolioModalComponent } from './create-portfolio-modal.component';
import {
  AccountType,
  CreatePortfolioDto,
  PortfolioDto,
  PortfolioService,
} from '../../../core/services/portfolio.service';

describe('CreatePortfolioModalComponent', () => {
  let serviceMock: { create: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    serviceMock = { create: vi.fn() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [CreatePortfolioModalComponent],
      providers: [{ provide: PortfolioService, useValue: serviceMock }],
    });
  });

  function create(strategyIds: string[] = ['s-1', 's-2']) {
    const fixture = TestBed.createComponent(CreatePortfolioModalComponent);
    fixture.componentRef.setInput('strategyIds', strategyIds);
    fixture.componentRef.setInput('broker', 'Darwinex');
    fixture.componentRef.setInput('accountType', AccountType.Demo);
    fixture.detectChanges();
    return fixture;
  }

  it('defaultsTheCapitalAndLeavesTheNameEmpty', () => {
    const cmp = create().componentInstance;
    expect(cmp.form.controls.name.value).toBe('');
    expect(cmp.form.controls.initialCapital.value).toBe(100000);
  });

  it('doesNotSubmitWithoutAName', () => {
    const cmp = create().componentInstance;

    cmp.submit();

    expect(serviceMock.create).not.toHaveBeenCalled();
    expect(cmp.errorMessage()).toBeTruthy();
  });

  it('doesNotSubmitWithNonPositiveCapital', () => {
    const cmp = create().componentInstance;

    cmp.form.controls.name.setValue('NQ H4');
    cmp.form.controls.initialCapital.setValue(0);
    cmp.submit();

    expect(serviceMock.create).not.toHaveBeenCalled();
  });

  it('sendsEveryStrategyAsAnEquallyWeightedMember', () => {
    serviceMock.create.mockReturnValue(of({ id: 'p-1' } as PortfolioDto));
    const cmp = create(['s-1', 's-2', 's-3']).componentInstance;

    cmp.form.controls.name.setValue('  NQ H4  ');
    cmp.submit();

    const dto = serviceMock.create.mock.calls[0][0] as CreatePortfolioDto;
    expect(dto.name).toBe('NQ H4');
    expect(dto.broker).toBe('Darwinex');
    expect(dto.accountType).toBe(AccountType.Demo);
    expect(dto.initialCapital).toBe(100000);
    expect(dto.members).toEqual([
      { strategyId: 's-1', weight: 1 },
      { strategyId: 's-2', weight: 1 },
      { strategyId: 's-3', weight: 1 },
    ]);
  });

  it('emitsTheCreatedPortfolioOnSuccess', () => {
    serviceMock.create.mockReturnValue(of({ id: 'p-1' } as PortfolioDto));
    const fixture = create();
    const cmp = fixture.componentInstance;
    const emitted: string[] = [];
    cmp.created.subscribe((id) => emitted.push(id));

    cmp.form.controls.name.setValue('NQ H4');
    cmp.submit();

    expect(emitted).toEqual(['p-1']);
    expect(cmp.saving()).toBe(false);
  });

  it('surfacesTheServerErrorAndStaysOpen', () => {
    serviceMock.create.mockReturnValue(throwError(() => ({ error: { error: 'Name taken' } })));
    const fixture = create();
    const cmp = fixture.componentInstance;
    const emitted: string[] = [];
    cmp.created.subscribe((id) => emitted.push(id));

    cmp.form.controls.name.setValue('NQ H4');
    cmp.submit();

    expect(cmp.errorMessage()).toBe('Name taken');
    expect(cmp.saving()).toBe(false);
    expect(emitted).toEqual([]);
  });

  it('doesNotFireASecondRequestWhileOneIsInFlight', () => {
    // A pending observable: the first submit stays in flight.
    serviceMock.create.mockReturnValue({ subscribe: () => ({ unsubscribe: () => {} }) });
    const cmp = create().componentInstance;

    cmp.form.controls.name.setValue('NQ H4');
    cmp.submit();
    cmp.submit();

    expect(serviceMock.create).toHaveBeenCalledTimes(1);
  });
});
