import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import {
  MAX_FULL_SIMULATIONS_CEILING,
  MAX_WALL_CLOCK_SECONDS_CEILING,
  MIN_GROUP_SIZE,
  SearchFormValue,
  fxBandValid,
  sizeBoundsValid,
} from '../ftmo-group-search.mappers';

type NumericKey = Exclude<keyof SearchFormValue, 'excludeIdentical' | 'onePercentRule'>;
type BooleanKey = 'excludeIdentical' | 'onePercentRule';

interface NumericField {
  key: NumericKey;
  labelKey: string;
}

const F = 'SIMULATOR.FTMO_SEARCH.FORM.';

/**
 * Presentational search form. The page owns the value and the derived `canStart`; this component renders
 * it and emits edits and the explicit Start. The ceiling is typed as a percent (converted once, in the
 * request mapper). Broker and the lot grid are intentionally not editable here.
 */
@Component({
  selector: 'app-ftmo-search-form',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './ftmo-search-form.component.html',
  styleUrl: './ftmo-search-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoSearchFormComponent {
  readonly value = input.required<SearchFormValue>();
  readonly accounts = input.required<readonly { id: string; name: string }[]>();
  readonly accountId = input<string | null>(null);
  readonly maxMembers = input.required<number>();
  readonly showFx = input(false);
  readonly canStart = input(false);
  readonly running = input(false);
  readonly valueChange = output<SearchFormValue>();
  readonly accountChange = output<string>();
  readonly start = output<void>();

  readonly minSize = MIN_GROUP_SIZE;
  readonly maxFullSimulationsCeiling = MAX_FULL_SIMULATIONS_CEILING;
  readonly maxWallClockCeiling = MAX_WALL_CLOCK_SECONDS_CEILING;
  readonly sizeValid = computed(() => sizeBoundsValid(this.value(), this.maxMembers()));
  readonly fxValid = computed(() => fxBandValid(this.value()));

  readonly sizeFields: NumericField[] = [
    { key: 'minMembers', labelKey: `${F}MIN_MEMBERS` },
    { key: 'maxMembers', labelKey: `${F}MAX_MEMBERS` },
  ];
  readonly constraintFields: NumericField[] = [
    { key: 'maxPerInstrument', labelKey: `${F}MAX_PER_INSTRUMENT` },
    { key: 'ceilingPercent', labelKey: `${F}CEILING` },
  ];
  readonly moneyFields: NumericField[] = [
    { key: 'initialCapital', labelKey: `${F}CAPITAL` },
    { key: 'targetRiskPerTrade', labelKey: `${F}RISK` },
  ];
  readonly fxFields: NumericField[] = [
    { key: 'fxLow', labelKey: `${F}FX_LOW` },
    { key: 'fxHigh', labelKey: `${F}FX_HIGH` },
  ];
  readonly budgetFields: (NumericField & { ceiling: number })[] = [
    {
      key: 'maxFullSimulations',
      labelKey: `${F}MAX_FULL_SIMULATIONS`,
      ceiling: MAX_FULL_SIMULATIONS_CEILING,
    },
    {
      key: 'maxWallClockSeconds',
      labelKey: `${F}MAX_WALL_CLOCK`,
      ceiling: MAX_WALL_CLOCK_SECONDS_CEILING,
    },
  ];

  /** An empty input is `null`, never `0`; a typed `0` stays `0`. */
  setNumber(key: NumericKey, event: Event): void {
    const raw = (event.target as HTMLInputElement).value.trim();
    const parsed = raw === '' ? Number.NaN : Number(raw);
    this.valueChange.emit({ ...this.value(), [key]: Number.isNaN(parsed) ? null : parsed });
  }

  setFlag(key: BooleanKey, event: Event): void {
    this.valueChange.emit({ ...this.value(), [key]: (event.target as HTMLInputElement).checked });
  }

  setAccount(event: Event): void {
    this.accountChange.emit((event.target as HTMLSelectElement).value);
  }

  numberValue(key: NumericKey): number | string {
    return this.value()[key] ?? '';
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.canStart()) this.start.emit();
  }
}
