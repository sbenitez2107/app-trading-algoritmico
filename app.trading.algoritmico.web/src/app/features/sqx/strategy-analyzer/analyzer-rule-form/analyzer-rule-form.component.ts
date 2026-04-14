import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { AnalyzerRuleService, AnalyzerRuleDto } from '../../../../core/services/analyzer-rule.service';

@Component({
  selector: 'app-analyzer-rule-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule],
  templateUrl: './analyzer-rule-form.component.html',
  styleUrl: './analyzer-rule-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnalyzerRuleFormComponent {
  @Input() rule: AnalyzerRuleDto | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AnalyzerRuleService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    priority: [0],
    isActive: [true]
  });

  get isEditing(): boolean {
    return this.rule !== null;
  }

  ngOnInit(): void {
    if (this.rule) {
      this.form.patchValue({
        name: this.rule.name,
        description: this.rule.description,
        priority: this.rule.priority,
        isActive: this.rule.isActive
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const formValue = this.form.value;

    const operation = this.isEditing
      ? this.service.update(this.rule!.id, {
          name: formValue.name!,
          description: formValue.description!,
          priority: formValue.priority!,
          isActive: formValue.isActive!
        })
      : this.service.create({
          name: formValue.name!,
          description: formValue.description!,
          priority: formValue.priority!
        });

    operation.subscribe({
      next: () => {
        this.isLoading.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'An error occurred. Please try again.');
      }
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('sa-form-backdrop')) {
      this.cancelled.emit();
    }
  }
}
