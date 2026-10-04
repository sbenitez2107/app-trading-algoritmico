import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FtmoGroupCandidateDto } from '../../../core/models/ftmo-group-simulation.model';
import {
  CandidateRowVm,
  canSelect,
  distinctSymbols,
  filterCandidates,
  toCandidateRowVm,
  toggleSelection,
} from '../ftmo-group-simulation.mappers';

/**
 * Presentational multi-select picker for ONE account's candidates. Filter and search are view state
 * and never touch the selection, which the container owns. Flags inform and never block selection;
 * only the cap (`maxMembers`, supplied by the candidates read) disables unselected rows.
 */
@Component({
  selector: 'app-ftmo-group-picker',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './ftmo-group-picker.component.html',
  styleUrl: './ftmo-group-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FtmoGroupPickerComponent {
  readonly candidates = input.required<FtmoGroupCandidateDto[]>();
  readonly maxMembers = input.required<number>();
  readonly selectedIds = input.required<ReadonlySet<string>>();
  readonly selectionChange = output<ReadonlySet<string>>();

  readonly symbolFilter = signal<string | null>(null);
  readonly search = signal('');

  readonly symbols = computed(() => distinctSymbols(this.candidates()));
  readonly rows = computed<CandidateRowVm[]>(() =>
    filterCandidates(this.candidates(), {
      symbol: this.symbolFilter(),
      search: this.search(),
    }).map(toCandidateRowVm),
  );
  readonly capReached = computed(() => this.selectedIds().size >= this.maxMembers());

  isSelected(strategyId: string): boolean {
    return this.selectedIds().has(strategyId);
  }

  isDisabled(strategyId: string): boolean {
    return !canSelect(this.selectedIds(), strategyId, this.maxMembers());
  }

  onToggle(strategyId: string): void {
    this.selectionChange.emit(toggleSelection(this.selectedIds(), strategyId, this.maxMembers()));
  }

  onSymbolChange(value: string): void {
    this.symbolFilter.set(value === '' ? null : value);
  }

  onSearchChange(value: string): void {
    this.search.set(value);
  }
}
