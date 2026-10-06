import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import {
  FtmoGroupSearchIneligibleDto,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { percentToFraction } from '../ftmo-group-search.mappers';
import {
  DEFAULT_CEILING_PERCENT,
  ineligibleCounts,
  toTableRows,
} from '../ftmo-group-search.table.mappers';

/**
 * Presentational ranked table of the FTMO group search. Rows keep the backend order; the ceiling is a local,
 * editable percent that re-evaluates the highlight and the "only within ceiling" filter without a request.
 * The filter narrows the view only: the `rows` input is never mutated or reordered. Values that are not
 * reported render "not reported", never `0`. The per-row deep link is a typed output (wired in slice 4b).
 */
@Component({
  selector: 'app-ftmo-search-table',
  standalone: true,
  imports: [TranslateModule, DecimalPipe, NgTemplateOutlet],
  templateUrl: './ftmo-search-table.component.html',
  styleUrl: './ftmo-search-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoSearchTableComponent {
  readonly rows = input.required<FtmoGroupSearchRowDto[]>();
  readonly status = input.required<FtmoGroupSearchStatus>();
  readonly ineligible = input<FtmoGroupSearchIneligibleDto[]>([]);
  readonly openInGroup = output<FtmoGroupSearchRowDto>();

  /** The ceiling the search was requested with, as a percent; seeds (and re-seeds) the editable input. */
  readonly defaultCeilingPercent = input<number>(DEFAULT_CEILING_PERCENT);

  readonly ceilingPercent = linkedSignal<number | null>(() => this.defaultCeilingPercent());
  readonly onlyWithin = signal(false);

  /** The ceiling as a raw fraction; `null` for an empty or out-of-range input (nothing is highlighted). */
  readonly ceilingFraction = computed(() => {
    const percent = this.ceilingPercent();
    return percent === null || !Number.isFinite(percent) || percent <= 0 || percent > 100
      ? null
      : percentToFraction(percent);
  });

  readonly view = computed(() =>
    toTableRows(this.rows(), this.ceilingFraction(), this.onlyWithin()),
  );
  readonly excluded = computed(() => ineligibleCounts(this.ineligible()));
  readonly running = computed(() => this.status() === FtmoGroupSearchStatus.Running);
  readonly partial = computed(
    () =>
      this.status() === FtmoGroupSearchStatus.Cancelled ||
      this.status() === FtmoGroupSearchStatus.StoppedAtBudget,
  );

  readonly metricKeys = [
    'BREACH',
    'WORST_DAILY',
    'WORST_DRAWDOWN',
    'MEDIAN_DRAWDOWN',
    'FUNDED_NO_BREACH',
    'MEDIAN_DAYS',
  ] as const;
  readonly kindHeaders = [
    { name: 'deploy', labelKey: 'FTMO_SIMULATION.KIND.DEPLOY' },
    { name: 'evaluation', labelKey: 'FTMO_SIMULATION.KIND.EVALUATION' },
  ] as const;

  kindName(kind: BacktestRunKind): string {
    return kind === BacktestRunKind.Deploy ? 'deploy' : 'evaluation';
  }

  onCeilingInput(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.ceilingPercent.set(Number.isNaN(value) ? null : value);
  }

  onOnlyWithinChange(event: Event): void {
    this.onlyWithin.set((event.target as HTMLInputElement).checked);
  }
}
