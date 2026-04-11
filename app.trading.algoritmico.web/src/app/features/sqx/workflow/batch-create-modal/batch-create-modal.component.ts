import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService } from '../../../../core/services/batch.service';
import { BuildingBlockService, BuildingBlockDto, BB_TYPE_LABELS } from '../../../../core/services/building-block.service';

@Component({
  selector: 'app-batch-create-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule],
  templateUrl: './batch-create-modal.component.html',
  styleUrl: './batch-create-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchCreateModalComponent {
  @Input({ required: true }) assetId!: string;
  @Input({ required: true }) timeframe!: number;
  @Output() created = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly batchService = inject(BatchService);
  private readonly bbService = inject(BuildingBlockService);

  blocks = signal<BuildingBlockDto[]>([]);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  selectedFile = signal<File | null>(null);
  readonly typeLabels = BB_TYPE_LABELS;

  form = this.fb.group({
    buildingBlockId: ['', Validators.required],
    name: ['']
  });

  ngOnInit(): void {
    this.bbService.getAll().subscribe({
      next: (data) => this.blocks.set(data)
    });
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onSubmit(): void {
    if (this.form.invalid || !this.selectedFile()) {
      this.form.markAllAsTouched();
      if (!this.selectedFile()) this.errorMessage.set('SQX.WORKFLOW.FILE_REQUIRED');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.batchService.create(
      {
        assetId: this.assetId,
        timeframe: this.timeframe,
        buildingBlockId: this.form.value.buildingBlockId!,
        name: this.form.value.name || undefined
      },
      this.selectedFile()!
    ).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.created.emit();
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
