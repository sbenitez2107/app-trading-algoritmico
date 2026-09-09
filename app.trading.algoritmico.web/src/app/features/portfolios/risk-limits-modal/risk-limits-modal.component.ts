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
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import {
  PortfolioService,
  FundingService,
  DrawdownModel,
  FtmoProduct,
  GuardrailKind,
  ServiceGuardrailDto,
  UpsertBrokerRiskLimitsDto,
  UpsertFundingStageLimitDto,
} from '../../../core/services/portfolio.service';

/**
 * Extracted risk-limits editor modal (per `advance-stage-modal` convention — typed reactive form,
 * OnPush, input()/output()). The visible field set switches by the selected funding service:
 * Darwinex Zero shows the VarTarget band (target/floor VaR), Axi shows a stage-rows form array,
 * every other service shows the LossLimits breach-style fields (FTMO additionally shows the
 * optional product select) — mirroring `RiskLimitsService.UpsertAsync`'s kind-aware validation
 * (`funding-guardrails` spec). Copy stays hardcoded Spanish: the `features/portfolios` tree has
 * zero ngx-translate usage (design decision — pre-existing debt, not addressed here).
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
  readonly FtmoProduct = FtmoProduct;

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /** Tracks the SELECTED funding service (not necessarily the guardrail's original one) so the
   * field set toggles live when the user switches broker in the dropdown. */
  private readonly selectedFundingService = signal<FundingService>(FundingService.Other);

  /** Darwinex Zero -> VarTarget field set. */
  readonly isVarTarget = computed(
    () => this.selectedFundingService() === FundingService.DarwinexZero,
  );

  /** Axi -> StagedLossLimits field set (stage rows, no parent-row scalars). */
  readonly isStagedLossLimits = computed(
    () => this.selectedFundingService() === FundingService.Axi,
  );

  /** FTMO -> LossLimits field set plus the optional product select. */
  readonly isFtmo = computed(() => this.selectedFundingService() === FundingService.Ftmo);

  readonly form = this.fb.group({
    fundingService: [FundingService.Other],
    dailyLossPct: [null as number | null],
    maxLossPct: [null as number | null],
    profitTargetPct: [null as number | null],
    drawdownModel: [DrawdownModel.Static],
    ftmoProduct: [null as FtmoProduct | null],
    targetVarPct: [null as number | null],
    varFloorPct: [null as number | null],
    verified: [false],
  });

  readonly stageRows = this.fb.array([this.buildStageRow()]);

  private buildStageRow(ordinal = 1) {
    return this.fb.group({
      stageOrdinal: [ordinal, Validators.required],
      stageName: [`Stage ${ordinal}`, Validators.required],
      maxLossLimitPct: [null as number | null],
      profitTargetPct: [null as number | null],
    });
  }

  addStageRow(): void {
    this.stageRows.push(this.buildStageRow(this.stageRows.length + 1));
  }

  removeStageRow(index: number): void {
    if (this.stageRows.length > 1) this.stageRows.removeAt(index);
  }

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
          ftmoProduct: null,
          targetVarPct: g.varTarget.targetVarPct != null ? g.varTarget.targetVarPct * 100 : null,
          varFloorPct: g.varTarget.varFloorPct != null ? g.varTarget.varFloorPct * 100 : null,
          verified: g.verified,
        });
      } else if (g.kind === GuardrailKind.StagedLossLimits) {
        this.form.reset({
          fundingService: g.fundingService,
          dailyLossPct: null,
          maxLossPct: null,
          profitTargetPct: null,
          drawdownModel: DrawdownModel.Static,
          ftmoProduct: null,
          targetVarPct: null,
          varFloorPct: null,
          verified: g.verified,
        });
      } else {
        this.form.reset({
          fundingService: g.fundingService,
          dailyLossPct: g.dailyLossLimitPct != null ? g.dailyLossLimitPct * 100 : null,
          maxLossPct: g.maxLossLimitPct != null ? g.maxLossLimitPct * 100 : null,
          profitTargetPct: g.profitTargetPct != null ? g.profitTargetPct * 100 : null,
          drawdownModel: g.drawdownModel ?? DrawdownModel.Static,
          ftmoProduct: null,
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

    if (this.isStagedLossLimits()) {
      const stages: UpsertFundingStageLimitDto[] = this.stageRows.controls.map((row) => {
        const v = row.value;
        return {
          stageOrdinal: v.stageOrdinal ?? 0,
          stageName: v.stageName ?? '',
          maxLossLimitPct: this.toFraction(v.maxLossLimitPct) ?? 0,
          profitTargetPct: this.toFraction(v.profitTargetPct),
        };
      });

      if (stages.length === 0 || stages.some((s) => !s.maxLossLimitPct)) {
        this.errorMessage.set('Cada stage necesita un límite de pérdida máximo.');
        return;
      }

      this.submit({
        broker: this.guardrail().service,
        fundingService: f.fundingService ?? FundingService.Axi,
        kind: GuardrailKind.StagedLossLimits,
        dailyLossLimitPct: undefined,
        maxLossLimitPct: undefined,
        profitTargetPct: undefined,
        // Must be null — the API rejects a DrawdownModel on a StagedLossLimits payload.
        drawdownModel: null,
        targetVarPct: undefined,
        varFloorPct: undefined,
        verified: f.verified ?? false,
        stages,
      });
      return;
    }

    const ftmoProduct = this.isFtmo() ? (f.ftmoProduct ?? undefined) : undefined;
    const drawdownModel = f.drawdownModel ?? DrawdownModel.Static;

    if (ftmoProduct === FtmoProduct.OneStep && drawdownModel !== DrawdownModel.Trailing) {
      this.errorMessage.set('One Step de FTMO requiere modelo de drawdown Trailing.');
      return;
    }
    if (ftmoProduct === FtmoProduct.TwoStep && drawdownModel !== DrawdownModel.Static) {
      this.errorMessage.set('Two Step de FTMO requiere modelo de drawdown Static.');
      return;
    }

    this.submit({
      broker: this.guardrail().service,
      fundingService: f.fundingService ?? FundingService.Other,
      kind: GuardrailKind.LossLimits,
      dailyLossLimitPct: this.toFraction(f.dailyLossPct),
      maxLossLimitPct: this.toFraction(f.maxLossPct),
      profitTargetPct: this.toFraction(f.profitTargetPct),
      drawdownModel,
      ftmoProduct,
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
