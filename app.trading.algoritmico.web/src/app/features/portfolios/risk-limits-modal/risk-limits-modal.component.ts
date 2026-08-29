import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  output,
  signal,
  computed,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import {
  PortfolioService,
  FundingService,
  DrawdownModel,
  GuardrailKind,
  ServiceGuardrailDto,
  UpsertBrokerRiskLimitsDto,
} from '../../../core/services/portfolio.service';

/**
 * Extracted risk-limits editor modal (per `advance-stage-modal` convention — typed reactive form,
 * OnPush, input()/output()). The visible field set switches by the selected funding service:
 * Darwinex Zero shows the VarTarget band (target/floor VaR); every other service shows the
 * LossLimits breach-style fields — mirroring `RiskLimitsService.UpsertAsync`'s kind-aware
 * validation (`funding-guardrails` spec). Copy stays hardcoded Spanish: the `features/portfolios`
 * tree has zero ngx-translate usage (design decision — pre-existing debt, not addressed here).
 */
@Component({
  selector: 'app-risk-limits-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './risk-limits-modal.component.html',
  styleUrl: './risk-limits-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RiskLimitsModalComponent {
  readonly guardrail = input.required<ServiceGuardrailDto>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PortfolioService);

  readonly FundingService = FundingService;
  readonly DrawdownModel = DrawdownModel;

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /** Tracks the SELECTED funding service (not necessarily the guardrail's original one) so the
   * field set toggles live when the user switches broker in the dropdown. */
  private readonly selectedFundingService = signal<FundingService>(FundingService.Other);

  /** Darwinex Zero -> VarTarget field set; every other service -> LossLimits field set. */
  readonly isVarTarget = computed(
    () => this.selectedFundingService() === FundingService.DarwinexZero,
  );

  readonly form = this.fb.group({
    fundingService: [FundingService.Other],
    dailyLossPct: [null as number | null],
    maxLossPct: [null as number | null],
    profitTargetPct: [null as number | null],
    drawdownModel: [DrawdownModel.Static],
    targetVarPct: [null as number | null],
    varFloorPct: [null as number | null],
    verified: [false],
  });

  constructor() {
    effect(() => {
      const g = this.guardrail();
      this.errorMessage.set(null);
      this.selectedFundingService.set(g.fundingService);

      if (g.kind === GuardrailKind.VarTarget) {
        this.form.reset({
          fundingService: g.fundingService,
          dailyLossPct: null,
          maxLossPct: null,
          profitTargetPct: null,
          drawdownModel: DrawdownModel.Static,
          targetVarPct: g.varTarget.targetVarPct != null ? g.varTarget.targetVarPct * 100 : null,
          varFloorPct: g.varTarget.varFloorPct != null ? g.varTarget.varFloorPct * 100 : null,
          verified: g.verified,
        });
      } else {
        this.form.reset({
          fundingService: g.fundingService,
          dailyLossPct: g.dailyLossLimitPct != null ? g.dailyLossLimitPct * 100 : null,
          maxLossPct: g.maxLossLimitPct != null ? g.maxLossLimitPct * 100 : null,
          profitTargetPct: g.profitTargetPct != null ? g.profitTargetPct * 100 : null,
          drawdownModel: g.drawdownModel ?? DrawdownModel.Static,
          targetVarPct: null,
          varFloorPct: null,
          verified: g.verified,
        });
      }
    });

    this.form.controls.fundingService.valueChanges.subscribe((v) => {
      if (v !== null && v !== undefined) this.selectedFundingService.set(v);
    });
  }

  save(): void {
    this.errorMessage.set(null);
    const f = this.form.value;

    if (this.isVarTarget()) {
      const targetVarPct = this.toFraction(f.targetVarPct);
      const varFloorPct = this.toFraction(f.varFloorPct);

      if (targetVarPct == null || varFloorPct == null) {
        this.errorMessage.set('VaR objetivo y floor son obligatorios para Darwinex Zero.');
        return;
      }
      if (targetVarPct <= 0 || targetVarPct > 1 || varFloorPct <= 0 || varFloorPct > 1) {
        this.errorMessage.set('Los porcentajes de VaR deben estar entre 0% (exclusivo) y 100%.');
        return;
      }
      if (varFloorPct > targetVarPct) {
        this.errorMessage.set('El floor de VaR no puede superar al target.');
        return;
      }

      this.submit({
        broker: this.guardrail().service,
        fundingService: f.fundingService ?? FundingService.DarwinexZero,
        kind: GuardrailKind.VarTarget,
        dailyLossLimitPct: undefined,
        maxLossLimitPct: undefined,
        profitTargetPct: undefined,
        drawdownModel: DrawdownModel.Static,
        targetVarPct,
        varFloorPct,
        verified: f.verified ?? false,
      });
      return;
    }

    this.submit({
      broker: this.guardrail().service,
      fundingService: f.fundingService ?? FundingService.Other,
      kind: GuardrailKind.LossLimits,
      dailyLossLimitPct: this.toFraction(f.dailyLossPct),
      maxLossLimitPct: this.toFraction(f.maxLossPct),
      profitTargetPct: this.toFraction(f.profitTargetPct),
      drawdownModel: f.drawdownModel ?? DrawdownModel.Static,
      targetVarPct: undefined,
      varFloorPct: undefined,
      verified: f.verified ?? false,
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.cancelled.emit();
    }
  }

  private toFraction(v: number | null | undefined): number | undefined {
    return v === null || v === undefined || Number.isNaN(v) ? undefined : v / 100;
  }

  private submit(dto: UpsertBrokerRiskLimitsDto): void {
    this.saving.set(true);
    this.service.upsertRiskLimits(dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: () => {
        this.saving.set(false);
        this.errorMessage.set('No se pudieron guardar los límites');
      },
    });
  }
}
