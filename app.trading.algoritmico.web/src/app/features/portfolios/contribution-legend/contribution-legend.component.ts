import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { formatCurrency } from '../../../shared/utils/format';

/** One selectable member in the legend, already decorated by the parent with its draw state. */
export interface ContributionLegendRow {
  strategyId: string;
  strategyName: string;
  finalContribution: number;
  /** Colour the line currently owns on the chart, or null when it is not drawn. */
  color: string | null;
  selected: boolean;
}

/**
 * Legend + picker for the per-member contribution lines drawn over the combined equity curve.
 * Purely presentational: the parent owns which members are selected and what colour each one got,
 * so this component never talks to a service.
 */
@Component({
  selector: 'app-contribution-legend',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './contribution-legend.component.html',
  styleUrl: './contribution-legend.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContributionLegendComponent {
  readonly rows = input<ContributionLegendRow[]>([]);
  /** Cap on simultaneously drawn lines — past it the unselected chips go disabled. */
  readonly maxOverlays = input<number>(8);
  readonly limitReached = input(false);
  readonly selectedCount = input(0);

  /** Whether the faint "every other member" fan is currently drawn. */
  readonly ghostsOn = input(false);
  /** Whether the combined curve itself is currently drawn. */
  readonly combinedOn = input(true);

  readonly toggled = output<string>();
  readonly cleared = output<void>();
  readonly ghostsToggled = output<void>();
  readonly combinedToggled = output<void>();

  money(v: number): string {
    return formatCurrency(v);
  }
}
