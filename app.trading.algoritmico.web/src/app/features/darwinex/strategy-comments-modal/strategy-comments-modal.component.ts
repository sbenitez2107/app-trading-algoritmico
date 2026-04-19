import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  output,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { StrategyService, StrategyCommentDto } from '../../../core/services/strategy.service';

@Component({
  selector: 'app-strategy-comments-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './strategy-comments-modal.component.html',
  styleUrl: './strategy-comments-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyCommentsModalComponent implements OnInit {
  private readonly strategyService = inject(StrategyService);

  readonly strategyId = input.required<string>();
  readonly strategyName = input.required<string>();
  readonly close = output<void>();

  readonly comments = signal<StrategyCommentDto[]>([]);
  readonly newContent = signal('');
  readonly isLoading = signal(false);
  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly canSubmit = computed(() => this.newContent().trim().length > 0 && !this.isSubmitting());

  ngOnInit(): void {
    this.loadComments();
  }

  onContentInput(value: string): void {
    this.newContent.set(value);
  }

  onClose(): void {
    this.close.emit();
  }

  onSubmit(): void {
    if (!this.canSubmit()) return;
    this.isSubmitting.set(true);
    this.error.set(null);

    this.strategyService.addComment(this.strategyId(), this.newContent().trim()).subscribe({
      next: (comment) => {
        this.comments.update((list) => [comment, ...list]);
        this.newContent.set('');
        this.isSubmitting.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to add comment.');
        this.isSubmitting.set(false);
      },
    });
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  private loadComments(): void {
    this.isLoading.set(true);
    this.strategyService.getComments(this.strategyId()).subscribe({
      next: (comments) => {
        this.comments.set(comments);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }
}
