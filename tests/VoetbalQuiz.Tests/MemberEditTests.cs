using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;

namespace VoetbalQuiz.Tests;

public sealed class MemberEditTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly UserManager<AppUser> _users;
    private readonly MemberEditService _edit;

    public MemberEditTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<QuizDbContext>(o => o.UseSqlite(_connection));
        // Dezelfde regels als in Program.cs.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 6;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<QuizDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<ResultsService>();
        services.AddScoped<MemberEditService>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _scope.ServiceProvider.GetRequiredService<QuizDbContext>().Database.EnsureCreated();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _edit = _scope.ServiceProvider.GetRequiredService<MemberEditService>();
    }

    private async Task<AppUser> AddAsync(string name)
    {
        var user = new AppUser { UserName = name, Email = $"{name}@voorbeeld.test", EmailConfirmed = true };
        Assert.True((await _users.CreateAsync(user, "wachtwoord")).Succeeded);
        return user;
    }

    [Fact]
    public async Task Name_and_email_change_and_the_player_can_log_in_with_them()
    {
        var user = await AddAsync("jef");

        var result = await _edit.EditAsync(" jef ", " Jefke ", "jef@nieuw.test");

        Assert.True(result!.Succeeded);
        var found = await _users.FindByNameAsync("jefke");
        Assert.Equal(user.Id, found!.Id);
        Assert.Equal(user.Id, (await _users.FindByEmailAsync("JEF@NIEUW.TEST"))!.Id);
        Assert.False(found.EmailConfirmed); // Google koppelt pas na aanmelden met het wachtwoord
        Assert.True(await _users.CheckPasswordAsync(found, "wachtwoord"));
        Assert.Null(await _users.FindByNameAsync("jef"));
    }

    [Fact]
    public async Task Only_a_name_change_keeps_the_email_confirmed()
    {
        await AddAsync("jef");

        Assert.True((await _edit.EditAsync("jef", "jefke", "JEF@voorbeeld.test"))!.Succeeded);

        Assert.True((await _users.FindByNameAsync("jefke"))!.EmailConfirmed);
    }

    [Fact]
    public async Task A_taken_name_or_email_changes_nothing()
    {
        await AddAsync("jef");
        await AddAsync("mieke");

        var name = await _edit.EditAsync("jef", "Mieke", "jef@nieuw.test");
        var mail = await _edit.EditAsync("jef", "jefke", "mieke@voorbeeld.test");

        Assert.Contains(name!.Errors, e => e.Code == "DuplicateUserName");
        Assert.Contains(mail!.Errors, e => e.Code == "DuplicateEmail");
        _scope.ServiceProvider.GetRequiredService<QuizDbContext>().ChangeTracker.Clear();
        var jef = await _users.FindByNameAsync("jef");
        Assert.Equal("jef@voorbeeld.test", jef!.Email);
    }

    [Fact]
    public async Task An_empty_or_invalid_email_is_refused()
    {
        await AddAsync("jef");

        Assert.Contains((await _edit.EditAsync("jef", "jef", ""))!.Errors, e => e.Code == "InvalidEmail");
        Assert.Contains((await _edit.EditAsync("jef", "jef", "geen-adres"))!.Errors, e => e.Code == "InvalidEmail");
    }

    [Fact]
    public async Task Unknown_or_deleted_player_gives_null()
    {
        var user = await AddAsync("weg");
        user.DeletedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        Assert.Null(await _edit.EditAsync("bestaat-niet", "x1", "x@voorbeeld.test"));
        Assert.Null(await _edit.EditAsync("weg", "terug", "weg@voorbeeld.test"));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }
}
