import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import { GroupFormValue, WorstCaseReadoutVm } from '../ftmo-group-simulation.mappers';

type NumericKey = Exclude<keyof GroupFormValue, 'broker'>;

interface NumericField {
  key: NumericKey;
  labelKey: string;
}

const FIELDS = 'FTMO_SIMULATION.FIELDS.';

/**
 * Presentational parameter form of the group simulation. The page owns the value and the derived `canRun`,
 * `showFx` and readout; this component only renders them and emits edits and the explicit Run. One risk
 * field applies to every member. Labels reuse the single-strategy `FTMO_SIMULATION.FIELDS.*` keys.
 */
@Component({
  selector: 'app-ftmo-group-form',
  standalone: true,
  imports: [TranslateModule, NgTemplateOutlet],
  templateUrl: './ftmo-group-form.component.html',
  styleUrl: './ftmo-group-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupFormComponent {
  readonly value = input.required<GroupFormValue>();
  readonly showFx = input(false);
  readonly canRun = input(false);
  readonly running = input(false);
  readonly readout = input<WorstCaseReadoutVm | null>(null);
  readonly valueChange = output<GroupFormValue>();
  readonly run = output<void>();

  readonly moneyFields: NumericField[] = [
    { key: 'initialCapital', labelKey: `${FIELDS}INITIAL_CAPITAL` },
    { key: 'targetRiskPerTrade', labelKey: `${FIELDS}TARGET_RISK_PER_TRADE` },
  ];
  readonly gridFields: NumericField[] = [
    { key: 'sizeDecimals', labelKey: `${FIELDS}SIZE_DECIMALS` },
    { key: 'step', labelKey: `${FIELDS}STEP` },
    { key: 'minLot', labelKey: `${FIELDS}MIN_LOT` },
    { key: 'maxLots', labelKey: `${FIELDS}MAX_LOTS` },
  ];
  readonly fxFields: NumericField[] = [
    { key: 'fxLow', labelKey: `${FIELDS}FX_LOW` },
    { key: 'fxHigh', labelKey: `${FIELDS}FX_HIGH` },
  ];

  /** An empty input is `null`, never `0`; a typed `0` stays `0`. */
  setNumber(key: NumericKey, event: Event): void {
    const raw = (event.target as HTMLInputElement).value.trim();
    const parsed = raw === '' ? Number.NaN : Number(raw);
    this.valueChange.emit({ ...this.value(), [key]: Number.isNaN(parsed) ? null : parsed });
  }

  setBroker(event: Event): void {
    this.valueChange.emit({ ...this.value(), broker: (event.target as HTMLInputElement).value });
  }

  numberValue(key: NumericKey): number | string {
    return this.value()[key] ?? '';
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.canRun()) this.run.emit();
  }

  kindLabelKey(kind: BacktestRunKind): string {
    return kind === BacktestRunKind.Deploy
      ? 'FTMO_SIMULATION.KIND.DEPLOY'
      : 'FTMO_SIMULATION.KIND.EVALUATION';
  }
}
