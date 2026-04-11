import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { BuildingBlockService, BuildingBlockDto, BB_TYPE_LABELS } from '../../../../core/services/building-block.service';

@Component({
  selector: 'app-building-block-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule],
  templateUrl: './building-block-form.component.html',
  styleUrl: './building-block-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuildingBlockFormComponent {
  @Input() block: BuildingBlockDto | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(BuildingBlockService);

  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  readonly typeOptions = Object.entries(BB_TYPE_LABELS).map(([value, label]) => ({
    value: Number(value),
    label
  }));

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    type: [0, [Validators.required]]
  });

  get isEditing(): boolean {
    return this.block !== null;
  }

  ngOnInit(): void {
    if (this.block) {
      this.form.patchValue({
        name: this.block.name,
        description: this.block.description ?? '',
        type: this.block.type
      });
    }
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.selectedFile.set(input.files[0]);
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const dto = {
      name: this.form.value.name!,
      description: this.form.value.description || null,
      type: this.form.value.type!
    };

    const file = this.selectedFile() ?? undefined;

    const operation = this.isEditing
      ? this.service.update(this.block!.id, dto, file)
      : this.service.create(dto, file);

    operation.subscribe({
      next: () => {
        this.isLoading.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'SQX.BUILDING_BLOCKS.FORM.ERROR');
      }
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('bb-form-backdrop')) {
      this.cancelled.emit();
    }
  }
}
