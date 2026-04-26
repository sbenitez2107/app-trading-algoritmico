import {
  Component,
  ChangeDetectionStrategy,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
} from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  /**
   * Tracks whether the user has manually edited the "nextInputCount" field.
   * While false, changes to "passedCount" mirror into "nextInputCount" automatically.
   * Once the user types into "nextInputCount" the two values become independent.
   */
  private readonly nextInputManuallyEdited = signal(false);

  form = this.fb.group({
    passedCount: [null as number | null, [Validators.min(0)]],
    nextInputCount: [null as number | null, [Validators.min(0)]],
  });

  /** True when the user has set both counts and `next > passed`. */
  readonly countMismatchWarning = signal(false);

  constructor() {
    // Mirror passedCount → nextInputCount until the user types into nextInputCount manually.
    this.form.controls.passedCount.valueChanges.subscribe((value) => {
      if (!this.nextInputManuallyEdited()) {
        this.form.controls.nextInputCount.setValue(value, { emitEvent: false });
      }
      this.recomputeWarning();
    });
    this.form.controls.nextInputCount.valueChanges.subscribe(() => this.recomputeWarning());
  }

  onNextInputCountChange(): void {
    this.nextInputManuallyEdited.set(true);
  }

  private recomputeWarning(): void {
    const passed = this.form.controls.passedCount.value;
    const next = this.form.controls.nextInputCount.value;
    this.countMismatchWarning.set(passed !== null && next !== null && next > passed);
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onSubmit(): void {
    const file = this.selectedFile() ?? undefined;
    const passed = this.form.value.passedCount ?? undefined;
    const next = this.form.value.nextInputCount ?? undefined;

    if (!file && passed === undefined) {
      this.errorMessage.set('SQX.WORKFLOW.FILE_OR_COUNT_REQUIRED');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.batchService.advance(this.batchId, file, passed, next).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.advanced.emit();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'COMMON.STATUS.ERROR');
      },
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.cancelled.emit();
    }
  }
}
