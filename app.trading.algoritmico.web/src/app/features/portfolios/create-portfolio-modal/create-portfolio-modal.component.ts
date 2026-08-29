import { Component, ChangeDetectionStrategy, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import {
  AccountType,
  CreatePortfolioDto,
  PortfolioService,
} from '../../../core/services/portfolio.service';

/**
 * Creates a portfolio from a set of strategies the caller already chose (today: the filtered rows
 * of the account monthly matrix). Deliberately asks for nothing but the two fields the caller
 * cannot know - name and starting capital - because everything else is implied by where it was
 * opened from: broker and account type come from the account, and every strategy enters with the
 * same weight. Rebalancing belongs in the portfolio detail, not in a creation dialog.
 * Follows the `risk-limits-modal` convention: typed reactive form, OnPush, input()/output().
 */
@Component({
  selector: 'app-create-portfolio-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-portfolio-modal.component.html',
  styleUrl: './create-portfolio-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreatePortfolioModalComponent {
  readonly strategyIds = input.required<string[]>();
  readonly broker = input.required<string>();
  readonly accountType = input.required<AccountType>();

  /** Emits the id of the created portfolio. */
  readonly created = output<string>();
  readonly cancelled = output<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PortfolioService);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
    initialCapital: [100000, [Validators.required, Validators.min(0.01)]],
  });

  submit(): void {
    // Guarded rather than merely disabled: a double Enter on the form would otherwise create two
    // portfolios, and the request is not idempotent.
    if (this.saving()) return;

    const name = this.form.controls.name.value.trim();
    const initialCapital = this.form.controls.initialCapital.value;

    if (name === '' || !(initialCapital > 0)) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Enter a name and a starting capital above zero.');
      return;
    }

    const description = this.form.controls.description.value.trim();
    const dto: CreatePortfolioDto = {
      name,
      description: description === '' ? undefined : description,
      broker: this.broker(),
      accountType: this.accountType(),
      initialCapital,
      baseCurrency: 'USD',
      members: this.strategyIds().map((strategyId) => ({ strategyId, weight: 1 })),
    };

    this.saving.set(true);
    this.errorMessage.set(null);
    this.service.create(dto).subscribe({
      next: (portfolio) => {
        this.saving.set(false);
        this.created.emit(portfolio.id);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err?.error?.error ?? 'The portfolio could not be created.');
      },
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.cancelled.emit();
    }
  }
}
