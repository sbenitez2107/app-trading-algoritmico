import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import {
  BACKTEST_SEGMENT_LABELS,
  BacktestRunKind,
  BacktestSegment,
  BacktestService,
  GROUP_RISK_MEMBER_STATUS_LABELS,
  GROUP_RISK_STATUS_LABELS,
  GroupRiskAnalysisDto,
  GroupRiskAnalysisStatus,
  VAR_WITHHOLD_LABELS,
  VarWithholdReason,
} from '../../../../core/services/backtest.service';

/**
 * Correlation and VaR over the strategies of one named group, computed over ONE named sample.
 *
 * WHY THIS PANEL EXISTS AT ALL: the shipped portfolio risk card cannot render a withheld figure,
 * because its VaR fields are non-nullable and a missing number would arrive as `0` — which reads as
 * "this group loses nothing at the 5th percentile". Both committed fixtures measure a daily VaR95
 * the data cannot support, so that `0` is not hypothetical: it is what this UI is here to prevent,
 * and this template is the LAST place it could still happen.
 *
 * How it is prevented, structurally rather than by care: every figure goes through `@if (x !== null)
 * { number } @else { reason }`. There is no `?? 0`, no `| number` applied to a nullable, and no
 * default in the type — `dailyVar95` is `number | null`, so a template that forgot the branch would
 * render an empty cell rather than a plausible zero. The tests assert the withheld cell contains no
 * digit at all.
 *
 * A REFUSAL is not an error state either. The server answers a 400/404/422 with the same payload,
 * carrying which member and why, so the panel renders that evidence instead of a generic failure.
 */
@Component({
  selector: 'app-group-risk-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  templateUrl: './group-risk-panel.component.html',
  styleUrl: './group-risk-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupRiskPanelComponent {
  private readonly backtestService = inject(BacktestService);

  /** The group's members. The panel evaluates exactly this group and never ranks alternatives. */
  readonly strategyIds = input.required<string[]>();

  /**
   * Starts as null, and stays expressible as null. Sending `0` would be asking for
   * `BacktestSegment.Unknown` — a DIFFERENT request from not having chosen, refused by the server
   * for its own reason.
   */
  readonly segment = signal<BacktestSegment | null>(null);
  readonly runKind = signal<BacktestRunKind | null>(null);
  readonly initialCapital = signal(10000);
  readonly targetRiskPerTrade = signal(200);
  readonly fundingService = signal('');

  readonly analysis = signal<GroupRiskAnalysisDto | null>(null);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  readonly segmentOptions: BacktestSegment[] = [
    BacktestSegment.InSample,
    BacktestSegment.OutOfSample,
    BacktestSegment.InSampleTest,
  ];

  readonly runKindOptions: BacktestRunKind[] = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

  readonly segmentLabels = BACKTEST_SEGMENT_LABELS;
  readonly withholdLabels = VAR_WITHHOLD_LABELS;
  readonly statusLabels = GROUP_RISK_STATUS_LABELS;
  readonly memberStatusLabels = GROUP_RISK_MEMBER_STATUS_LABELS;

  readonly isCompleted = computed(
    () => this.analysis()?.status === GroupRiskAnalysisStatus.Completed,
  );

  /** A refusal is anything that is not `Completed`. There is no partial figure. */
  readonly refusal = computed(() => {
    const current = this.analysis();
    return current && current.status !== GroupRiskAnalysisStatus.Completed ? current : null;
  });

  analyze(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.backtestService
      .getGroupRisk({
        strategyIds: this.strategyIds(),
        initialCapital: this.initialCapital(),
        targetRiskPerTrade: this.targetRiskPerTrade(),
        // `?? undefined`, never `?? 0`: an omitted parameter and Unknown are different requests.
        segment: this.segment() ?? undefined,
        runKind: this.runKind() ?? undefined,
        fundingService: this.fundingService() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.isLoading.set(false);
          this.analysis.set(result);
        },
        error: () => {
          this.isLoading.set(false);
          this.analysis.set(null);
          this.error.set('SQX.BACKTESTS.GROUP_RISK.REQUEST_ERROR');
        },
      });
  }

  /** The i18n key for a withheld figure. Never returns anything that renders as a number. */
  withholdKey(reason: VarWithholdReason): string {
    return this.withholdLabels[reason] ?? 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_NO_SERIES';
  }

  segmentKey(segment: BacktestSegment | null): string {
    return segment === null ? 'SQX.BACKTESTS.GROUP_RISK.SEGMENT_NONE' : this.segmentLabels[segment];
  }
}
