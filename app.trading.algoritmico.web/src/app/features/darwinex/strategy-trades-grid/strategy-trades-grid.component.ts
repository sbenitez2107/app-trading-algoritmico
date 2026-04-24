import { Component, ChangeDetectionStrategy, Input, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, RowClassParams, themeQuartz } from 'ag-grid-community';
import { StrategyService, StrategyTradeDto } from '../../../core/services/strategy.service';

@Component({
  selector: 'app-strategy-trades-grid',
  standalone: true,
  imports: [CommonModule, AgGridAngular],
  templateUrl: './strategy-trades-grid.component.html',
  styleUrl: './strategy-trades-grid.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyTradesGridComponent implements OnInit {
  @Input({ required: true }) strategyId!: string;

  private readonly strategyService = inject(StrategyService);

  readonly status = signal<'all' | 'open' | 'closed'>('all');
  readonly isLoading = signal(true);
  readonly trades = signal<StrategyTradeDto[]>([]);
  readonly error = signal<string | null>(null);

  readonly gridTheme = themeQuartz;

  readonly columnDefs: ColDef<StrategyTradeDto>[] = [
    { field: 'ticket', headerName: 'Ticket', width: 100 },
    { field: 'openTime', headerName: 'Open Time', width: 160 },
    { field: 'closeTime', headerName: 'Close Time', width: 160 },
    { field: 'type', headerName: 'Type', width: 80 },
    { field: 'size', headerName: 'Size', width: 80 },
    { field: 'item', headerName: 'Item', width: 100 },
    { field: 'openPrice', headerName: 'Open Price', width: 110 },
    { field: 'closePrice', headerName: 'Close Price', width: 110 },
    { field: 'sl', headerName: 'SL', width: 90 },
    { field: 'tp', headerName: 'TP', width: 90 },
    { field: 'commission', headerName: 'Commission', width: 110 },
    { field: 'swap', headerName: 'Swap', width: 80 },
    { field: 'profit', headerName: 'Profit', width: 90 },
    {
      headerName: 'Status',
      field: 'isOpen',
      width: 90,
      valueFormatter: (p: { value: boolean }) => (p.value ? 'Open' : 'Closed'),
    },
  ];

  readonly rowClassRules: Record<string, (params: RowClassParams<StrategyTradeDto>) => boolean> = {
    'trade--open': (params: RowClassParams<StrategyTradeDto>) => !!params.data?.isOpen,
  };

  readonly defaultColDef: ColDef<StrategyTradeDto> = {
    sortable: true,
    filter: true,
    resizable: true,
  };

  ngOnInit(): void {
    this.loadTrades();
  }

  setStatus(status: 'all' | 'open' | 'closed'): void {
    this.status.set(status);
    this.loadTrades();
  }

  private loadTrades(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.strategyService.getTradesByStrategy(this.strategyId, this.status(), 1, 50).subscribe({
      next: (result) => {
        this.trades.set(result.items);
        this.isLoading.set(false);
      },
      error: (err: { error?: { message?: string }; message?: string }) => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Failed to load trades.');
        this.isLoading.set(false);
      },
    });
  }
}
