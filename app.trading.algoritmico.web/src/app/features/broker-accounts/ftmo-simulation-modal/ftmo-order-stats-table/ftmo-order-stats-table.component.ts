import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoOrderStatRowVm } from '../ftmo-simulation.mappers';

/**
 * Presentational — a plain semantic `<table>` for the six order-statistics rows (design.md AD5
 * explicitly rejects a data-grid component for six fixed rows). `null` renders "—" (absent, e.g. an
 * N=0 row); `0` renders "0" — the two are never conflated (design.md Data Flow). The funded-from-
 * chain-start row is the secondary/muted row; funded-from-funded-start is the headline.
 */
@Component({
  selector: 'app-ftmo-order-stats-table',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './ftmo-order-stats-table.component.html',
  styleUrl: './ftmo-order-stats-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoOrderStatsTableComponent {
  readonly rows = input.required<FtmoOrderStatRowVm[]>();

  /** `null` is absent ("—"), never conflated with a real `0`, which passes through unchanged. */
  cellKey(value: number | null): string | null {
    return value === null ? 'FTMO_SIMULATION.ORDER_STATS.ABSENT' : null;
  }
}
