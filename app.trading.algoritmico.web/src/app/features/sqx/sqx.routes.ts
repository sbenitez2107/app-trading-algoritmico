import { Routes } from '@angular/router';

export const SQX_ROUTES: Routes = [
  {
    path: 'workflow',
    loadComponent: () =>
      import('./workflow/asset-overview/asset-overview.component').then(m => m.AssetOverviewComponent)
  },
  {
    path: 'workflow/:assetId/:timeframe',
    loadComponent: () =>
      import('./workflow/pipeline-detail/pipeline-detail.component').then(m => m.PipelineDetailComponent)
  },
  {
    path: 'workflow/:assetId/:timeframe/batch/:batchId/stage/:stageType',
    loadComponent: () =>
      import('./workflow/stage-detail/stage-detail.component').then(m => m.StageDetailComponent)
  },
  {
    path: 'building-blocks',
    loadComponent: () =>
      import('./building-blocks/building-blocks-list/building-blocks-list.component').then(m => m.BuildingBlocksListComponent)
  },
  {
    path: 'strategy-analyzer',
    loadComponent: () =>
      import('./strategy-analyzer/strategy-analyzer.component').then(m => m.StrategyAnalyzerComponent),
  },
  { path: '', redirectTo: 'workflow', pathMatch: 'full' }
];
