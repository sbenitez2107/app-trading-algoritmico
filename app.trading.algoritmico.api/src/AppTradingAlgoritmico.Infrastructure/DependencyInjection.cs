using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppTradingAlgoritmico.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql
                    .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(15),
                        errorNumbersToAdd: null)));

        // Same scoped AppDbContext instance, exposed through the narrow backtest-import surface.
        // Used for reads and for obtaining the execution strategy — never for a retried write.
        services.AddScoped<IBacktestDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // A retried unit of work needs a context per ATTEMPT, not the request-scoped one: an
        // execution strategy re-invokes its delegate with the previous attempt's change tracker
        // still loaded. See IBacktestDbContextFactory.
        services.AddScoped<IBacktestDbContextFactory>(sp =>
            new BacktestDbContextFactory(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));

        // Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password policy
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                // Lockout
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddScoped<ITradingAccountService, TradingAccountService>();
        services.AddScoped<IUserPreferencesService, UserPreferencesService>();

        // Strategy Workflow
        services.AddScoped<ISqxParserService, SqxParserService>();
        services.AddScoped<IHtmlReportParserService, HtmlReportParserService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IBuildingBlockService, BuildingBlockService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IBatchStageService, BatchStageService>();
        services.AddScoped<IStrategyService, StrategyService>();

        // Trade Import
        services.AddScoped<IMtStatementParserService, MtStatementParserService>();
        services.AddScoped<ITradeImportService, TradeImportService>();

        // Analyzer
        services.AddScoped<IAnalyzerRuleService, AnalyzerRuleService>();

        // Grid Presets
        services.AddScoped<IGridPresetService, GridPresetService>();

        // Expenses
        services.AddScoped<IExpenseService, ExpenseService>();

        // Portfolios
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IRiskLimitsService, RiskLimitsService>();

        // SQX Backtest Import. Two parsers, never one: the trade list and the walk-forward export
        // use inverted decimal and date conventions, so a shared policy would corrupt one of them
        // (design.md D9).
        services.AddScoped<ISqxTradeListParser, SqxTradeListParserService>();
        services.AddScoped<IWalkForwardExportParser, WalkForwardExportParserService>();
        services.AddScoped<IBacktestImportService, BacktestImportService>();
        services.AddScoped<IWalkForwardImportService, WalkForwardImportService>();
        services.AddScoped<IBacktestReadService, BacktestReadService>();

        return services;
    }
}
