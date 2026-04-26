import { computeCellCounts } from './pipeline-stage-counts';
import { BatchStageSummaryDto } from '../../../../core/services/batch.service';

function makeStage(
  stageType: number,
  status: number,
  inputCount: number,
  outputCount: number,
): BatchStageSummaryDto {
  return {
    id: `stage-${stageType}`,
    stageType,
    status,
    inputCount,
    outputCount,
    runningStartedAt: null,
    updatedAt: null,
  };
}

describe('computeCellCounts', () => {
  it('builder_ShowsOutputCountAsPassed', () => {
    // Arrange — Builder completed with 600 strategies produced
    const builder = makeStage(0, 2, 600, 600);

    // Act
    const counts = computeCellCounts(builder);

    // Assert — Builder cell shows passed=outputCount, input=0
    expect(counts).toEqual({ input: 0, passed: 600 });
  });

  it('retesterCompleted_ShowsInputOverOutput', () => {
    // Arrange — Retester completed: 600 entered, 74 passed the filter
    const retester = makeStage(1, 2, 600, 74);

    // Act
    const counts = computeCellCounts(retester);

    // Assert — spec: "600 / 74"
    expect(counts).toEqual({ input: 600, passed: 74 });
  });

  it('optimizerPending_FreshlyCreated_ShowsZeroPassed', () => {
    // Arrange — Optimizer pending; AdvanceAsync now initializes outputCount=0
    // for new stages, so a freshly-created stage naturally reports "74 / 0".
    const optimizer = makeStage(2, 0, 74, 0);

    // Act
    const counts = computeCellCounts(optimizer);

    // Assert
    expect(counts).toEqual({ input: 74, passed: 0 });
  });

  it('optimizerPending_UserEditedPassed_RespectsOutputCount', () => {
    // Arrange — User manually edited outputCount=42 on a Pending stage.
    // The display must respect the persisted value, not mask it with 0.
    const optimizer = makeStage(2, 0, 74, 42);

    // Act
    const counts = computeCellCounts(optimizer);

    // Assert — user's edit is honored
    expect(counts).toEqual({ input: 74, passed: 42 });
  });

  it('optimizerRunning_UserEditedPassed_RespectsOutputCount', () => {
    // Arrange — Same rule for Running: outputCount is the source of truth.
    const optimizer = makeStage(2, 1, 74, 30);

    // Act
    const counts = computeCellCounts(optimizer);

    // Assert
    expect(counts).toEqual({ input: 74, passed: 30 });
  });

  it('nonBuilderStage_IgnoresPreviousStage_UsesOwnFields', () => {
    // Arrange — even if siblings carry weird values, this stage's own
    // inputCount/outputCount are the single source of truth.
    const retester = makeStage(1, 2, 600, 74);

    // Act
    const counts = computeCellCounts(retester);

    // Assert
    expect(counts).toEqual({ input: 600, passed: 74 });
  });
});
