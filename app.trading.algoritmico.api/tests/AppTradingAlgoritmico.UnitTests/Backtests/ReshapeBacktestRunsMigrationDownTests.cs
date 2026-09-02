using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// The rollback of <c>ReshapeBacktestRunsForStrategyScopedImport</c> must EXECUTE.
/// <para>
/// Its <c>Down</c> re-imposes two uniqueness constraints that the forward migration removed on
/// purpose. It re-adds <c>StrategyNameKey</c>/<c>RunLabel</c> with <c>defaultValue: ""</c> and then
/// creates a UNIQUE index over exactly that pair — so with two or more rows every row is
/// <c>("", "")</c> and they collide. Two rows is the ordinary steady state, not an edge case: one
/// Deploy slot plus one Evaluation slot. It also recreates a UNIQUE <c>ContentHash</c> index, which
/// the forward design dropped precisely because identical bytes legitimately back runs under two
/// different strategies.
/// </para>
/// <para>
/// A rollback that drops a table is inherently lossy and that is acceptable. Throwing is not: a
/// migration whose <c>Down</c> cannot run is not a rollback at all. So the invariant asserted here
/// is that this <c>Down</c> discards the rows it cannot make unique BEFORE it demands uniqueness of
/// them — and, by doing so, states the loss instead of discovering it in production.
/// </para>
/// <para>
/// The operation list is built by invoking the real <c>Down</c>, so this reads the shipped
/// behaviour rather than the source text.
/// </para>
/// </summary>
public class ReshapeBacktestRunsMigrationDownTests
{
    private static IReadOnlyList<MigrationOperation> DownOperations()
    {
        var migrationType = typeof(AppTradingAlgoritmico.Infrastructure.Persistence.AppDbContext).Assembly
            .GetTypes()
            .Single(t => t.Name == "ReshapeBacktestRunsForStrategyScopedImport");

        var migration = Activator.CreateInstance(migrationType)!;
        var builder = new MigrationBuilder(activeProvider: "Microsoft.EntityFrameworkCore.SqlServer");

        migrationType
            .GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static bool ClearsRowsOf(MigrationOperation operation, string table)
        => operation switch
        {
            SqlOperation sql => sql.Sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                && sql.Sql.Contains(table, StringComparison.OrdinalIgnoreCase),
            DeleteDataOperation delete => string.Equals(delete.Table, table, StringComparison.OrdinalIgnoreCase),
            DropTableOperation drop => string.Equals(drop.Name, table, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    [Fact]
    public void Down_DoesNotDemandUniquenessOfRowsItLeavesInPlace()
    {
        var operations = DownOperations();

        // A table this Down CREATES starts empty, so a unique index over it can never collide.
        var tablesCreatedHere = operations
            .OfType<CreateTableOperation>()
            .Select(o => o.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uniqueIndexesOnSurvivingTables = operations
            .OfType<CreateIndexOperation>()
            .Where(o => o.IsUnique && !tablesCreatedHere.Contains(o.Table))
            .ToList();

        uniqueIndexesOnSurvivingTables.Should().NotBeEmpty(
            "this Down is expected to restore the previous shape's unique indexes — if it no longer does, this test is guarding nothing");

        foreach (var index in uniqueIndexesOnSurvivingTables)
        {
            var position = operations.ToList().IndexOf(index);

            operations.Take(position).Should().Contain(
                op => ClearsRowsOf(op, index.Table),
                $"'{index.Name}' is UNIQUE over ({string.Join(", ", index.Columns)}) on the surviving table "
                + $"'{index.Table}', whose existing rows cannot satisfy it — the forward migration removed that "
                + "constraint deliberately, and the re-added columns carry a constant default, so every row would "
                + "be identical. The rows must be discarded before the constraint is re-imposed or CREATE INDEX throws");
        }
    }

    [Fact]
    public void Down_DiscardsTheImportedRunsRatherThanPretendingItCanRebuildThem()
    {
        // The rev-1 identity was (StrategyNameKey, RunLabel), both parsed out of a file-name
        // convention that no longer exists anywhere in the system. There is nothing to derive them
        // FROM, so a faithful rollback is impossible and the honest one discards the data.
        var operations = DownOperations();

        operations.Should().Contain(op => ClearsRowsOf(op, "BacktestRuns"));
        operations.Should().Contain(op => ClearsRowsOf(op, "BacktestTrades"));
    }
}
