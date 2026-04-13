using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

public class BatchStageStatusTests
{
    [Fact]
    public void PipelineStageStatus_HasThreeValues()
    {
        var values = Enum.GetValues<PipelineStageStatus>();
        values.Should().HaveCount(3);
    }

    [Fact]
    public void PipelineStageStatus_ValuesAreCorrect()
    {
        ((int)PipelineStageStatus.Pending).Should().Be(0);
        ((int)PipelineStageStatus.Running).Should().Be(1);
        ((int)PipelineStageStatus.Completed).Should().Be(2);
    }

    [Fact]
    public void PipelineStageStatus_DoesNotContainInProgress()
    {
        var names = Enum.GetNames<PipelineStageStatus>();
        names.Should().NotContain("InProgress");
    }

    [Fact]
    public void PipelineStageType_HasFiveValues()
    {
        var values = Enum.GetValues<PipelineStageType>();
        values.Should().HaveCount(5);
        values.Should().Contain(PipelineStageType.Builder);
        values.Should().Contain(PipelineStageType.Retester);
        values.Should().Contain(PipelineStageType.Optimizer);
        values.Should().Contain(PipelineStageType.Demo);
        values.Should().Contain(PipelineStageType.Live);
    }

    [Fact]
    public void BatchStage_DefaultStatusIsPending()
    {
        var stage = new Domain.Entities.BatchStage();
        stage.Status.Should().Be(PipelineStageStatus.Pending);
        stage.RunningStartedAt.Should().BeNull();
    }
}
