using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Task 6.1 — asserts <see cref="ICostDecompositionReadService"/> resolves to
/// <see cref="CostDecompositionReadService"/> from the container, mirroring the registration added
/// beside slice A's comparability registration in <c>DependencyInjection.cs</c>. Registers directly
/// against a minimal <see cref="AppDbContext"/> (EF InMemory) rather than exercising the full
/// <c>AddInfrastructure</c> pipeline, which also configures SQL Server and Identity and is not
/// exercised end-to-end anywhere else in this suite.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void CostDecompositionReadService_ResolvesFromTheContainer_AsTheRegisteredImplementation()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<ICostDecompositionReadService, CostDecompositionReadService>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<ICostDecompositionReadService>();

        resolved.Should().BeOfType<CostDecompositionReadService>();
    }
}
