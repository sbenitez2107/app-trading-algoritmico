import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import {
  BacktestService,
  BacktestRunDto,
  SymbolCalibrationDto,
  BACKTEST_KIND_LABELS,
  CALIBRATION_STATUS_LABELS,
} from '../../../../core/services/backtest.service';

const PAGE_SIZE = 20;

/**
 * Read-only view of every imported backtest run and the per-symbol point-value calibrations.
 *
 * Import used to live here, behind a multi-file modal that inferred which strategy each file
 * belonged to from its name. It now happens from the strategy's own row on the account grid, where
 * the strategy is already known — so this page has no import action, and no "unmatched" panel,
 * because a run without a strategy is no longer expressible.
 *
 * Calibrations stay here rather than on a strategy row: they are pooled per SYMBOL across every
 * run, so they belong to no single strategy.
 */
@Component({
  selector: 'app-backtests-list',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './backtests-list.component.html',
  styleUrl: './backtests-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacktestsListComponent implements OnInit {
  private readonly backtestService = inject(BacktestService);

  readonly pageSize = PAGE_SIZE;
  readonly runs = signal<BacktestRunDto[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly calibrations = signal<SymbolCalibrationDto[]>([]);
  readonly isLoading = signal(false);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly kindLabels = BACKTEST_KIND_LABELS;
  readonly calibrationLabels = CALIBRATION_STATUS_LABELS;

  ngOnInit(): void {
    this.loadRuns();
    this.loadCalibrations();
  }

  loadRuns(): void {
    this.isLoading.set(true);
    this.backtestService.getRuns(this.page(), this.pageSize).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.runs.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.isLoading.set(false),
    });
  }

  loadCalibrations(): void {
    this.backtestService.getCalibrations().subscribe({
      next: (data) => this.calibrations.set(data),
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.page.set(page);
    this.loadRuns();
  }
}
