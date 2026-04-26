import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KpiInfo } from './kpi-info';

/**
 * Renders a `?` icon next to a KPI label that, on hover, shows a tooltip
 * with the metric definition and good/bad interpretation guide.
 *
 * CSS-only tooltip — no Angular CDK dependency, no JS positioning. Good
 * enough for desktop usage; on mobile the icon stays inert (no hover).
 */
@Component({
  selector: 'app-kpi-help',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="kpi-help" tabindex="0" [attr.aria-label]="info().what">
      <span class="kpi-help__icon" aria-hidden="true">?</span>
      <span class="kpi-help__tooltip" role="tooltip">
        <span class="kpi-help__what">{{ info().what }}</span>
        @if (info().good) {
          <span class="kpi-help__good">{{ info().good }}</span>
        }
      </span>
    </span>
  `,
  styles: [
    `
      .kpi-help {
        position: relative;
        display: inline-flex;
        align-items: center;
        margin-left: 0.25rem;
        cursor: help;
      }

      .kpi-help__icon {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 1rem;
        height: 1rem;
        border-radius: 50%;
        background: var(--color-border, #313244);
        color: var(--color-text-secondary, #a6adc8);
        font-size: 0.6875rem;
        font-weight: 700;
        line-height: 1;
        user-select: none;
      }

      .kpi-help:hover .kpi-help__icon,
      .kpi-help:focus-visible .kpi-help__icon {
        background: var(--color-accent, #89b4fa);
        color: #fff;
      }

      .kpi-help__tooltip {
        // Hidden by default. Becomes visible on :hover/:focus-within.
        position: absolute;
        bottom: calc(100% + 0.5rem);
        left: 50%;
        transform: translateX(-50%) translateY(0.25rem);
        z-index: 10;
        min-width: 16rem;
        max-width: 22rem;
        padding: 0.625rem 0.75rem;
        background: var(--color-surface, #1e1e2e);
        border: 1px solid var(--color-border, #313244);
        border-radius: 6px;
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
        color: var(--color-text-primary, #cdd6f4);
        font-size: 0.75rem;
        font-weight: 400;
        line-height: 1.45;
        text-transform: none;
        letter-spacing: normal;
        text-align: left;
        white-space: normal;
        opacity: 0;
        pointer-events: none;
        visibility: hidden;
        transition:
          opacity 0.12s ease,
          transform 0.12s ease,
          visibility 0s linear 0.12s;
      }

      .kpi-help:hover .kpi-help__tooltip,
      .kpi-help:focus-within .kpi-help__tooltip {
        opacity: 1;
        transform: translateX(-50%) translateY(0);
        visibility: visible;
        transition-delay: 0s;
      }

      .kpi-help__what {
        display: block;
      }

      .kpi-help__good {
        display: block;
        margin-top: 0.375rem;
        padding-top: 0.375rem;
        border-top: 1px solid var(--color-border, #313244);
        color: var(--color-text-secondary, #a6adc8);
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiHelpComponent {
  readonly info = input.required<KpiInfo>();
}
