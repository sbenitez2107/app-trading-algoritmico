import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { BatchService, STAGE_TYPE_LABELS, STAGE_STATUS_LABELS, TIMEFRAME_LABELS } from '../../../../core/services/batch.service';
import { BatchStageService, BatchStageDetailDto } from '../../../../core/services/batch-stage.service';
import { StrategyService, StrategyDto } from '../../../../core/services/strategy.service';

type KpiKey = 'returnDrawdownRatio' | 'sharpeRatio' | 'winRate' | 'profitFactor' | 'netProfit' | 'maxDrawdown';

@Component({
  selector: 'app-stage-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  templateUrl: './stage-detail.component.html',
  styleUrl: './stage-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StageDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly batchService = inject(BatchService);
  private readonly stageService = inject(BatchStageService);
  private readonly strategyService = inject(StrategyService);

  assetId = '';
  timeframe = 0;
  batchId = '';
  stageType = 0;
  stageId = '';

  stage = signal<BatchStageDetailDto | null>(null);
  strategies = signal<StrategyDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(true);
  expandedStrategy = signal<string | null>(null);
  rankBy = signal<KpiKey>('returnDrawdownRatio');
  bbName = signal('');

  readonly stageLabels = STAGE_TYPE_LABELS;
  readonly statusLabels = STAGE_STATUS_LABELS;
  readonly timeframeLabels = TIMEFRAME_LABELS;

  readonly kpiOptions: { key: KpiKey; label: string }[] = [
    { key: 'returnDrawdownRatio', label: 'Ret DD/Ratio' },
    { key: 'sharpeRatio', label: 'Sharpe Ratio' },
    { key: 'winRate', label: 'Win Rate' },
    { key: 'profitFactor', label: 'Profit Factor' },
    { key: 'netProfit', label: 'Net Profit' },
    { key: 'maxDrawdown', label: 'Max Drawdown' }
  ];

  readonly topStrategies = computed(() => {
    const key = this.rankBy();
    return [...this.strategies()]
      .filter(s => s[key] !== null && s[key] !== undefined)
      .sort((a, b) => ((b[key] as number) ?? 0) - ((a[key] as number) ?? 0))
      .slice(0, 10);
  });

  ngOnInit(): void {
    const params = this.route.snapshot.params;
    this.assetId = params['assetId'];
    this.timeframe = Number(params['timeframe']);
    this.batchId = params['batchId'];
    this.stageType = Number(params['stageType']);
    this.loadData();
  }

  togglePseudocode(strategyId: string): void {
    this.expandedStrategy.update(v => v === strategyId ? null : strategyId);
  }

  onKpiChange(strategy: StrategyDto, field: string, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const numValue = value ? Number(value) : undefined;
    if (numValue === undefined || isNaN(numValue)) return;

    this.strategyService.updateKpis(strategy.id, { [field]: numValue }).subscribe({
      next: (updated) => {
        this.strategies.update(list =>
          list.map(s => s.id === updated.id ? updated : s)
        );
      }
    });
  }

  onRankByChange(event: Event): void {
    this.rankBy.set((event.target as HTMLSelectElement).value as KpiKey);
  }

  goBack(): void {
    this.router.navigate(['/sqx/workflow', this.assetId, this.timeframe]);
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 0: return 'sd-status--pending';
      case 1: return 'sd-status--running';
      case 2: return 'sd-status--completed';
      default: return '';
    }
  }

  formatKpi(value: number | null): string {
    return value !== null && value !== undefined ? value.toFixed(2) : '—';
  }

  private loadData(): void {
    this.isLoading.set(true);

    // First load batch to find the stageId by stageType
    this.batchService.getById(this.batchId).subscribe({
      next: (batch) => {
        this.bbName.set(batch.buildingBlockName);
        const matchingStage = batch.stages.find(s => s.stageType === this.stageType);
        if (!matchingStage) {
          this.isLoading.set(false);
          return;
        }

        this.stageId = matchingStage.id;

        // Load stage detail
        this.stageService.getDetail(this.batchId, this.stageId).subscribe({
          next: (stageDetail) => this.stage.set(stageDetail)
        });

        // Load strategies
        this.strategyService.getByStage(this.batchId, this.stageId, 1, 200).subscribe({
          next: (result) => {
            this.strategies.set(result.items);
            this.totalCount.set(result.totalCount);
            this.isLoading.set(false);
          },
          error: () => this.isLoading.set(false)
        });
      },
      error: () => this.isLoading.set(false)
    });
  }
}
