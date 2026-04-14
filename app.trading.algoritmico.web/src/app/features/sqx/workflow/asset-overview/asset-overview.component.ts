import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { CdkDropList, CdkDrag, CdkDragDrop, CdkDragPlaceholder, moveItemInArray } from '@angular/cdk/drag-drop';
import { forkJoin } from 'rxjs';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, ICellRendererParams, RowClickedEvent, themeQuartz } from 'ag-grid-community';
import { BatchService, BatchDto, STAGE_TYPE_LABELS, STAGE_STATUS_LABELS, TIMEFRAME_LABELS } from '../../../../core/services/batch.service';
import { AssetService, AssetDto } from '../../../../core/services/asset.service';

interface TimeframeGroup {
  timeframe: number;
  batchCount: number;
  stagesSummary: { type: number; count: number; hasRunning: boolean; hasCompleted: boolean }[];
  activeBatch: BatchDto | null;
}

interface AssetGroup {
  assetId: string;
  assetName: string;
  assetSymbol: string;
  timeframes: TimeframeGroup[];
  totalBatches: number;
}

type CardItem =
  | { kind: 'withBatches'; id: string; group: AssetGroup }
  | { kind: 'empty'; id: string; asset: AssetDto };

interface BatchRow {
  batchId: string;
  assetId: string;
  assetSymbol: string;
  assetName: string;
  timeframe: number;
  timeframeLabel: string;
  buildingBlockName: string;
  batchName: string | null;
  latestStageType: number;
  latestStageLabel: string;
  latestStatus: number;
  latestStatusLabel: string;
  inputCount: number;
  outputCount: number;
  createdAt: string;
}

const STORAGE_KEY = 'bent_asset_card_order';

