using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Data;

/// <summary>
/// Applies pending migrations and seeds the roles plus the first administrator. Since 004 there are
/// two roles — <see cref="AdminRole"/> and <see cref="ClientRole"/> — and no default administrator
/// password in Production (contracts/user-management.md).
/// </summary>
public static class DbSeeder
{
    public const string AdminRole = "Admin";
    public const string ClientRole = "Client";

    /// <summary>Development-only fallback so <c>dotnet run</c> works on a fresh clone. It is never
    /// used outside Development — see <see cref="SeedAsync"/>.</summary>
    private const string DevelopmentFallbackPassword = "Admin@123";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Ensure the read-only reporting views exist (created if missing) — not managed by EF migrations.
        await DbViewSeeder.EnsureViewsAsync(db);

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AdminRole, ClientRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["AdminSeed:Email"] ?? "admin@beta.local";
        var fullName = config["AdminSeed:FullName"] ?? "Beta Administrator";

        // An existing administrator's password is never overwritten by seeding — it is changed
        // through /Account/ChangePassword or /Users/ResetPassword (FR-006).
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var password = config["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            // Outside Development, fail loudly rather than seeding a weak, publicly-known credential.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "No administrator account exists and 'AdminSeed:Password' is not configured. " +
                    "Set AdminSeed:Password (configuration, environment variable or user secret) " +
                    "before starting the application in this environment.");
            }
            password = DevelopmentFallbackPassword;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };
        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AdminRole);
        }
        else
        {
            throw new InvalidOperationException(
                "Failed to create the seed administrator account: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
