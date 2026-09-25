using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pins the EF contract for <see cref="FtmoInstrumentSpec"/> (design.md D4). Mirrors
/// <c>BacktestSchemaTests</c>'s idiom: SQLite in-memory because EF InMemory does not enforce
/// unique indexes.
/// </summary>
public class FtmoInstrumentSpecSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<FtmoInstrumentSpecTestDbContext> _options;

    public FtmoInstrumentSpecSchemaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<FtmoInstrumentSpecTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new FtmoInstrumentSpecTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static FtmoInstrumentSpec NewSpec(string sqxSymbol) => new()
    {
        Id = Guid.NewGuid(),
        SqxSymbol = sqxSymbol,
        FtmoSymbol = "XAUUSD",
        ContractSize = 100m,
        ProfitCurrency = "USD",
        SizeDecimals = 2,
        Step = 0.01m,
        MinLot = 0.01m,
        MaxLots = 1000m,
        SourceTimeZoneId = "Asia/Jerusalem",
        Provenance = "user-supplied from the FTMO MT platform 2026-09-24",
        CapturedOn = new DateOnly(2026, 9, 24),
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task SqxSymbol_IsUnique()
    {
        using var db = new FtmoInstrumentSpecTestDbContext(_options);
        db.FtmoInstrumentSpecs.Add(NewSpec("XAUUSD_M1_UTC02"));
        await db.SaveChangesAsync();

        db.FtmoInstrumentSpecs.Add(NewSpec("XAUUSD_M1_UTC02"));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void SqxSymbol_HasUniqueIndex()
    {
        using var db = new FtmoInstrumentSpecTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(FtmoInstrumentSpec))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["SqxSymbol"]));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Provenance_IsRequiredNonNullableString()
    {
        using var db = new FtmoInstrumentSpecTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(FtmoInstrumentSpec))!;

        var provenance = entity.FindProperty(nameof(FtmoInstrumentSpec.Provenance))!;
        provenance.ClrType.Should().Be(typeof(string));
        provenance.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void SourceTimeZoneId_IsNonNullableString()
    {
        using var db = new FtmoInstrumentSpecTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(FtmoInstrumentSpec))!;

        var timeZoneId = entity.FindProperty(nameof(FtmoInstrumentSpec.SourceTimeZoneId))!;
        timeZoneId.ClrType.Should().Be(typeof(string));
        timeZoneId.IsNullable.Should().BeFalse();
    }
}

/// <summary>Minimal DbContext for <see cref="FtmoInstrumentSpec"/> EF configuration tests.</summary>
public class FtmoInstrumentSpecTestDbContext(DbContextOptions<FtmoInstrumentSpecTestDbContext> options)
    : DbContext(options)
{
    public DbSet<FtmoInstrumentSpec> FtmoInstrumentSpecs => Set<FtmoInstrumentSpec>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FtmoInstrumentSpecConfiguration());
    }
}
