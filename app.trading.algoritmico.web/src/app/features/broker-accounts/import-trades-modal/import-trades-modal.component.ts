import {
  Component,
  ChangeDetectionStrategy,
  Input,
  Output,
  EventEmitter,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { switchMap } from 'rxjs/operators';
import {
  TradingAccountService,
  TradeImportResultDto,
} from '../../../core/services/trading-account.service';
import { StrategyService } from '../../../core/services/strategy.service';

const ALLOWED_EXTENSIONS = ['.htm', '.html'];

@Component({
  selector: 'app-import-trades-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './import-trades-modal.component.html',
  styleUrl: './import-trades-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportTradesModalComponent {
  @Input({ required: true }) accountId!: string;
  @Output() closed = new EventEmitter<TradeImportResultDto | null>();

  private readonly tradingAccountService = inject(TradingAccountService);
  private readonly strategyService = inject(StrategyService);

  readonly isLoading = signal(false);
  readonly result = signal<TradeImportResultDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly fileError = signal<string | null>(null);

  /** Per-orphan selected strategyId for the assign combo. Keyed by magicNumber. */
  readonly orphanSelections = signal<Record<number, string>>({});
  /** Magic numbers currently being assigned (disable button + show spinner). */
  readonly assigningMagic = signal<number | null>(null);
  /** Last assign error message, shown inline. */
  readonly assignError = signal<string | null>(null);

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (!file) {
      this.selectedFile.set(null);
      this.fileError.set(null);
      return;
    }

    const name = file.name.toLowerCase();
    const isAllowed = ALLOWED_EXTENSIONS.some((ext) => name.endsWith(ext));

    if (!isAllowed) {
      this.selectedFile.set(null);
      this.fileError.set('Only .htm and .html files are accepted.');
      return;
    }

    this.selectedFile.set(file);
    this.fileError.set(null);
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file || this.isLoading()) return;

    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.tradingAccountService.importTrades(this.accountId, file).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.result.set(res);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(err?.error?.message ?? err?.message ?? 'An unexpected error occurred.');
      },
    });
  }

  copyMagicNumber(magicNumber: number): void {
    navigator.clipboard.writeText(magicNumber.toString());
  }

  onSelectStrategy(magicNumber: number, strategyId: string): void {
    this.orphanSelections.update((m) => ({ ...m, [magicNumber]: strategyId }));
  }

  selectedStrategyFor(magicNumber: number): string {
    return this.orphanSelections()[magicNumber] ?? '';
  }

  /**
   * Assigns the magic number to the chosen strategy and immediately re-imports the same
   * statement file so the trades land under the newly-linked strategy.
   */
  assignAndReimport(magicNumber: number): void {
    const strategyId = this.orphanSelections()[magicNumber];
    const file = this.selectedFile();

    if (!strategyId || !file || this.assigningMagic() !== null) return;

    this.assigningMagic.set(magicNumber);
    this.assignError.set(null);

    this.strategyService
      .assignMagicNumber(this.accountId, strategyId, magicNumber)
      .pipe(switchMap(() => this.tradingAccountService.importTrades(this.accountId, file)))
      .subscribe({
        next: (res) => {
          this.assigningMagic.set(null);
          this.result.set(res);
          this.orphanSelections.update((m) => {
            const next = { ...m };
            delete next[magicNumber];
            return next;
          });
        },
        error: (err) => {
          this.assigningMagic.set(null);
          const message = err?.error?.message ?? err?.message ?? 'Failed to assign magic number.';
          this.assignError.set(message);
        },
      });
  }

  onClose(): void {
    this.closed.emit(this.result());
  }
}
