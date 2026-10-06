import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchStatus,
} from '../../../core/models/ftmo-group-search.model';
import {
  formatElapsed,
  isRunningStatus,
  progressPercent,
  stageKey,
  statusKey,
  stopReasonKey,
} from '../ftmo-group-search.mappers';

/**
 * Presentational progress of one search job: status, stage, bar, funnel counts, full simulations versus the
 * budget, elapsed time, Cancel (enabled only while running) and the terminal message of each end state.
 * Statuses are matched by exact value, never by truthiness, so `Unknown` (0) renders its own label. Server
 * disclosure text is never rendered; the examined count and the selection-bias statement are translated.
 */
@Component({
  selector: 'app-ftmo-search-progress',
  standalone: true,
  imports: [TranslateModule, DecimalPipe],
  templateUrl: './ftmo-search-progress.component.html',
  styleUrl: './ftmo-search-progress.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoSearchProgressComponent {
  readonly job = input.required<FtmoGroupSearchJobDto>();
  readonly cancel = output<void>();

  readonly Status = FtmoGroupSearchStatus;
  readonly status = computed(() => statusKey(this.job().status));
  readonly stage = computed(() => stageKey(this.job().progress.stage));
  readonly stopReason = computed(() => stopReasonKey(this.job().stopReason));
  readonly running = computed(() => isRunningStatus(this.job().status));
  readonly percent = computed(() =>
    progressPercent(this.job().progress.processed, this.job().progress.total),
  );
  readonly elapsed = computed(() => formatElapsed(this.job().progress.elapsedMs));
}
