import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoOutcomeRowVm } from '../ftmo-simulation.mappers';

/**
 * Presentational — the six chain-outcome shares of one run, in the fixed order the mapper already
 * produced (design.md Data Flow). Every row renders even at count 0 / share 0 (spec.md "A zero-count
 * outcome is still rendered"). Bar width alone never carries meaning: the count and the share are
 * both shown as text (accessibility — no colour-only encoding), and no bar uses the "gain" green
 * (design.md AD9 — green would read as "passed").
 */
@Component({
  selector: 'app-ftmo-outcome-bars',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './ftmo-outcome-bars.component.html',
  styleUrl: './ftmo-outcome-bars.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoOutcomeBarsComponent {
  readonly rows = input.required<FtmoOutcomeRowVm[]>();

  sharePercent(row: FtmoOutcomeRowVm): number {
    return Math.round(row.share * 1000) / 10;
  }
}
