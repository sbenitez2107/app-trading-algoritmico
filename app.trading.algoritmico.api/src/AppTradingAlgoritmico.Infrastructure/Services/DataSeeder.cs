using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DataSeeder> _logger;

    // Seed password meets all Identity requirements:
    // uppercase, lowercase, digit, special char, min length 8
    private const string DefaultPassword = "Trading@2024!";

    public DataSeeder(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DataSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedUsersAsync();
        await SeedAnalyzerRulesAsync(ct);
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = ["Admin", "Trader", "Viewer"];
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new ApplicationRole(role));
                if (result.Succeeded)
                    _logger.LogInformation("Role '{Role}' created.", role);
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        // Seed: sbenitez (primary user)
        await CreateUserIfNotExistsAsync(
            email: "sbenitez2107@gmail.com",
            userName: "sbenitez",
            firstName: "Sebastian",
            lastName: "Benitez",
            role: "Admin"
        );

        // Seed: admin (technical admin account)
        await CreateUserIfNotExistsAsync(
            email: "admin@appta.local",
            userName: "admin",
            firstName: "Admin",
            lastName: "AppTA",
            role: "Admin"
        );
    }

    private async Task CreateUserIfNotExistsAsync(
        string email, string userName, string firstName, string lastName, string role)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            _logger.LogInformation("User '{Email}' already exists. Skipping.", email);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, DefaultPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user '{Email}': {Errors}", email, errors);
            return;
        }

        await _userManager.AddToRoleAsync(user, role);
        _logger.LogInformation("User '{Email}' created with role '{Role}'.", email, role);
    }

    private async Task SeedAnalyzerRulesAsync(CancellationToken ct)
    {
        if (await _db.AnalyzerRules.AnyAsync(ct))
        {
            _logger.LogInformation("AnalyzerRules already seeded. Skipping.");
            return;
        }

        var rules = new List<AnalyzerRule>
        {
            new()
            {
                Name = "SPP Ret/DD Ratio Filter",
                Description = "Filter by SPP. If Ret/DD Ratio is too far from the mean, the strategy is over-optimized and should be discarded.",
                Priority = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "All Years Positive",
                Description = "All individual years must show positive results. Discard any strategy with a negative year.",
                Priority = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "Walk-Forward Matrix Stability",
                Description = "Analyze the WF matrix. Look for green zones or stable areas. Discard strategies with unstable matrices.",
                Priority = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "3D Surface Zone Validation",
                Description = "Within each Run/OSS of the matrix, find the stable zone and validate it in the 3D Surface chart. Stability matters more than the highest Ret DD/Ratio.",
                Priority = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "Group by Entry Parameters",
                Description = "Group strategies sharing the same price/Entry parameters. Keep only 2 per group.",
                Priority = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "KPI Thresholds",
                Description = "Profit Factor > 1.6, Sharpe Ratio > 1.3, Ret DD/Ratio > 12, Winning % > 48%, %DD < 2%, Stagnation < 365 days, Max Avg Losses < 10.",
                Priority = 6,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _db.AnalyzerRules.AddRange(rules);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("{Count} AnalyzerRules seeded.", rules.Count);
    }
}
