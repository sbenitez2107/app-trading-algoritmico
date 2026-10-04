import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoRunPanelComponent } from '../../broker-accounts/ftmo-simulation-modal/ftmo-run-panel/ftmo-run-panel.component';
import { GroupResultVm } from '../ftmo-group-simulation.result.mappers';

/**
 * Presentational result area of the FTMO group simulation: name warnings, the group-wide refusal (once,
 * above the slots, with no panel findings), one slot per kind (Deploy and Evaluation side by side, never
 * merged), the window and member coverage, and the group disclosure. Successful kinds reuse the shipped
 * `FtmoRunPanelComponent` unchanged. Every string is an i18n key; the server's `Disclosures` text is never
 * rendered (the three group points are fixed translated keys).
 */
@Component({
  selector: 'app-ftmo-group-result',
  standalone: true,
  imports: [NgTemplateOutlet, TranslateModule, FtmoRunPanelComponent],
  templateUrl: './ftmo-group-result.component.html',
  styleUrl: './ftmo-group-result.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupResultComponent {
  readonly vm = input.required<GroupResultVm>();
  /** The submitted broker: broker-scoped inner refusals (`LimitsNotConfigured`) name it. */
  readonly broker = input<string | null>(null);
}
