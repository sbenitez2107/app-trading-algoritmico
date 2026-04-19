import {
  Component,
  ChangeDetectionStrategy,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StrategyService, StrategyDto } from '../../../core/services/strategy.service';

@Component({
  selector: 'app-add-strategy-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './add-strategy-modal.component.html',
  styleUrl: './add-strategy-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddStrategyModalComponent {
  @Input({ required: true }) accountId!: string;
  @Output() strategyCreated = new EventEmitter<StrategyDto>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly strategyService = inject(StrategyService);

  readonly name = signal('');
  readonly sqxFile = signal<File | null>(null);
  readonly htmlFile = signal<File | null>(null);
  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly canSubmit = computed(
    () =>
      this.name().trim().length > 0 &&
      !!this.sqxFile() &&
      !!this.htmlFile() &&
      !this.isSubmitting(),
  );

  onSqxFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.sqxFile.set(file);
    this.autoSuggestName(file);
  }

  onHtmlFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.htmlFile.set(file);
    this.autoSuggestName(file);
  }

  private autoSuggestName(file: File | null): void {
    if (!file || this.name().trim().length > 0) return;
    const basename = file.name.replace(/\.[^.]+$/, '');
    this.name.set(basename);
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  submit(): void {
    const sqx = this.sqxFile();
    const html = this.htmlFile();

    if (!this.canSubmit() || !sqx || !html) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    this.strategyService.addToAccount(this.accountId, this.name().trim(), sqx, html).subscribe({
      next: (dto) => {
        this.isSubmitting.set(false);
        this.strategyCreated.emit(dto);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.error.set(err?.error?.message ?? err?.message ?? 'An unexpected error occurred.');
      },
    });
  }
}
