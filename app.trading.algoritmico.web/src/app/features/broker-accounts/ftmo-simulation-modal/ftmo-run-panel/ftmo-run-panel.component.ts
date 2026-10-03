import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoRunPanelVm } from '../ftmo-simulation.mappers';
import { FtmoOutcomeBarsComponent } from '../ftmo-outcome-bars/ftmo-outcome-bars.component';
import { FtmoOrderStatsTableComponent } from '../ftmo-order-stats-table/ftmo-order-stats-table.component';
import { BacktestRunKind } from '../../../../core/services/backtest.service';

/**
 * Presentational — one run's panel (Deploy or Evaluation), per design.md AD5/AD9. The template
 * switches on the mapper's state discriminant (never on a raw enum, so a `0` value can never read as
 * absent — hard rule 2). Every state renders the disclosure, `notModelled`, `monthsWithoutStart`, and
 * the start-1 flag unconditionally and without a collapse/expand interaction (design.md AD11 — this
 * is always-visible data, not a translated sentence behind an accordion).
 */
@Component({
  selector: 'app-ftmo-run-panel',
  standalone: true,
  imports: [CommonModule, TranslateModule, FtmoOutcomeBarsComponent, FtmoOrderStatsTableComponent],
  templateUrl: './ftmo-run-panel.component.html',
  styleUrl: './ftmo-run-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoRunPanelComponent {
  readonly vm = input.required<FtmoRunPanelVm>();
  /**
   * The broker of the submitted query. Broker-scoped refusals (`LimitsNotConfigured`,
   * `ProductNotTwoStep`) name it, because the cause is the broker's risk-limit configuration, not the
   * account the modal was opened from.
   */
  readonly broker = input<string | null>(null);

  readonly BacktestRunKind = BacktestRunKind;

  kindLabelKey(): string {
    return this.vm().kind === BacktestRunKind.Deploy
      ? 'FTMO_SIMULATION.KIND.DEPLOY'
      : 'FTMO_SIMULATION.KIND.EVALUATION';
  }
}
