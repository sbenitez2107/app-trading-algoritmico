using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for BatchService.AdvanceAsync — verifies that the new dual-count signature
/// (passedCount + nextInputCount) lets the caller decouple how many strategies passed
/// the current stage from how many enter the next stage.
/// </summary>
public class BatchServiceAdvanceTests
{
    private static (Guid batchId, Guid builderStageId) SeedBuilderBatch(string dbName, int builderOutput)
    {
        var batchId = Guid.NewGuid();
        var builderStageId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var bbId = Guid.NewGuid();

        using var db = InMemoryDbContextFactory.Create(dbName);

        db.Assets.Add(new Asset { Id = assetId, Name = "EUR/USD", Symbol = "EURUSD", CreatedAt = DateTime.UtcNow });
        db.BuildingBlocks.Add(new BuildingBlock { Id = bbId, Name = "BB1", CreatedAt = DateTime.UtcNow });

        db.Batches.Add(new Batch
        {
            Id = batchId,
            Name = "Test",
            AssetId = assetId,
            BuildingBlockId = bbId,
            Timeframe = Timeframe.H1,
            CreatedAt = DateTime.UtcNow,
            Stages =
            {
                new BatchStage
                {
                    Id = builderStageId,
                    BatchId = batchId,
                    StageType = PipelineStageType.Builder,
                    Status = PipelineStageStatus.Pending,
                    Order = 0,
                    InputCount = 0,
                    OutputCount = builderOutput,
                    CreatedAt = DateTime.UtcNow
                }
            }
        });
        db.SaveChanges();

        return (batchId, builderStageId);
    }

    private static BatchService CreateSut(string dbName, out AppTradingAlgoritmico.Infrastructure.Persistence.AppDbContext db)
    {
        db = InMemoryDbContextFactory.Create(dbName);
        return new BatchService(db, Mock.Of<ISqxParserService>(), Mock.Of<IHtmlReportParserService>());
    }

    [Fact]
    public async Task AdvanceAsync_OnlyPassedCount_NextInputDefaultsToPassed()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (batchId, _) = SeedBuilderBatch(dbName, builderOutput: 100);
        var sut = CreateSut(dbName, out var db);

        // Act — caller provides only passedCount; nextInputCount must default to it
        await sut.AdvanceAsync(batchId, zipFile: null, passedCount: 80, nextInputCount: null);

        // Assert
        using var verify = InMemoryDbContextFactory.Create(dbName);
        var batch = await verify.Batches.Include(b => b.Stages).FirstAsync(b => b.Id == batchId);
        var builder = batch.Stages.Single(s => s.StageType == PipelineStageType.Builder);
        var retester = batch.Stages.Single(s => s.StageType == PipelineStageType.Retester);

        builder.OutputCount.Should().Be(80, "passedCount overrides Builder.OutputCount");
        builder.Status.Should().Be(PipelineStageStatus.Completed);
        retester.InputCount.Should().Be(80, "default — same as passedCount");
        retester.OutputCount.Should().Be(0, "freshly-created stage starts with outputCount=0; user edits later");
    }

    [Fact]
    public async Task AdvanceAsync_PassedAndNextInputDifferent_PersistsBothIndependently()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (batchId, _) = SeedBuilderBatch(dbName, builderOutput: 100);
        var sut = CreateSut(dbName, out var db);

        // Act — caller wants 95 to be marked as passed but only 80 to enter the next stage
        await sut.AdvanceAsync(batchId, zipFile: null, passedCount: 95, nextInputCount: 80);

        // Assert
        using var verify = InMemoryDbContextFactory.Create(dbName);
        var batch = await verify.Batches.Include(b => b.Stages).FirstAsync(b => b.Id == batchId);
        var builder = batch.Stages.Single(s => s.StageType == PipelineStageType.Builder);
        var retester = batch.Stages.Single(s => s.StageType == PipelineStageType.Retester);

        builder.OutputCount.Should().Be(95);
        retester.InputCount.Should().Be(80);
        retester.OutputCount.Should().Be(0);
    }

    [Fact]
    public async Task AdvanceAsync_NoCountsAndNoZip_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        var (batchId, _) = SeedBuilderBatch(dbName, builderOutput: 50);
        var sut = CreateSut(dbName, out _);

        var act = async () =>
            await sut.AdvanceAsync(batchId, zipFile: null, passedCount: null, nextInputCount: null);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
