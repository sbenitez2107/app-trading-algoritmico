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
import { GroupRiskPanelComponent } from '../group-risk-panel/group-risk-panel.component';

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
  imports: [CommonModule, TranslateModule, GroupRiskPanelComponent],
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

  /**
   * The i18n key of the message shown when a load FAILS, or null when it did not.
   *
   * An empty list and a failed request are different facts and must not render the same way: a
   * 500, a dropped connection or an unapplied migration all leave the array empty, and the empty
   * state would tell the user their imports are gone. Separate signals per panel because the two
   * loads are separate requests — a calibration outage must not claim the runs list failed.
   */
  readonly runsError = signal<string | null>(null);
  readonly calibrationsError = signal<string | null>(null);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  /**
   * The distinct strategies whose runs are on the CURRENT page — the group the risk panel offers to
   * analyse. Scoped to the page rather than to every imported run because the group has to be
   * something the operator can see: analysing strategies that are not on screen would produce a
   * figure whose membership is invisible.
   */
  readonly pageStrategyIds = computed(() => [...new Set(this.runs().map((run) => run.strategyId))]);

  readonly kindLabels = BACKTEST_KIND_LABELS;
  readonly calibrationLabels = CALIBRATION_STATUS_LABELS;

  ngOnInit(): void {
    this.loadRuns();
    this.loadCalibrations();
  }

  loadRuns(): void {
    this.isLoading.set(true);
    this.runsError.set(null);
    this.backtestService.getRuns(this.page(), this.pageSize).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.runs.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => {
        this.isLoading.set(false);
        this.runsError.set('SQX.BACKTESTS.RUNS_ERROR');
      },
    });
  }

  loadCalibrations(): void {
    this.calibrationsError.set(null);
    this.backtestService.getCalibrations().subscribe({
      next: (data) => this.calibrations.set(data),
      // Without this the rejection escapes the component entirely as an unhandled error and the
      // panel still renders "no calibrations yet".
      error: () => this.calibrationsError.set('SQX.BACKTESTS.CALIBRATIONS_ERROR'),
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.page.set(page);
    this.loadRuns();
  }
}
