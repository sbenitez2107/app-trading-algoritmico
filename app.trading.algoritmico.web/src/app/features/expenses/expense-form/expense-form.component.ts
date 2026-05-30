import { Component, Input, Output, EventEmitter, signal, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, FormGroup, Validators } from '@angular/forms';
import { ExpenseService, ExpenseDto, CreateExpenseDto, UpdateExpenseDto, ExpenseCategory } from '../../../core/services/expense.service';

@Component({
  selector: 'app-expense-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './expense-form.component.html',
  styleUrls: ['./expense-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseFormComponent implements OnInit {
  @Input() expense: ExpenseDto | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly expenseService = inject(ExpenseService);

  form!: FormGroup;
  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly isEditing = signal(false);

  readonly categoryOptions = [
    { value: ExpenseCategory.MentoriaImox, label: 'Mentoría IMOX' },
    { value: ExpenseCategory.ServidorHetzner, label: 'Servidor Hetzner' },
    { value: ExpenseCategory.FTMO, label: 'FTMO' },
    { value: ExpenseCategory.WSF, label: 'WSF' },
    { value: ExpenseCategory.DarwinexZero, label: 'Darwinex Zero' },
    { value: ExpenseCategory.ServidorFxvsPro, label: 'Servidor fxvps.pro' },
  ];

  ngOnInit(): void {
    this.initForm();
    this.isEditing.set(!!this.expense);
    if (this.expense) {
      this.patchFormWithExpense();
    }
  }

  private initForm(): void {
    this.form = this.fb.group({
      description: ['', [Validators.required, Validators.minLength(3)]],
      category: [ExpenseCategory.FTMO, Validators.required],
      amount: ['', [Validators.required, Validators.min(0.01)]],
      date: ['', Validators.required],
      notes: ['']
    });
  }

  private patchFormWithExpense(): void {
    if (!this.expense) return;
    const date = new Date(this.expense.date).toISOString().split('T')[0];
    this.form.patchValue({
      description: this.expense.description,
      category: this.expense.category,
      amount: this.expense.amount,
      date,
      notes: this.expense.notes
    });
  }

  onSubmit(): void {
    if (!this.form.valid) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const formValue = this.form.value;
    const date = new Date(formValue.date).toISOString();

    if (this.isEditing()) {
      const dto: UpdateExpenseDto = {
        description: formValue.description,
        category: parseInt(formValue.category, 10),
        amount: parseFloat(formValue.amount),
        date,
        notes: formValue.notes || undefined
      };
      this.expenseService.update(this.expense!.id, dto).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Error al actualizar el gasto');
          this.isSubmitting.set(false);
        }
      });
    } else {
      const dto: CreateExpenseDto = {
        description: formValue.description,
        category: parseInt(formValue.category, 10),
        amount: parseFloat(formValue.amount),
        date,
        notes: formValue.notes || undefined
      };
      this.expenseService.create(dto).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Error al crear el gasto');
          this.isSubmitting.set(false);
        }
      });
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }
}
