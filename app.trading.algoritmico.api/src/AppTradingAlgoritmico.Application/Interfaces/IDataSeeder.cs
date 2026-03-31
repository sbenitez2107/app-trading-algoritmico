namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
