import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  TradingAccountDto,
  CreateTradingAccountDto,
  UpdateTradingAccountDto,
  TradingAccountService,
  AccountType,
  PlatformType,
} from '../../../core/services/trading-account.service';

@Component({
  selector: 'app-account-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './account-form.component.html',
  styleUrl: './account-form.component.scss',
  changeDetection: ChangeDetectionStrategy.Default,
})
export class AccountFormComponent implements OnInit {
  @Input() account: TradingAccountDto | null = null;
  @Input() broker: string = 'Darwinex';
  @Input() defaultAccountType: AccountType = 0;
  @Output() saved = new EventEmitter<TradingAccountDto>();
  @Output() cancelled = new EventEmitter<void>();

  form!: FormGroup;
  isLoading = false;
  errorMessage = '';
  showPassword = false;

  get isEditing(): boolean {
    return !!this.account;
  }

  constructor(
    private fb: FormBuilder,
    private service: TradingAccountService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      name: [this.account?.name ?? '', [Validators.required, Validators.maxLength(200)]],
      broker: [{ value: this.account?.broker ?? this.broker, disabled: this.isEditing }],
      accountType: [
        { value: this.account?.accountType ?? this.defaultAccountType, disabled: this.isEditing },
      ],
      platform: [this.account?.platform ?? (0 as PlatformType)],
      accountNumber: [
        this.account?.accountNumber ?? null,
        [Validators.required, Validators.min(1)],
      ],
      login: [this.account?.login ?? null, [Validators.required, Validators.min(1)]],
      password: ['', this.isEditing ? [] : [Validators.required]],
      server: [this.account?.server ?? '', [Validators.required, Validators.maxLength(300)]],
      initialBalance: [
        this.account?.initialBalance ?? null,
        [Validators.required, Validators.min(0.01)],
      ],
      currency: [this.account?.currency ?? 'USD', [Validators.maxLength(10)]],
      isEnabled: [this.account?.isEnabled ?? true],
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('account-form-backdrop')) {
      this.onCancel();
    }
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    const raw = this.form.getRawValue();

    if (this.isEditing && this.account) {
      const dto: UpdateTradingAccountDto = {
        name: raw.name,
        platform: Number(raw.platform) as PlatformType,
        accountNumber: raw.accountNumber,
        login: raw.login,
        password: raw.password || undefined,
        server: raw.server,
        initialBalance: Number(raw.initialBalance),
        currency: raw.currency || null,
        isEnabled: raw.isEnabled,
      };
      this.service.update(this.account.id, dto).subscribe({
        next: (result) => {
          this.isLoading = false;
          this.cdr.markForCheck();
          this.saved.emit(result);
        },
        error: () => {
          this.isLoading = false;
          this.errorMessage = 'Error al actualizar la cuenta.';
          this.cdr.markForCheck();
        },
      });
    } else {
      const dto: CreateTradingAccountDto = {
        name: raw.name,
        broker: raw.broker,
        accountType: Number(raw.accountType) as AccountType,
        platform: Number(raw.platform) as PlatformType,
        accountNumber: raw.accountNumber,
        login: raw.login,
        password: raw.password,
        server: raw.server,
        initialBalance: Number(raw.initialBalance),
        currency: raw.currency || undefined,
        isEnabled: raw.isEnabled,
      };
      this.service.create(dto).subscribe({
        next: (result) => {
          this.isLoading = false;
          this.cdr.markForCheck();
          this.saved.emit(result);
        },
        error: () => {
          this.isLoading = false;
          this.errorMessage = 'Error al crear la cuenta.';
          this.cdr.markForCheck();
        },
      });
    }
  }
}
