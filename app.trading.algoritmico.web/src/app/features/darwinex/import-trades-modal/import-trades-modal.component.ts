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
import {
  TradingAccountService,
  TradeImportResultDto,
} from '../../../core/services/trading-account.service';

const ALLOWED_EXTENSIONS = ['.htm', '.html'];

@Component({
  selector: 'app-import-trades-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './import-trades-modal.component.html',
  styleUrl: './import-trades-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportTradesModalComponent {
  @Input({ required: true }) accountId!: string;
  @Output() closed = new EventEmitter<TradeImportResultDto | null>();

  private readonly tradingAccountService = inject(TradingAccountService);

  readonly isLoading = signal(false);
  readonly result = signal<TradeImportResultDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly fileError = signal<string | null>(null);

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

  onClose(): void {
    this.closed.emit(this.result());
  }
}
