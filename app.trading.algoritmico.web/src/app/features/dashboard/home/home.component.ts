import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService, BatchDto, STAGE_TYPE_LABELS, TIMEFRAME_LABELS } from '../../../core/services/batch.service';

interface RunningStageItem {
  batchId: string;
  stageId: string;
  assetId: string;
  assetSymbol: string;
  assetName: string;
  timeframe: number;
  timeframeLabel: string;
  buildingBlockName: string;
  stageType: number;
  stageLabel: string;
  inputCount: number;
  outputCount: number;
  startedAt: string | null;
  elapsed: string;
}

interface PendingStageItem {
  batchId: string;
  stageId: string;
  assetId: string;
  assetSymbol: string;
  assetName: string;
  timeframe: number;
  timeframeLabel: string;
  buildingBlockName: string;
  stageType: number;
  stageLabel: string;
  inputCount: number;
  outputCount: number;
  since: string | null;
  age: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent {
  private readonly batchService = inject(BatchService);
  private readonly router = inject(Router);

  batches = signal<BatchDto[]>([]);
  isLoading = signal(true);

  readonly pendingStages = computed<PendingStageItem[]>(() => {
    const items: PendingStageItem[] = [];
    for (const batch of this.batches()) {
      for (const stage of batch.stages) {
        if (stage.status === 0) {
          const since = stage.updatedAt ?? null;
          items.push({
            batchId: batch.id,
            stageId: stage.id,
            assetId: batch.assetId,
            assetSymbol: batch.assetSymbol,
            assetName: batch.assetName,
            timeframe: batch.timeframe,
            timeframeLabel: TIMEFRAME_LABELS[batch.timeframe] ?? '—',
            buildingBlockName: batch.buildingBlockName,
            stageType: stage.stageType,
            stageLabel: STAGE_TYPE_LABELS[stage.stageType] ?? '—',
            inputCount: stage.inputCount,
            outputCount: stage.outputCount,
            since,
            age: this.getElapsed(since)
          });
        }
      }
    }
    return items.sort((a, b) => {
      const aTime = a.since ? new Date(a.since).getTime() : 0;
      const bTime = b.since ? new Date(b.since).getTime() : 0;
      return aTime - bTime; // oldest first
    });
  });

  readonly runningStages = computed<RunningStageItem[]>(() => {
    const items: RunningStageItem[] = [];
    for (const batch of this.batches()) {
      for (const stage of batch.stages) {
        if (stage.status === 1) {
          items.push({
            batchId: batch.id,
            stageId: stage.id,
            assetId: batch.assetId,
            assetSymbol: batch.assetSymbol,
            assetName: batch.assetName,
            timeframe: batch.timeframe,
            timeframeLabel: TIMEFRAME_LABELS[batch.timeframe] ?? '—',
            buildingBlockName: batch.buildingBlockName,
            stageType: stage.stageType,
            stageLabel: STAGE_TYPE_LABELS[stage.stageType] ?? '—',
            inputCount: stage.inputCount,
            outputCount: stage.outputCount,
            startedAt: stage.runningStartedAt,
            elapsed: this.getElapsed(stage.runningStartedAt)
          });
        }
      }
    }
    return items.sort((a, b) => a.assetSymbol.localeCompare(b.assetSymbol));
  });

  ngOnInit(): void {
    this.batchService.getAll().subscribe({
      next: (data) => {
        this.batches.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  goToPipeline(item: RunningStageItem | PendingStageItem): void {
    this.router.navigate(['/sqx/workflow', item.assetId, item.timeframe]);
  }

  goToStage(item: RunningStageItem | PendingStageItem, event: MouseEvent): void {
    event.stopPropagation();
    this.router.navigate(['/sqx/workflow', item.assetId, item.timeframe, 'batch', item.batchId, 'stage', item.stageType]);
  }

  private getElapsed(startedAt: string | null): string {
    if (!startedAt) return '';
    const start = new Date(startedAt).getTime();
    const now = Date.now();
    const diffMs = now - start;
    const mins = Math.floor(diffMs / 60000);
    if (mins < 1) return '<1m';
    if (mins < 60) return `${mins}m`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h ${mins % 60}m`;
    const days = Math.floor(hours / 24);
    return `${days}d ${hours % 24}h`;
  }
}
