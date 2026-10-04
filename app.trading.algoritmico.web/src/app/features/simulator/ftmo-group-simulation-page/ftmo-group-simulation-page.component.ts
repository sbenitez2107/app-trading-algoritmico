import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

/** Shell of the FTMO group simulation screen: title and the always-visible disclosure. */
@Component({
  selector: 'app-ftmo-group-simulation-page',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './ftmo-group-simulation-page.component.html',
  styleUrl: './ftmo-group-simulation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupSimulationPageComponent {}
