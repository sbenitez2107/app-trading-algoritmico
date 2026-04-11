import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService } from '../../../../core/services/batch.service';

@Component({
  selector: 'app-advance-stage-modal',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './advance-stage-modal.component.html',
  styleUrl: './advance-stage-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdvanceStageModalComponent {
  @Input({ required: true }) batchId!: string;
  @Output() advanced = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly batchService = inject(BatchService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onSubmit(): void {
    if (!this.selectedFile()) {
      this.errorMessage.set('SQX.WORKFLOW.FILE_REQUIRED');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.batchService.advance(this.batchId, this.selectedFile()!).subscribe({
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