@Component({
  selector: 'app-asset-overview',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, CdkDropList, CdkDrag, CdkDragPlaceholder, AgGridAngular],
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
  savedOrder = signal<string[]>(this.loadOrder());

  // AG Grid
  readonly gridTheme = themeQuartz;

  readonly defaultColDef: ColDef<BatchRow> = {
    sortable: true,
    filter: true,
    resizable: true,
    suppressHeaderMenuButton: true,
    floatingFilter: true,
  };

  readonly columnDefs: ColDef<BatchRow>[] = [
    {
      headerName: 'Asset',
      field: 'assetName',
      flex: 1,
      minWidth: 120,
      getQuickFilterText: p => `${p.data!.assetSymbol} ${p.data!.assetName}`,
    },
    {
      headerName: 'TF',
      field: 'timeframeLabel',
      width: 90,
      cellRenderer: (p: ICellRendererParams<BatchRow>) => {
        const el = document.createElement('span');
        el.className = 'wf-ag-tf';
        el.textContent = p.data!.timeframeLabel;
        return el;
      },
    },
    {
      headerName: 'Building Block',
      field: 'buildingBlockName',
      flex: 1,
      minWidth: 140,
    },
    {
      headerName: 'Name',
      field: 'batchName',
      flex: 1,
      minWidth: 120,
      cellClass: 'wf-ag-muted',
      valueFormatter: p => p.value ?? '—',
    },
    {
      headerName: 'Stage',
      field: 'latestStageLabel',
      width: 110,
      cellRenderer: (p: ICellRendererParams<BatchRow>) => {
        const el = document.createElement('span');
        el.className = 'wf-ag-stage';
        el.textContent = p.data!.latestStageLabel;
        return el;
      },
      comparator: (_, __, a, b) => (a.data?.latestStageType ?? 0) - (b.data?.latestStageType ?? 0),
    },
    {
      headerName: 'Status',
      field: 'latestStatusLabel',
      width: 120,
      cellRenderer: (p: ICellRendererParams<BatchRow>) => {
        const status = p.data!.latestStatus;
        const cls = status === 0 ? 'wf-ag-status--pending'
                  : status === 1 ? 'wf-ag-status--running'
                  : 'wf-ag-status--completed';
        const el = document.createElement('span');
        el.className = `wf-ag-status ${cls}`;
        el.textContent = p.data!.latestStatusLabel;
        return el;
      },
      comparator: (_, __, a, b) => (a.data?.latestStatus ?? 0) - (b.data?.latestStatus ?? 0),
    },
    {
      headerName: 'Strategies',
      field: 'outputCount',
      width: 120,
      filter: false,
      cellClass: 'wf-ag-muted',
      valueFormatter: p => {
        const d = p.data!;
        return d.latestStageType === 0 ? String(d.outputCount) : `${d.inputCount} → ${d.outputCount}`;
      },
    },
    {
      headerName: 'Created',
      field: 'createdAt',
      width: 110,
      filter: false,
      cellClass: 'wf-ag-muted',
      valueFormatter: p => {
        const d = new Date(p.value);
        return `${d.getDate().toString().padStart(2,'0')}/${(d.getMonth()+1).toString().padStart(2,'0')}/${d.getFullYear().toString().slice(2)}`;
      },
      sort: 'desc',
    },
  ];

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
    const assetMap = new Map<string, AssetGroup>();

    for (const batch of batchList) {
      if (!assetMap.has(batch.assetId)) {
        assetMap.set(batch.assetId, {
          assetId: batch.assetId,
          assetName: batch.assetName,
          assetSymbol: batch.assetSymbol,
          timeframes: [],
          totalBatches: 0
        });
      }
      const asset = assetMap.get(batch.assetId)!;
      asset.totalBatches++;

      let tfGroup = asset.timeframes.find(t => t.timeframe === batch.timeframe);
      if (!tfGroup) {
        tfGroup = {
          timeframe: batch.timeframe,
          batchCount: 0,
          stagesSummary: this.stageTypes.map(t => ({ type: t, count: 0, hasRunning: false, hasCompleted: false })),
          activeBatch: null
        };
        asset.timeframes.push(tfGroup);
      }
      tfGroup.batchCount++;

      for (const stage of batch.stages) {
        const summary = tfGroup.stagesSummary.find(s => s.type === stage.stageType);
        if (summary) {
          summary.count++;
          if (stage.status === 1) summary.hasRunning = true;
          if (stage.status === 2) summary.hasCompleted = true;
        }
      }

      if (!tfGroup.activeBatch || batch.stages.some(s => s.status === 1)) {
        tfGroup.activeBatch = batch;
      }
    }

    for (const asset of assetMap.values()) {
      asset.timeframes.sort((a, b) => a.timeframe - b.timeframe);
    }

    return [...assetMap.values()].sort((a, b) => a.assetName.localeCompare(b.assetName));
  });

  readonly assetsWithoutBatches = computed(() => {
    const batchAssetIds = new Set(this.batches().map(b => b.assetId));
    return this.assets().filter(a => !batchAssetIds.has(a.id));
  });

  readonly orderedCards = computed<CardItem[]>(() => {
    const withBatches: CardItem[] = this.assetGroups().map(g => ({
      kind: 'withBatches' as const, id: g.assetId, group: g
    }));
    const empty: CardItem[] = this.assetsWithoutBatches().map(a => ({
      kind: 'empty' as const, id: a.id, asset: a
    }));

    const all = [...withBatches, ...empty];
    const order = this.savedOrder();
    if (order.length === 0) return all;

    const byId = new Map(all.map(c => [c.id, c]));
    const ordered: CardItem[] = [];
    for (const id of order) {
      const item = byId.get(id);
      if (item) { ordered.push(item); byId.delete(id); }
    }
    for (const remaining of byId.values()) {
      ordered.push(remaining);
    }
    return ordered;
  });

  readonly allBatchRows = computed<BatchRow[]>(() =>
    this.batches().map(b => {
      const latest = [...b.stages].sort((a, z) => z.stageType - a.stageType)[0];
      return {
        batchId: b.id,
        assetId: b.assetId,
        assetSymbol: b.assetSymbol,
        assetName: b.assetName,
        timeframe: b.timeframe,
        timeframeLabel: TIMEFRAME_LABELS[b.timeframe] ?? '—',
        buildingBlockName: b.buildingBlockName,
        batchName: b.name,
        latestStageType: latest?.stageType ?? -1,
        latestStageLabel: latest ? (STAGE_TYPE_LABELS[latest.stageType] ?? '—') : '—',
        latestStatus: latest?.status ?? -1,
        latestStatusLabel: latest ? (STAGE_STATUS_LABELS[latest.status] ?? '—') : '—',
        inputCount: latest?.inputCount ?? 0,
        outputCount: latest?.outputCount ?? 0,
        createdAt: b.createdAt
      };
    })
  );

  onRowClicked(event: RowClickedEvent<BatchRow>): void {
    const row = event.data;
    if (row) this.router.navigate(['/sqx/workflow', row.assetId, row.timeframe]);
  }

  ngOnInit(): void {
    this.loadData();
  }

  onCardDrop(event: CdkDragDrop<CardItem[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const cards = [...this.orderedCards()];
    moveItemInArray(cards, event.previousIndex, event.currentIndex);
    this.persistOrder(cards.map(c => c.id));
  }

  navigateToTimeframe(assetId: string, timeframe: number): void {
    this.router.navigate(['/sqx/workflow', assetId, timeframe]);
  }

  getStageDotClass(summary: { count: number; hasRunning: boolean; hasCompleted: boolean }): string {
    if (summary.hasRunning) return 'wf-dot--running';
    if (summary.hasCompleted) return 'wf-dot--completed';
    if (summary.count > 0) return 'wf-dot--has-data';
    return 'wf-dot--empty';
  }

  getActiveStageLabel(batch: BatchDto | null): string {
    if (!batch) return '—';
    const running = batch.stages.find(s => s.status === 1);
    if (running) return this.stageLabels[running.stageType] ?? '—';
    const latest = [...batch.stages].sort((a, b) => b.stageType - a.stageType)[0];
    return latest ? this.stageLabels[latest.stageType] ?? '—' : '—';
  }

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

  private loadOrder(): string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }

  private persistOrder(ids: string[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(ids));
    this.savedOrder.set(ids);
  }

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
