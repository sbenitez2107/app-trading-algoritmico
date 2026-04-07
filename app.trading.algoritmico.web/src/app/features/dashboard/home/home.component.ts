import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

interface KpiCard {
  titleKey: string;
  value: string;
  type: 'gain' | 'neutral' | 'stable';
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
  readonly kpiCards: KpiCard[] = [
    { titleKey: 'DASHBOARD.HOME.KPI_STRATEGIES', value: '+12.5%', type: 'gain' },
    { titleKey: 'DASHBOARD.HOME.KPI_MONTHLY_RETURN', value: '+8.2%', type: 'gain' },
    { titleKey: 'DASHBOARD.HOME.KPI_RISK_LEVEL', value: 'Stable', type: 'stable' }
  ];

  readonly taskProgress = { completed: 12, total: 14 };

  get taskProgressPercent(): number {
    return Math.round((this.taskProgress.completed / this.taskProgress.total) * 100);
  }
}
