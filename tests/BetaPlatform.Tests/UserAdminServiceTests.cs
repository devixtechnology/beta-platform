using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.ViewModels.Users;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Covers the seven cases in specs/004-phase1-feedback/contracts/user-management.md. Identity is
/// exercised for real against the in-memory store — the last-administrator guard and the
/// password-reset behaviour are only meaningful if UserManager actually applies them.
/// </summary>
public class UserAdminServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public UserAdminServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        // AddDefaultTokenProviders needs data protection — the password-reset token is protected.
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    private UserManager<ApplicationUser> Users =>
        _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    private UserAdminService NewService() => new(
        Users,
        _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());

    private static UserFormViewModel Form(
        string email = "person@beta.local",
        string role = DbSeeder.ClientRole,
        string? password = "Passw0rd!",
        bool isActive = true) =>
        new()
        {
            Email = email,
            FullName = "Test Person",
            Role = role,
            Password = password,
            IsActive = isActive
        };

    /// <summary>Creates an active administrator directly, so tests can set up the "last admin"
    /// situation without going through the code under test.</summary>
    private async Task<ApplicationUser> SeedAdminAsync(string email = "admin@beta.local")
    {
        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(DbSeeder.AdminRole))
            await roles.CreateAsync(new IdentityRole(DbSeeder.AdminRole));

        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Admin", IsActive = true };
        Assert.True((await Users.CreateAsync(admin, "Passw0rd!")).Succeeded);
        Assert.True((await Users.AddToRoleAsync(admin, DbSeeder.AdminRole)).Succeeded);
        return admin;
    }

    [Fact]
    public async Task Create_Succeeds_And_Assigns_The_Requested_Role()
    {
        var svc = NewService();

        var result = await svc.CreateAsync(Form(role: DbSeeder.ClientRole));

        Assert.True(result.Success);
        var created = await Users.FindByEmailAsync("person@beta.local");
        Assert.NotNull(created);
        Assert.True(await Users.IsInRoleAsync(created!, DbSeeder.ClientRole));
        Assert.True(created!.IsActive);
    }

    [Fact]
    public async Task Create_Refuses_A_Duplicate_Email_And_Creates_Nothing()
    {
        var svc = NewService();
        Assert.True((await svc.CreateAsync(Form())).Success);

        var result = await svc.CreateAsync(Form());

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
        Assert.Single(await Users.Users.ToListAsync());
    }

    [Fact]
    public async Task Last_Active_Administrator_Cannot_Be_Deactivated()
    {
        var admin = await SeedAdminAsync();
        var svc = NewService();

        var result = await svc.SetActiveAsync(admin.Id, isActive: false);

        Assert.False(result.Success);
        Assert.Contains("last active administrator", result.Error);
        Assert.True((await Users.FindByIdAsync(admin.Id))!.IsActive);
    }

    [Fact]
    public async Task Last_Active_Administrator_Cannot_Be_Demoted_To_Client()
    {
        var admin = await SeedAdminAsync();
        var svc = NewService();

        var result = await svc.UpdateAsync(new UserFormViewModel
        {
            Id = admin.Id,
            Email = admin.Email!,
            FullName = "Admin",
            Role = DbSeeder.ClientRole,
            IsActive = true
        });

        Assert.False(result.Success);
        Assert.True(await Users.IsInRoleAsync((await Users.FindByIdAsync(admin.Id))!, DbSeeder.AdminRole));
    }

    [Fact]
    public async Task A_Second_Administrator_Makes_The_First_Deactivatable()
    {
        var first = await SeedAdminAsync();
        await SeedAdminAsync("second@beta.local");
        var svc = NewService();

        var result = await svc.SetActiveAsync(first.Id, isActive: false);

        Assert.True(result.Success);
        Assert.False((await Users.FindByIdAsync(first.Id))!.IsActive);
    }

    [Fact]
    public async Task Deactivating_A_Client_Sets_IsActive_False_And_Rotates_The_Security_Stamp()
    {
        var svc = NewService();
        var client = (await svc.CreateAsync(Form())).Value!;
        var stampBefore = (await Users.FindByIdAsync(client.Id))!.SecurityStamp;

        var result = await svc.SetActiveAsync(client.Id, isActive: false);

        Assert.True(result.Success);
        var reloaded = (await Users.FindByIdAsync(client.Id))!;
        Assert.False(reloaded.IsActive);
        // The rotated stamp is what invalidates a cookie already issued to this account (research T4).
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp);
    }

    [Fact]
    public async Task Reactivation_Restores_Eligibility()
    {
        var svc = NewService();
        var client = (await svc.CreateAsync(Form())).Value!;
        await svc.SetActiveAsync(client.Id, isActive: false);

        var result = await svc.SetActiveAsync(client.Id, isActive: true);

        Assert.True(result.Success);
        Assert.True((await Users.FindByIdAsync(client.Id))!.IsActive);
    }

    [Fact]
    public async Task Password_Reset_Invalidates_The_Old_Password()
    {
        var svc = NewService();
        var client = (await svc.CreateAsync(Form(password: "Passw0rd!"))).Value!;

        var result = await svc.ResetPasswordAsync(client.Id, "Newpass1!");

        Assert.True(result.Success);
        var reloaded = (await Users.FindByIdAsync(client.Id))!;
        Assert.False(await Users.CheckPasswordAsync(reloaded, "Passw0rd!"));
        Assert.True(await Users.CheckPasswordAsync(reloaded, "Newpass1!"));
    }

    [Fact]
    public async Task Update_Changes_Role_And_Name()
    {
        var svc = NewService();
        var client = (await svc.CreateAsync(Form())).Value!;

        var result = await svc.UpdateAsync(new UserFormViewModel
        {
            Id = client.Id,
            Email = client.Email!,
            FullName = "Renamed Person",
            Role = DbSeeder.AdminRole,
            IsActive = true
        });

        Assert.True(result.Success);
        var reloaded = (await Users.FindByIdAsync(client.Id))!;
        Assert.Equal("Renamed Person", reloaded.FullName);
        Assert.True(await Users.IsInRoleAsync(reloaded, DbSeeder.AdminRole));
        Assert.False(await Users.IsInRoleAsync(reloaded, DbSeeder.ClientRole));
    }

    [Fact]
    public async Task Create_Refuses_An_Unrecognised_Role()
    {
        var svc = NewService();

        var result = await svc.CreateAsync(Form(role: "Superuser"));

        Assert.False(result.Success);
        Assert.Empty(await Users.Users.ToListAsync());
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
