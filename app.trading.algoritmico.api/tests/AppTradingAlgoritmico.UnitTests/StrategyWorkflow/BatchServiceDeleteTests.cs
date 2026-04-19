using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for BatchService.DeleteAsync and RollbackStageAsync with dual-linked strategies.
/// Spec: strategy-model M3.
/// Uses EF InMemory + Moq for ISqxParserService and IHtmlReportParserService.
/// Note: SetNull FK behavior is NOT enforced by EF InMemory — these tests verify the
/// explicit RemoveRange filter logic (pipeline-only removed, dual-linked preserved).
/// </summary>
public class BatchServiceDeleteTests
{
    private static (Guid batchId, Guid stageId, Guid pipelineStrategyId, Guid dualLinkedStrategyId)
        SeedBatchWithStrategies(string dbName)
    {
        var batchId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var bbId = Guid.NewGuid();
        var pipelineStrategyId = Guid.NewGuid();
        var dualLinkedStrategyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        using var db = InMemoryDbContextFactory.Create(dbName);

        db.Assets.Add(new Asset { Id = assetId, Name = "EUR/USD", Symbol = "EURUSD", CreatedAt = DateTime.UtcNow });
        db.BuildingBlocks.Add(new BuildingBlock { Id = bbId, Name = "BB1", CreatedAt = DateTime.UtcNow });
        db.TradingAccounts.Add(new TradingAccount
        {
            Id = accountId,
            Name = "Demo",
            Broker = "Darwinex",
            AccountNumber = 1,
            Login = 1,
            PasswordEncrypted = "e",
            Server = "s",
            CreatedAt = DateTime.UtcNow
        });

        var batch = new Batch
        {
            Id = batchId,
            Name = "Test",
            AssetId = assetId,
            BuildingBlockId = bbId,
            Timeframe = Timeframe.H1,
            CreatedAt = DateTime.UtcNow
        };

        var stage = new BatchStage
        {
            Id = stageId,
            BatchId = batchId,
            StageType = PipelineStageType.Builder,
            Status = PipelineStageStatus.Pending,
            Order = 0,
            InputCount = 2,
            OutputCount = 2,
            CreatedAt = DateTime.UtcNow
        };

        // Pipeline-only strategy (TradingAccountId = null)
        var pipelineStrategy = new Strategy
        {
            Id = pipelineStrategyId,
            Name = "Pipeline Only",
            BatchStageId = stageId,
            TradingAccountId = null,
            CreatedAt = DateTime.UtcNow
        };

        // Dual-linked strategy (has both BatchStageId and TradingAccountId)
        var dualLinkedStrategy = new Strategy
        {
            Id = dualLinkedStrategyId,
            Name = "Dual Linked",
            BatchStageId = stageId,
            TradingAccountId = accountId,
            CreatedAt = DateTime.UtcNow
        };

        stage.Strategies.Add(pipelineStrategy);
        stage.Strategies.Add(dualLinkedStrategy);
        batch.Stages.Add(stage);

        db.Batches.Add(batch);
        db.SaveChanges();

        return (batchId, stageId, pipelineStrategyId, dualLinkedStrategyId);
    }

    [Fact]
    public async Task DeleteAsync_StageDeletion_PipelineOnlyRemovedDualLinkedPreserved()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (batchId, _, pipelineStrategyId, dualLinkedStrategyId) = SeedBatchWithStrategies(dbName);

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new BatchService(db, sqxMock.Object, htmlMock.Object);

        // Act
        await sut.DeleteAsync(batchId);

        // Assert
        using var verify = InMemoryDbContextFactory.Create(dbName);

        // Pipeline-only strategy must be deleted
        var pipeline = await verify.Strategies.FindAsync(pipelineStrategyId);
        pipeline.Should().BeNull("pipeline-only strategy must be explicitly removed during batch deletion");

        // Dual-linked strategy must be preserved (EF InMemory won't apply SetNull, but service filter preserves it)
        var dualLinked = await verify.Strategies.FindAsync(dualLinkedStrategyId);
        dualLinked.Should().NotBeNull("dual-linked strategy must survive batch deletion");
    }

    [Fact]
    public async Task RollbackStageAsync_StageDeletion_PipelineOnlyRemovedDualLinkedPreserved()
    {
        // Arrange — RollbackStageAsync cannot delete Builder stage, so we seed a Retester stage on top
        var dbName = Guid.NewGuid().ToString();
        var (batchId, _, _, _) = SeedBatchWithStrategies(dbName);

        var accountId2 = Guid.NewGuid();
        var retesterStageId = Guid.NewGuid();
        var pipelineRetesterId = Guid.NewGuid();
        var dualLinkedRetesterId = Guid.NewGuid();

        using (var setup = InMemoryDbContextFactory.Create(dbName))
        {
            setup.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId2,
                Name = "Demo2",
                Broker = "Darwinex",
                AccountNumber = 2,
                Login = 2,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });

            var retesterStage = new BatchStage
            {
                Id = retesterStageId,
                BatchId = batchId,
                StageType = PipelineStageType.Retester,
                Status = PipelineStageStatus.Pending,
                Order = (int)PipelineStageType.Retester,
                InputCount = 2,
                OutputCount = 2,
                CreatedAt = DateTime.UtcNow
            };
            retesterStage.Strategies.Add(new Strategy
            {
                Id = pipelineRetesterId,
                Name = "Retester Pipeline Only",
                BatchStageId = retesterStageId,
                TradingAccountId = null,
                CreatedAt = DateTime.UtcNow
            });
            retesterStage.Strategies.Add(new Strategy
            {
                Id = dualLinkedRetesterId,
                Name = "Retester Dual Linked",
                BatchStageId = retesterStageId,
                TradingAccountId = accountId2,
                CreatedAt = DateTime.UtcNow
            });
            setup.BatchStages.Add(retesterStage);
            await setup.SaveChangesAsync();
        }

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new BatchService(db, sqxMock.Object, htmlMock.Object);

        // Act — rollback the Retester stage
        await sut.RollbackStageAsync(batchId, retesterStageId);

        // Assert
        using var verify = InMemoryDbContextFactory.Create(dbName);

        // Pipeline-only strategy must be deleted
        var pipeline = await verify.Strategies.FindAsync(pipelineRetesterId);
        pipeline.Should().BeNull("pipeline-only strategy must be explicitly removed during stage rollback");

        // Dual-linked strategy must be preserved
        var dualLinked = await verify.Strategies.FindAsync(dualLinkedRetesterId);
        dualLinked.Should().NotBeNull("dual-linked strategy must survive stage rollback");
    }
}
