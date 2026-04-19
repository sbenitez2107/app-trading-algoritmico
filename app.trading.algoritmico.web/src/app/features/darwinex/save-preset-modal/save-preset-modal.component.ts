import {
  Component,
  ChangeDetectionStrategy,
  Output,
  EventEmitter,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-save-preset-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './save-preset-modal.component.html',
  styleUrl: './save-preset-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SavePresetModalComponent {
  @Output() save = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();

  readonly presetName = signal('');

  readonly canSave = computed(() => this.presetName().trim().length > 0);

  onSave(): void {
    if (!this.canSave()) return;
    this.save.emit(this.presetName().trim());
  }

  onCancel(): void {
    this.cancelled.emit();
  }
}
