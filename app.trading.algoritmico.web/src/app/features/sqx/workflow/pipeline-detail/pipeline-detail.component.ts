import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService, BatchDto, STAGE_TYPE_LABELS, STAGE_STATUS_LABELS, TIMEFRAME_LABELS, BatchStageSummaryDto } from '../../../../core/services/batch.service';
import { BatchStageService } from '../../../../core/services/batch-stage.service';
import { BatchCreateModalComponent } from '../batch-create-modal/batch-create-modal.component';
import { AdvanceStageModalComponent } from '../advance-stage-modal/advance-stage-modal.component';

@Component({
  selector: 'app-pipeline-detail',
  standalone: true,
  imports: [CommonModule, TranslateModule, BatchCreateModalComponent, AdvanceStageModalComponent],
  templateUrl: './pipeline-detail.component.html',
  styleUrl: './pipeline-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PipelineDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly batchService = inject(BatchService);
  private readonly stageService = inject(BatchStageService);

  assetId = '';
  timeframe = 0;
  batches = signal<BatchDto[]>([]);
  isLoading = signal(true);
  showCreateModal = signal(false);
  advanceBatch = signal<BatchDto | null>(null);
  deleteTarget = signal<{ batch: BatchDto; stage: BatchStageSummaryDto } | null>(null);
  editingStage = signal<{ batch: BatchDto; stage: BatchStageSummaryDto } | null>(null);
  editCount = signal<number>(0);

  readonly stageLabels = STAGE_TYPE_LABELS;
  readonly statusLabels = STAGE_STATUS_LABELS;
  readonly timeframeLabels = TIMEFRAME_LABELS;
  readonly stageTypes = [0, 1, 2, 3, 4];

  /**
   * Gets the display counts for a batch cell.
   * Each stage shows: "input to this stage / output of this stage"
   * - Builder: 0 / inputCount (no input, creates strategies)
   * - Other stages: previous stage's inputCount / this stage's inputCount
   *   (because inputCount = what entered this stage = what passed previous stage)
   */
  getCellCounts(batch: BatchDto, stage: BatchStageSummaryDto): { input: number; passed: number } {
    if (stage.stageType === 0) {
      // Builder: no input, output = strategies created (inputCount)
      return { input: 0, passed: stage.inputCount };
    }
    // Find previous stage in this batch
    const prevStageType = stage.stageType - 1;
    const prevStage = batch.stages.find(s => s.stageType === prevStageType);
    const input = prevStage?.inputCount ?? stage.inputCount;
    return { input, passed: stage.inputCount };
  }

  /**
   * Totals per stage across all batches, using the same logic as cell display.
   */
  readonly stageTotals = computed(() => {
    const totals = new Map<number, { totalInput: number; totalPassed: number }>();
    for (const st of this.stageTypes) {
      totals.set(st, { totalInput: 0, totalPassed: 0 });
    }
    for (const batch of this.batches()) {
      for (const stage of batch.stages) {
        const counts = this.getCellCounts(batch, stage);
        const t = totals.get(stage.stageType)!;
        t.totalInput += counts.input;
        t.totalPassed += counts.passed;
      }
    }
    return totals;
  });

  getStageTotalDisplay(stageType: number): string {
    const t = this.stageTotals().get(stageType);
    if (!t || (t.totalInput === 0 && t.totalPassed === 0)) return '—';
    if (stageType === 0) return `${t.totalPassed}`;
    return `${t.totalInput} / ${t.totalPassed}`;
  }

  getStageTotalPassRate(stageType: number): string {
    const t = this.stageTotals().get(stageType);
    if (!t || t.totalInput === 0 || stageType === 0) return '';
    if (t.totalInput === t.totalPassed) return '';
    const pct = (t.totalPassed / t.totalInput) * 100;
    return `${pct.toFixed(1)}%`;
  }

  ngOnInit(): void {
    this.assetId = this.route.snapshot.params['assetId'];
    this.timeframe = Number(this.route.snapshot.params['timeframe']);
    this.loadBatches();
  }

  getStage(batch: BatchDto, stageType: number): BatchStageSummaryDto | undefined {
    return batch.stages.find(s => s.stageType === stageType);
  }

  getCellPassRate(batch: BatchDto, stage: BatchStageSummaryDto): string {
    if (stage.stageType === 0) return ''; // Builder has no pass rate
    const counts = this.getCellCounts(batch, stage);
    if (counts.input === 0 || counts.input === counts.passed) return '';
    const pct = (counts.passed / counts.input) * 100;
    return `${pct.toFixed(1)}%`;
  }

  getStatusClass(stage: BatchStageSummaryDto | undefined): string {
    if (!stage) return 'cell--empty';
    switch (stage.status) {
      case 0: return 'cell--pending';
      case 1: return 'cell--running';
      case 2: return 'cell--completed';
      default: return 'cell--empty';
    }
  }

  getStatusIcon(stage: BatchStageSummaryDto | undefined): string {
    if (!stage) return '';
    switch (stage.status) {
      case 0: return '○';
      case 1: return '🔄';
      case 2: return '✅';
      default: return '';
    }
  }

  getStatusLabel(stage: BatchStageSummaryDto | undefined): string {
    if (!stage) return '—';
    return this.statusLabels[stage.status] ?? '—';
  }

  canAdvance(batch: BatchDto): boolean {
    if (batch.stages.length === 0) return false;
    const latest = [...batch.stages].sort((a, b) => b.stageType - a.stageType)[0];
    if (latest.stageType >= 4) return false;
    // Demo must be completed before advancing to Live
    if (latest.stageType === 3) return latest.status === 2;
    return true;
  }

  canCompleteDemo(batch: BatchDto, stage: BatchStageSummaryDto): boolean {
    if (stage.stageType !== 3 || stage.status === 2) return false;
    const latest = [...batch.stages].sort((a, b) => b.stageType - a.stageType)[0];
    return latest.id === stage.id;
  }

  completeDemo(batch: BatchDto, stage: BatchStageSummaryDto, event: MouseEvent): void {
    event.stopPropagation();
    this.stageService.update(batch.id, stage.id, { status: 2 }).subscribe({
      next: () => this.loadBatches()
    });
  }

  // Toggle Pending ↔ Running
  toggleRunning(batch: BatchDto, stage: BatchStageSummaryDto, event: MouseEvent): void {
    event.stopPropagation();
    const newStatus = stage.status === 0 ? 1 : 0; // Pending(0) → Running(1) or back
    this.stageService.update(batch.id, stage.id, { status: newStatus }).subscribe({
      next: () => this.loadBatches()
    });
  }

  canToggleRunning(stage: BatchStageSummaryDto | undefined): boolean {
    return stage !== undefined && stage.status !== 2; // Can toggle if not Completed
  }

  // Edit stage counts
  openEditStage(batch: BatchDto, stage: BatchStageSummaryDto, event: MouseEvent): void {
    event.stopPropagation();
    this.editingStage.set({ batch, stage });
    this.editCount.set(stage.inputCount);
  }

  saveEditStage(): void {
    const target = this.editingStage();
    if (!target) return;
    this.stageService.update(target.batch.id, target.stage.id, {
      outputCount: this.editCount()
    }).subscribe({ next: () => { this.editingStage.set(null); this.loadBatches(); } });
  }

  cancelEditStage(): void {
    this.editingStage.set(null);
  }

  // Delete (rollback) stage
  confirmDeleteStage(batch: BatchDto, stage: BatchStageSummaryDto, event: MouseEvent): void {
    event.stopPropagation();
    this.deleteTarget.set({ batch, stage });
  }

  executeDeleteStage(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.batchService.rollbackStage(target.batch.id, target.stage.id).subscribe({
      next: () => { this.deleteTarget.set(null); this.loadBatches(); },
      error: () => this.deleteTarget.set(null)
    });
  }

  cancelDeleteStage(): void {
    this.deleteTarget.set(null);
  }

  canEditOrDelete(stage: BatchStageSummaryDto): boolean {
    return stage.status !== 2; // Not Completed
  }

  canDeleteStage(stage: BatchStageSummaryDto): boolean {
    return stage.status !== 2 && stage.stageType !== 0; // Not Completed and not Builder
  }

  // Delete full batch
  deleteBatchTarget = signal<BatchDto | null>(null);

  confirmDeleteBatch(batch: BatchDto, event: MouseEvent): void {
    event.stopPropagation();
    this.deleteBatchTarget.set(batch);
  }

  executeDeleteBatch(): void {
    const target = this.deleteBatchTarget();
    if (!target) return;
    this.batchService.delete(target.id).subscribe({
      next: () => { this.deleteBatchTarget.set(null); this.loadBatches(); },
      error: () => this.deleteBatchTarget.set(null)
    });
  }

  cancelDeleteBatch(): void {
    this.deleteBatchTarget.set(null);
  }

  navigateToStage(batch: BatchDto, stage: BatchStageSummaryDto): void {
    this.router.navigate([
      '/sqx/workflow', this.assetId, this.timeframe,
      'batch', batch.id, 'stage', stage.stageType
    ]);
  }

  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  openAdvanceModal(batch: BatchDto): void {
    this.advanceBatch.set(batch);
  }

  closeAdvanceModal(): void {
    this.advanceBatch.set(null);
  }

  onBatchCreated(): void {
    this.closeCreateModal();
    this.loadBatches();
  }

  onBatchAdvanced(): void {
    this.closeAdvanceModal();
    this.loadBatches();
  }

  goBack(): void {
    this.router.navigate(['/sqx/workflow']);
  }

  get assetTitle(): string {
    const first = this.batches()[0];
    if (!first) return '';
    return `${first.assetSymbol} (${first.assetName})`;
  }

  private loadBatches(): void {
    this.isLoading.set(true);
    this.batchService.getAll(this.assetId, this.timeframe).subscribe({
      next: (data) => {
        this.batches.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
