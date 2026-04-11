import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { BatchService, BatchDto, STAGE_TYPE_LABELS, STAGE_STATUS_LABELS, TIMEFRAME_LABELS } from '../../../../core/services/batch.service';
import { AssetService, AssetDto } from '../../../../core/services/asset.service';

interface AssetGroup {
  assetId: string;
  assetName: string;
  assetSymbol: string;
  timeframe: number;
  batches: BatchDto[];
  stagesSummary: { type: number; count: number; hasInProgress: boolean; hasCompleted: boolean }[];
  activeBatch: BatchDto | null;
}

@Component({
  selector: 'app-asset-overview',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule],
  templateUrl: './asset-overview.component.html',
  styleUrl: './asset-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssetOverviewComponent {
  private readonly fb = inject(FormBuilder);
  private readonly batchService = inject(BatchService);
  private readonly assetService = inject(AssetService);
  private readonly router = inject(Router);

  batches = signal<BatchDto[]>([]);
  assets = signal<AssetDto[]>([]);
  isLoading = signal(true);
  showAssetModal = signal(false);
  isCreatingAsset = signal(false);
  assetError = signal<string | null>(null);

  assetForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    symbol: ['', [Validators.required, Validators.maxLength(50)]]
  });

  readonly stageLabels = STAGE_TYPE_LABELS;
  readonly statusLabels = STAGE_STATUS_LABELS;
  readonly timeframeLabels = TIMEFRAME_LABELS;
  readonly stageTypes = [0, 1, 2, 3, 4];

  readonly timeframeOptions = Object.entries(TIMEFRAME_LABELS).map(([value, label]) => ({
    value: Number(value),
    label
  }));

  readonly assetGroups = computed<AssetGroup[]>(() => {
    const batchList = this.batches();
    const groups = new Map<string, AssetGroup>();

    for (const batch of batchList) {
      const key = `${batch.assetId}-${batch.timeframe}`;
      if (!groups.has(key)) {
        groups.set(key, {
          assetId: batch.assetId,
          assetName: batch.assetName,
          assetSymbol: batch.assetSymbol,
          timeframe: batch.timeframe,
          batches: [],
          stagesSummary: this.stageTypes.map(t => ({ type: t, count: 0, hasInProgress: false, hasCompleted: false })),
          activeBatch: null
        });
      }
      const group = groups.get(key)!;
      group.batches.push(batch);

      for (const stage of batch.stages) {
        const summary = group.stagesSummary.find(s => s.type === stage.stageType);
        if (summary) {
          summary.count++;
          if (stage.status === 1) summary.hasInProgress = true;
          if (stage.status === 2) summary.hasCompleted = true;
        }
      }
    }

    for (const group of groups.values()) {
      group.activeBatch = group.batches.find(b =>
        b.stages.some(s => s.status === 1)
      ) ?? group.batches[0] ?? null;
    }

    return [...groups.values()].sort((a, b) => a.assetName.localeCompare(b.assetName));
  });

  ngOnInit(): void {
    this.loadData();
  }

  navigateToDetail(group: AssetGroup): void {
    this.router.navigate(['/sqx/workflow', group.assetId, group.timeframe]);
  }

  navigateToAsset(asset: AssetDto, timeframe: number): void {
    this.router.navigate(['/sqx/workflow', asset.id, timeframe]);
  }

  getStageDotClass(summary: { count: number; hasInProgress: boolean; hasCompleted: boolean }): string {
    if (summary.hasInProgress) return 'wf-dot--in-progress';
    if (summary.hasCompleted) return 'wf-dot--completed';
    if (summary.count > 0) return 'wf-dot--has-data';
    return 'wf-dot--empty';
  }

  getActiveStageLabel(batch: BatchDto | null): string {
    if (!batch) return '—';
    const inProgress = batch.stages.find(s => s.status === 1);
    if (inProgress) return this.stageLabels[inProgress.stageType] ?? '—';
    const latest = [...batch.stages].sort((a, b) => b.stageType - a.stageType)[0];
    return latest ? this.stageLabels[latest.stageType] ?? '—' : '—';
  }

  // Asset modal
  openAssetModal(): void {
    this.assetForm.reset();
    this.assetError.set(null);
    this.showAssetModal.set(true);
  }

  closeAssetModal(): void {
    this.showAssetModal.set(false);
  }

  onCreateAsset(): void {
    if (this.assetForm.invalid) {
      this.assetForm.markAllAsTouched();
      return;
    }

    this.isCreatingAsset.set(true);
    this.assetError.set(null);

    this.assetService.create({
      name: this.assetForm.value.name!,
      symbol: this.assetForm.value.symbol!.toUpperCase()
    }).subscribe({
      next: () => {
        this.isCreatingAsset.set(false);
        this.closeAssetModal();
        this.loadData();
      },
      error: (err) => {
        this.isCreatingAsset.set(false);
        this.assetError.set(err?.error?.message ?? 'COMMON.STATUS.ERROR');
      }
    });
  }

  onAssetModalBackdrop(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('wf-modal-backdrop')) {
      this.closeAssetModal();
    }
  }

  // Assets without batches (show them as standalone cards to allow navigation)
  readonly assetsWithoutBatches = computed(() => {
    const batchAssetIds = new Set(this.batches().map(b => b.assetId));
    return this.assets().filter(a => !batchAssetIds.has(a.id));
  });

  private loadData(): void {
    this.isLoading.set(true);
    forkJoin({
      batches: this.batchService.getAll(),
      assets: this.assetService.getAll()
    }).subscribe({
      next: ({ batches, assets }) => {
        this.batches.set(batches);
        this.assets.set(assets);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
