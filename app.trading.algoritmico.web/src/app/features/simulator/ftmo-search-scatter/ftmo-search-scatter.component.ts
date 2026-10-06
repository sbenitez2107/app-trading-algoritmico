import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoGroupSearchRowDto } from '../../../core/models/ftmo-group-search.model';
import { percentToFraction } from '../ftmo-group-search.mappers';
import { SCATTER_LAYOUT, ScatterPointVm, toScatter } from '../ftmo-group-search.scatter.mappers';

/**
 * Presentational scatter of the FTMO group search: plain SVG, theme variables only. One point per ranked row
 * that has a median-days-to-both-targets (x) and a breach share (y), both of the worse kind. The frontier is
 * highlighted and the points within the ceiling are styled distinctly. Rows that cannot be plotted are
 * counted, never placed at 0. Activating a point emits the same typed event as the table's action.
 */
@Component({
  selector: 'app-ftmo-search-scatter',
  standalone: true,
  imports: [TranslateModule, DecimalPipe],
  templateUrl: './ftmo-search-scatter.component.html',
  styleUrl: './ftmo-search-scatter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoSearchScatterComponent {
  readonly rows = input.required<FtmoGroupSearchRowDto[]>();
  /** The elimination ceiling as a percent; `null` or out of range highlights nothing. */
  readonly ceilingPercent = input<number | null>(null);
  readonly openInGroup = output<FtmoGroupSearchRowDto>();

  readonly layout = SCATTER_LAYOUT;
  readonly viewBox = `0 0 ${SCATTER_LAYOUT.viewWidth} ${SCATTER_LAYOUT.viewHeight}`;
  readonly baseline = SCATTER_LAYOUT.top + SCATTER_LAYOUT.height;
  readonly right = SCATTER_LAYOUT.left + SCATTER_LAYOUT.width;

  private readonly ceilingFraction = computed(() => {
    const percent = this.ceilingPercent();
    return percent === null || !Number.isFinite(percent) || percent <= 0 || percent > 100
      ? null
      : percentToFraction(percent);
  });

  readonly vm = computed(() => toScatter(this.rows(), this.ceilingFraction()));

  open(point: ScatterPointVm): void {
    this.openInGroup.emit(point.row);
  }
}
