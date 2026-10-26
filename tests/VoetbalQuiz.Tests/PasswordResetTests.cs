using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;

namespace VoetbalQuiz.Tests;

public sealed class PasswordResetTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly UserManager<AppUser> _users;
    private readonly PasswordResetService _reset;

    public PasswordResetTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<QuizDbContext>(o => o.UseSqlite(_connection));
        // Dezelfde wachtwoordregels als in Program.cs.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 6;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<QuizDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<PasswordResetService>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _scope.ServiceProvider.GetRequiredService<QuizDbContext>().Database.EnsureCreated();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _reset = _scope.ServiceProvider.GetRequiredService<PasswordResetService>();
    }

    [Fact]
    public async Task Reset_sets_a_new_password_that_works()
    {
        var user = new AppUser { UserName = "LB7" };
        Assert.True((await _users.CreateAsync(user, "oud-wachtwoord")).Succeeded);

        var result = await _reset.ResetAsync(" LB7 ");

        Assert.NotNull(result);
        Assert.Equal("LB7", result.UserName);
        Assert.Equal(PasswordResetService.Length, result.TemporaryPassword.Length);
        Assert.True(await _users.CheckPasswordAsync(user, result.TemporaryPassword));
        Assert.False(await _users.CheckPasswordAsync(user, "oud-wachtwoord"));
    }

    [Fact]
    public async Task Reset_lifts_a_lockout()
    {
        var user = new AppUser { UserName = "LB7", LockoutEnabled = true };
        Assert.True((await _users.CreateAsync(user, "oud-wachtwoord")).Succeeded);
        await _users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));

        await _reset.ResetAsync("LB7");

        Assert.False(await _users.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task A_deleted_account_stays_closed()
    {
        var user = new AppUser { UserName = "verwijderd",DeletedAt = DateTime.UtcNow, LockoutEnabled = true, LockoutEnd = DateTimeOffset.MaxValue };
        Assert.True((await _users.CreateAsync(user)).Succeeded);

        Assert.Null(await _reset.ResetAsync(user.UserName));
        Assert.True(await _users.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task Unknown_user_gives_null() =>
        Assert.Null(await _reset.ResetAsync("bestaat-niet"));

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }
}
