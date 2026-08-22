import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RiskLimitsModalComponent } from './risk-limits-modal.component';
import {
  PortfolioService,
  FundingService,
  DrawdownModel,
  GuardrailKind,
  ServiceGuardrailDto,
} from '../../../core/services/portfolio.service';

/**
 * Extracted risk-limits modal (per advance-stage-modal convention): typed reactive form,
 * field set switches by funding service (Darwinex Zero -> VarTarget, everything else ->
 * LossLimits), client-side validators mirror RiskLimitsServiceTests (backend).
 */
describe('RiskLimitsModalComponent', () => {
  let serviceMock: Partial<PortfolioService>;

  const lossLimitsGuardrail: ServiceGuardrailDto = {
    service: 'FTMO',
    fundingService: FundingService.Ftmo,
    kind: GuardrailKind.LossLimits,
    configured: true,
    verified: true,
    dailyLossLimitPct: 0.05,
    maxLossLimitPct: 0.1,
    profitTargetPct: 0.1,
    drawdownModel: DrawdownModel.Static,
    serviceVar95Percent: 0.02,
    dailyHeadroomPct: 0.03,
    dailyBreached: false,
    varTarget: null,
  };

  const varTargetGuardrail: ServiceGuardrailDto = {
    service: 'Darwinex',
    fundingService: FundingService.DarwinexZero,
    kind: GuardrailKind.VarTarget,
    configured: true,
    verified: true,
    dailyLossLimitPct: null,
    maxLossLimitPct: null,
    profitTargetPct: null,
    drawdownModel: null,
    serviceVar95Percent: 0.02,
    dailyHeadroomPct: null,
    dailyBreached: false,
    varTarget: {
      targetVarPct: 0.065,
      varFloorPct: 0.0325,
      horizonDays: 30,
      insufficientHistory: false,
      observationDays: 120,
      overlappingWindows: 91,
      independentWindows: 4,
      monthlyVar95: 300,
      monthlyVar95Percent: 0.003,
      impliedMultiplier: 21.67,
    },
  };

  beforeEach(() => {
    serviceMock = {
      upsertRiskLimits: vi.fn().mockReturnValue(of({}) as never),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [RiskLimitsModalComponent],
      providers: [{ provide: PortfolioService, useValue: serviceMock }],
    });
  });

  function create(guardrail: ServiceGuardrailDto) {
    const fixture = TestBed.createComponent(RiskLimitsModalComponent);
    fixture.componentRef.setInput('guardrail', guardrail);
    fixture.detectChanges();
    return fixture;
  }

  it('create_NonDarwinexBroker_ShowsLossLimitsFieldSet', () => {
    const fixture = create(lossLimitsGuardrail);
    expect(fixture.componentInstance.isVarTarget()).toBe(false);
  });

  it('create_DarwinexZeroBroker_ShowsVarTargetFieldSet', () => {
    const fixture = create(varTargetGuardrail);
    expect(fixture.componentInstance.isVarTarget()).toBe(true);
  });

  it('fundingServiceChange_TogglesFieldSet', () => {
    const fixture = create(lossLimitsGuardrail);
    const comp = fixture.componentInstance;
    expect(comp.isVarTarget()).toBe(false);

    comp.form.controls.fundingService.setValue(FundingService.DarwinexZero);

    expect(comp.isVarTarget()).toBe(true);
  });

  it('save_VarTargetFloorAboveTarget_ShowsErrorWithoutCallingService', () => {
    const fixture = create(varTargetGuardrail);
    const comp = fixture.componentInstance;
    comp.form.controls.targetVarPct.setValue(6.5);
    comp.form.controls.varFloorPct.setValue(10); // floor > target — invalid

    comp.save();

    expect(comp.errorMessage()).toBeTruthy();
    expect(serviceMock.upsertRiskLimits).not.toHaveBeenCalled();
  });

  it('save_VarTargetPercentOutsideRange_ShowsErrorWithoutCallingService', () => {
    const fixture = create(varTargetGuardrail);
    const comp = fixture.componentInstance;
    comp.form.controls.targetVarPct.setValue(0);
    comp.form.controls.varFloorPct.setValue(0);

    comp.save();

    expect(comp.errorMessage()).toBeTruthy();
    expect(serviceMock.upsertRiskLimits).not.toHaveBeenCalled();
  });

  it('save_ValidVarTargetPayload_CallsUpsertWithFractionValuesAndVarTargetKind', () => {
    const fixture = create(varTargetGuardrail);
    const comp = fixture.componentInstance;
    comp.form.controls.targetVarPct.setValue(6.5);
    comp.form.controls.varFloorPct.setValue(3.25);

    comp.save();

    expect(serviceMock.upsertRiskLimits).toHaveBeenCalledWith(
      expect.objectContaining({
        broker: 'Darwinex',
        kind: GuardrailKind.VarTarget,
        targetVarPct: 0.065,
        varFloorPct: 0.0325,
        dailyLossLimitPct: undefined,
        maxLossLimitPct: undefined,
        profitTargetPct: undefined,
      }),
    );
  });

  it('save_ValidLossLimitsPayload_CallsUpsertWithFractionValuesAndLossLimitsKind', () => {
    const fixture = create(lossLimitsGuardrail);
    const comp = fixture.componentInstance;
    comp.form.controls.dailyLossPct.setValue(5);
    comp.form.controls.maxLossPct.setValue(10);

    comp.save();

    expect(serviceMock.upsertRiskLimits).toHaveBeenCalledWith(
      expect.objectContaining({
        broker: 'FTMO',
        kind: GuardrailKind.LossLimits,
        dailyLossLimitPct: 0.05,
        maxLossLimitPct: 0.1,
        targetVarPct: undefined,
        varFloorPct: undefined,
      }),
    );
  });
});
