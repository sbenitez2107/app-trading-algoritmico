import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { AccountsListComponent } from './accounts-list.component';
import {
  TradingAccountService,
  TradingAccountDto,
} from '../../../core/services/trading-account.service';

function makeAccount(id = 'acc-1', accountType: 0 | 1 = 0): TradingAccountDto {
  return {
    id,
    name: `Account ${id}`,
    broker: 'Darwinex',
    accountType,
    platform: 0,
    accountNumber: 123456,
    login: 123456,
    server: 'test-server',
    isEnabled: true,
    currency: 'USD',
    initialBalance: 100000,
    createdAt: new Date().toISOString(),
  };
}

describe('AccountsListComponent — navigate to detail', () => {
  let tradingAccountServiceMock: Partial<TradingAccountService>;
  let routerMock: Partial<Router>;

  beforeEach(() => {
    tradingAccountServiceMock = {
      getAll: vi.fn().mockReturnValue(of([makeAccount('acc-1', 0)])),
    };

    routerMock = {
      navigate: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AccountsListComponent],
      providers: [
        { provide: TradingAccountService, useValue: tradingAccountServiceMock },
        { provide: Router, useValue: routerMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { accountType: 0, broker: 'Darwinex', title: 'Demo Accounts' },
              params: {},
            },
          },
        },
      ],
    });
  });

  // --- spec R1: navigateToDetail only for demo accounts ---

  it('navigateToDetail_DemoAccount_NavigatesToDetailRoute', () => {
    // Arrange
    const fixture = TestBed.createComponent(AccountsListComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const acc = makeAccount('acc-1', 0);

    // Simulate a regular row click (not a button)
    const event = new MouseEvent('click');
    Object.defineProperty(event, 'target', {
      value: document.createElement('td'),
      writable: false,
    });

    // Act
    comp.navigateToDetail(acc, event);

    // Assert
    expect(routerMock.navigate).toHaveBeenCalledWith(['/darwinex/demo', 'acc-1']);
  });

  it('navigateToDetail_LiveAccount_DoesNotNavigate', () => {
    // Arrange
    const fixture = TestBed.createComponent(AccountsListComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const liveAcc = makeAccount('acc-2', 1);

    const event = new MouseEvent('click');
    Object.defineProperty(event, 'target', {
      value: document.createElement('td'),
      writable: false,
    });

    // Act
    comp.navigateToDetail(liveAcc, event);

    // Assert — spec: only demo accounts navigate to detail
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('navigateToDetail_ClickOnButton_DoesNotNavigate', () => {
    // Arrange
    const fixture = TestBed.createComponent(AccountsListComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    const acc = makeAccount('acc-1', 0);

    // Simulate click that originated from a button
    const event = new MouseEvent('click');
    Object.defineProperty(event, 'target', {
      value: document.createElement('button'),
      writable: false,
    });

    // Act
    comp.navigateToDetail(acc, event);

    // Assert — buttons have their own handlers; row click should be a no-op
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });
});
