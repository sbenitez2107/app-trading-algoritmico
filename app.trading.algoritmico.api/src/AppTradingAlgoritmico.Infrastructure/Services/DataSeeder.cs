using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class DataSeeder : IDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DataSeeder> _logger;

    // Seed password meets all Identity requirements:
    // uppercase, lowercase, digit, special char, min length 8
    private const string DefaultPassword = "Trading@2024!";

    public DataSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DataSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedUsersAsync();
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
}
