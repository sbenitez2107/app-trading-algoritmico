import { Component, ChangeDetectionStrategy, Signal, signal, computed } from '@angular/core';
import { ITooltipAngularComp } from 'ag-grid-angular';
import { ITooltipParams } from 'ag-grid-community';
import { MonthlyReturnDto, PortfolioSummaryDto } from '../../../core/services/portfolio.service';
import { MonthlyHeatmapComponent } from '../monthly-heatmap/monthly-heatmap.component';

/**
 * Read-only view over the list component's lazily-loaded monthly-returns cache.
 * Passed through `tooltipComponentParams` so the tooltip never fetches on its own —
 * one broker-wide request serves every row.
 */
export interface PortfolioMonthlySource {
  monthlyById: Signal<Map<string, MonthlyReturnDto[]>>;
  monthlyLoading: Signal<boolean>;
  monthlyError: Signal<string | null>;
}

type TooltipParams = ITooltipParams<PortfolioSummaryDto> & { source: PortfolioMonthlySource };

/**
 * AG Grid tooltip showing a portfolio's monthly returns heatmap without leaving the list.
 * Reuses the same heatmap component rendered in the portfolio detail page.
 */
@Component({
  selector: 'app-portfolio-monthly-tooltip',
  standalone: true,
  imports: [MonthlyHeatmapComponent],
  template: `
    <div class="monthly-tooltip">
      <div class="monthly-tooltip__title">{{ portfolioName() }}</div>
      @if (source()?.monthlyLoading()) {
        <p class="monthly-tooltip__msg">Cargando retorno mensual...</p>
      } @else if (source()?.monthlyError(); as err) {
        <p class="monthly-tooltip__msg monthly-tooltip__msg--error">{{ err }}</p>
      } @else {
        <app-monthly-heatmap [returns]="returns()" />
      }
    </div>
  `,
  styles: [
    `
      .monthly-tooltip {
        background: var(--bg-surface, #1e1e2e);
        border: 1px solid var(--color-border, #313244);
        border-radius: var(--radius-md, 8px);
        box-shadow: 0 8px 24px rgb(0 0 0 / 35%);
        padding: 10px 12px;
        max-width: min(90vw, 900px);
        overflow-x: auto;
      }

      .monthly-tooltip__title {
        color: var(--text-main);
        font-size: 0.78rem;
        font-weight: 700;
        margin-bottom: 8px;
        white-space: nowrap;
      }

      .monthly-tooltip__msg {
        color: var(--text-muted);
        font-size: 0.78rem;
        margin: 0;
      }

      .monthly-tooltip__msg--error {
        color: #ff3b30;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfolioMonthlyTooltipComponent implements ITooltipAngularComp {
  private readonly params = signal<TooltipParams | null>(null);

  readonly source = computed(() => this.params()?.source ?? null);
  readonly portfolioName = computed(() => this.params()?.data?.name ?? '');

  readonly returns = computed<MonthlyReturnDto[]>(() => {
    const id = this.params()?.data?.id;
    const src = this.source();
    if (!id || !src) return [];
    return src.monthlyById().get(id) ?? [];
  });

  agInit(params: TooltipParams): void {
    this.params.set(params);
  }
}
