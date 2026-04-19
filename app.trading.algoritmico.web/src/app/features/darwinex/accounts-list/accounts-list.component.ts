import { Component, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  TradingAccountDto,
  TradingAccountService,
  AccountType,
} from '../../../core/services/trading-account.service';
import { AccountFormComponent } from '../account-form/account-form.component';

@Component({
  selector: 'app-accounts-list',
  standalone: true,
  imports: [CommonModule, AccountFormComponent],
  templateUrl: './accounts-list.component.html',
  styleUrl: './accounts-list.component.scss',
  changeDetection: ChangeDetectionStrategy.Default,
})
export class AccountsListComponent implements OnInit {
  accounts: TradingAccountDto[] = [];
  isLoading = true;
  showModal = false;
  showDeleteConfirm = false;
  selectedAccount: TradingAccountDto | null = null;
  accountToDelete: TradingAccountDto | null = null;

  broker: string = 'Darwinex';
  accountType: AccountType = 0;
  pageTitle: string = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private service: TradingAccountService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const data = this.route.snapshot.data;
    this.accountType = data['accountType'] as AccountType;
    this.broker = data['broker'] ?? 'Darwinex';
    this.pageTitle = data['title'] ?? 'Cuentas';
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.isLoading = true;
    this.service.getAll(this.broker, this.accountType).subscribe({
      next: (data) => {
        this.accounts = data;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  openCreateModal(): void {
    this.selectedAccount = null;
    this.showModal = true;
  }

  openEditModal(account: TradingAccountDto): void {
    this.selectedAccount = account;
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.selectedAccount = null;
  }

  onSaved(account: TradingAccountDto): void {
    const idx = this.accounts.findIndex((a) => a.id === account.id);
    if (idx >= 0) {
      this.accounts = this.accounts.map((a) => (a.id === account.id ? account : a));
    } else {
      this.accounts = [account, ...this.accounts];
    }
    this.closeModal();
    this.cdr.markForCheck();
  }

  toggleAccount(account: TradingAccountDto): void {
    this.service.toggle(account.id).subscribe({
      next: () => {
        this.accounts = this.accounts.map((a) =>
          a.id === account.id ? { ...a, isEnabled: !a.isEnabled } : a,
        );
        this.cdr.markForCheck();
      },
    });
  }

  deleteAccount(account: TradingAccountDto): void {
    this.accountToDelete = account;
    this.showDeleteConfirm = true;
  }

  confirmDelete(): void {
    if (!this.accountToDelete) return;
    this.service.delete(this.accountToDelete.id).subscribe({
      next: () => {
        this.accounts = this.accounts.filter((a) => a.id !== this.accountToDelete?.id);
        this.showDeleteConfirm = false;
        this.accountToDelete = null;
        this.cdr.markForCheck();
      },
    });
  }

  navigateToDetail(account: TradingAccountDto, event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.tagName === 'BUTTON' || target.closest('button')) return;
    if (account.accountType !== 0) return;
    this.router.navigate(['/darwinex/demo', account.id]);
  }
}
