import { BatchStageSummaryDto } from '../../../../core/services/batch.service';

/**
 * Display counts for a single pipeline cell.
 * - Builder (stageType 0): no input; "passed" is the strategies it produced.
 * - Other stages: input = strategies entered the stage; passed = strategies
 *   that survived the filter. Always reflects the persisted outputCount —
 *   the user owns this value and edits it manually as the stage progresses.
 *   New stages are initialized with outputCount=0 by AdvanceAsync, so
 *   Pending/Running stages naturally render as `input / 0` until edited.
 */
export function computeCellCounts(stage: BatchStageSummaryDto): { input: number; passed: number } {
  if (stage.stageType === 0) {
    return { input: 0, passed: stage.outputCount };
  }
  return { input: stage.inputCount, passed: stage.outputCount };
}
