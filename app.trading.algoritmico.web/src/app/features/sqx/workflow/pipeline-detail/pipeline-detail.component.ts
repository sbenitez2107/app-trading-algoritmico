import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService, BatchDto, STAGE_TYPE_LABELS, STAGE_STATUS_LABELS, TIMEFRAME_LABELS, BatchStageSummaryDto } from '../../../../core/services/batch.service';
import { AssetService } from '../../../../core/services/asset.service';
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

  assetId = '';
  timeframe = 0;
  batches = signal<BatchDto[]>([]);
  isLoading = signal(true);
  showCreateModal = signal(false);
  advanceBatchId = signal<string | null>(null);

  readonly stageLabels = STAGE_TYPE_LABELS;
  readonly statusLabels = STAGE_STATUS_LABELS;
  readonly timeframeLabels = TIMEFRAME_LABELS;
  readonly stageTypes = [0, 1, 2, 3, 4];

  ngOnInit(): void {
    this.assetId = this.route.snapshot.params['assetId'];
    this.timeframe = Number(this.route.snapshot.params['timeframe']);
    this.loadBatches();
  }

  getStage(batch: BatchDto, stageType: number): BatchStageSummaryDto | undefined {
    return batch.stages.find(s => s.stageType === stageType);
  }

  getStatusClass(stage: BatchStageSummaryDto | undefined): string {
    if (!stage) return 'cell--empty';
    switch (stage.status) {
      case 0: return 'cell--pending';
      case 1: return 'cell--in-progress';
      case 2: return 'cell--completed';
      default: return 'cell--empty';
    }
  }

  getStatusIcon(stage: BatchStageSummaryDto | undefined): string {
    if (!stage) return '—';
    switch (stage.status) {
      case 0: return '○';
      case 1: return '⏳';
      case 2: return '✅';
      default: return '—';
    }
  }

  canAdvance(batch: BatchDto): boolean {
    if (batch.stages.length === 0) return false;
    const latest = [...batch.stages].sort((a, b) => b.stageType - a.stageType)[0];
    return latest.stageType < 4; // Not at Live yet
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

  openAdvanceModal(batchId: string): void {
    this.advanceBatchId.set(batchId);
  }

  closeAdvanceModal(): void {
    this.advanceBatchId.set(null);
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
