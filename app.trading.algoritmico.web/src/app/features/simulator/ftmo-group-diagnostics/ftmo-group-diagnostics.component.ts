import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { DiagnosticsVm } from '../ftmo-group-simulation.result.mappers';

/**
 * Presentational diagnostics panel of one successful kind: per-member contribution over the whole window, the
 * first-breach attribution (which member's trades decided the breaches; shared closes are ties) and the peak
 * concurrent open positions. Absent values render the shared "not reported" text, never `0`. Net is money.
 */
@Component({
  selector: 'app-ftmo-group-diagnostics',
  standalone: true,
  imports: [NgTemplateOutlet, TranslateModule],
  templateUrl: './ftmo-group-diagnostics.component.html',
  styleUrl: './ftmo-group-diagnostics.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupDiagnosticsComponent {
  readonly vm = input.required<DiagnosticsVm>();
}
