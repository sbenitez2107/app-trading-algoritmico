import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService } from '../../../../core/services/batch.service';

@Component({
  selector: 'app-advance-stage-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule],
  templateUrl: './advance-stage-modal.component.html',
  styleUrl: './advance-stage-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdvanceStageModalComponent {
  @Input({ required: true }) batchId!: string;
  @Input() batchName = '';
  @Output() advanced = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly batchService = inject(BatchService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  form = this.fb.group({
    strategyCount: [null as number | null, [Validators.min(0)]]
  });

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onSubmit(): void {
    const file = this.selectedFile() ?? undefined;
    const count = this.form.value.strategyCount ?? undefined;

    if (!file && count === undefined) {
      this.errorMessage.set('SQX.WORKFLOW.FILE_OR_COUNT_REQUIRED');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.batchService.advance(this.batchId, file, count).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.advanced.emit();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'COMMON.STATUS.ERROR');
      }
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.cancelled.emit();
    }
  }
}
