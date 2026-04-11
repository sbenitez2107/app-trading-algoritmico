import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { BuildingBlockService, BuildingBlockDto, BB_TYPE_LABELS } from '../../../../core/services/building-block.service';
import { BuildingBlockFormComponent } from '../building-block-form/building-block-form.component';

@Component({
  selector: 'app-building-blocks-list',
  standalone: true,
  imports: [CommonModule, TranslateModule, BuildingBlockFormComponent],
  templateUrl: './building-blocks-list.component.html',
  styleUrl: './building-blocks-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuildingBlocksListComponent {
  private readonly service = inject(BuildingBlockService);

  blocks = signal<BuildingBlockDto[]>([]);
  isLoading = signal(true);
  showModal = signal(false);
  editingBlock = signal<BuildingBlockDto | null>(null);
  deleteTarget = signal<BuildingBlockDto | null>(null);

  readonly typeLabels = BB_TYPE_LABELS;

  ngOnInit(): void {
    this.loadBlocks();
  }

  openCreate(): void {
    this.editingBlock.set(null);
    this.showModal.set(true);
  }

  openEdit(block: BuildingBlockDto): void {
    this.editingBlock.set(block);
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingBlock.set(null);
  }

  onSaved(): void {
    this.closeModal();
    this.loadBlocks();
  }

  confirmDelete(block: BuildingBlockDto): void {
    this.deleteTarget.set(block);
  }

  cancelDelete(): void {
    this.deleteTarget.set(null);
  }

  executeDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.service.delete(target.id).subscribe({
      next: () => {
        this.deleteTarget.set(null);
        this.loadBlocks();
      },
      error: () => this.deleteTarget.set(null)
    });
  }

  getTypeBadgeClass(type: number): string {
    const map: Record<number, string> = {
      0: 'bb-badge--base',
      1: 'bb-badge--trend',
      2: 'bb-badge--volatility',
      3: 'bb-badge--reversion'
    };
    return map[type] ?? '';
  }

  private loadBlocks(): void {
    this.isLoading.set(true);
    this.service.getAll().subscribe({
      next: (data) => {
        this.blocks.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
