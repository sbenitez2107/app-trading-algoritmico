using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

public class BatchStage : BaseEntity
{
    /// <summary>Pipeline stage (Builder, Retester, Optimizer, Demo, Live)</summary>
    public PipelineStageType StageType { get; set; }

    /// <summary>Current status of this stage</summary>
    public PipelineStageStatus Status { get; set; } = PipelineStageStatus.Pending;

    /// <summary>Number of strategies that entered this stage</summary>
    public int InputCount { get; set; }

    /// <summary>Number of strategies that passed this stage's filter</summary>
    public int OutputCount { get; set; }

    /// <summary>Stage ordering for pipeline sequencing (mirrors enum ordinal)</summary>
    public int Order { get; set; }

    /// <summary>Optional notes about this stage run</summary>
    public string? Notes { get; set; }

    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public ICollection<Strategy> Strategies { get; set; } = [];
}
