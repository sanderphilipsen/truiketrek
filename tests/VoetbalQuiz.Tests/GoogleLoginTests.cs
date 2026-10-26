using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class GoogleLoginTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly UserManager<AppUser> _users;
    private readonly GoogleLoginService _google;

    public GoogleLoginTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<QuizDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<QuizDbContext>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<GoogleLoginService>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _scope.ServiceProvider.GetRequiredService<QuizDbContext>().Database.EnsureCreated();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _google = _scope.ServiceProvider.GetRequiredService<GoogleLoginService>();
    }

    private static ExternalLoginInfo Info(string key, string email, bool verified = true, string? givenName = "Jan")
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email), new(GoogleLoginService.EmailVerifiedClaim, verified ? "True" : "False") };
        if (givenName is not null) claims.Add(new Claim(ClaimTypes.GivenName, givenName));
        return new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, "Google")), "Google", key, "Google");
    }

    [Fact]
    public async Task Existing_account_with_an_unconfirmed_email_is_not_linked_automatically()
    {
        // Bij /register wordt het adres niet bevestigd: wie dat Gmail-adres bezit, mag het account niet zomaar krijgen.
        var user = new AppUser { UserName = "jan", Email = "jan@voorbeeld.be" };
        Assert.True((await _users.CreateAsync(user, "Wachtwoord1!")).Succeeded);

        var (next, found) = await _google.FindOrLinkAsync(Info("g-1", "JAN@voorbeeld.be"));

        Assert.Equal(GoogleNext.SignInWithPasswordFirst, next);
        Assert.Null(found);
        Assert.Null(await _users.FindByLoginAsync("Google", "g-1"));
        Assert.False((await _users.FindByIdAsync(user.Id))!.EmailConfirmed);
    }

    [Fact]
    public async Task Existing_account_with_a_confirmed_email_is_not_linked_automatically()
    {
        // Ook een bevestigd adres kan later bij iemand anders terechtkomen: altijd eerst het wachtwoord.
        var user = new AppUser { UserName = "jan", Email = "jan@voorbeeld.be", EmailConfirmed = true };
        Assert.True((await _users.CreateAsync(user, "Wachtwoord1!")).Succeeded);

        var (next, found) = await _google.FindOrLinkAsync(Info("g-1", "JAN@voorbeeld.be"));

        Assert.Equal(GoogleNext.SignInWithPasswordFirst, next);
        Assert.Null(found);
        Assert.Null(await _users.FindByLoginAsync("Google", "g-1"));
    }

    [Fact]
    public async Task Second_google_account_with_the_same_email_never_links()
    {
        // Heruitgegeven mailbox: een nieuwe Google-account (andere sub) op het adres van een bestaande Google-speler.
        var (user, _) = await _google.CreateAsync(Info("g-1", "jan@voorbeeld.be"), "jan");
        Assert.True((await _users.AddPasswordAsync(user!, "Wachtwoord1!")).Succeeded);
        var intruder = Info("g-2", "jan@voorbeeld.be");

        var (next, found) = await _google.FindOrLinkAsync(intruder);

        Assert.Equal(GoogleNext.EmailInUse, next);
        Assert.Null(found);
        Assert.False(await _google.LinkAfterPasswordAsync(user!, intruder));
        Assert.Null(await _users.FindByLoginAsync("Google", "g-2"));
    }

    [Fact]
    public async Task Existing_account_without_password_is_refused()
    {
        // Zonder wachtwoord kan de speler niet bewijzen dat het account van hem is.
        Assert.True((await _users.CreateAsync(new AppUser { UserName = "jan", Email = "jan@voorbeeld.be", EmailConfirmed = true })).Succeeded);

        var (next, found) = await _google.FindOrLinkAsync(Info("g-1", "jan@voorbeeld.be"));

        Assert.Equal(GoogleNext.EmailInUse, next);
        Assert.Null(found);
        Assert.Null(await _users.FindByLoginAsync("Google", "g-1"));
    }

    [Fact]
    public async Task Google_links_after_signing_in_with_the_password()
    {
        var user = new AppUser { UserName = "jan", Email = "jan@voorbeeld.be" };
        Assert.True((await _users.CreateAsync(user, "Wachtwoord1!")).Succeeded);
        var info = Info("g-1", "JAN@voorbeeld.be");

        Assert.True(await _google.LinkAfterPasswordAsync(user, info));

        Assert.True(user.EmailConfirmed);
        var (next, found) = await _google.FindOrLinkAsync(info);
        Assert.Equal(GoogleNext.SignIn, next);
        Assert.Equal(user.Id, found?.Id);
    }

    [Theory]
    [InlineData("ander@voorbeeld.be", true)]
    [InlineData("jan@voorbeeld.be", false)]
    public async Task Google_does_not_link_after_password_with_another_or_unverified_email(string email, bool verified)
    {
        var user = new AppUser { UserName = "jan", Email = "jan@voorbeeld.be" };
        Assert.True((await _users.CreateAsync(user, "Wachtwoord1!")).Succeeded);

        Assert.False(await _google.LinkAfterPasswordAsync(user, Info("g-1", email, verified)));

        Assert.Null(await _users.FindByLoginAsync("Google", "g-1"));
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Unverified_email_never_links()
    {
        Assert.True((await _users.CreateAsync(new AppUser { UserName = "jan", Email = "jan@voorbeeld.be", EmailConfirmed = true }, "Wachtwoord1!")).Succeeded);

        var (next, found) = await _google.FindOrLinkAsync(Info("g-1", "jan@voorbeeld.be", verified: false));

        Assert.Equal(GoogleNext.EmailNotVerified, next);
        Assert.Null(found);
        Assert.Null(await _users.FindByLoginAsync("Google", "g-1"));
    }

    [Fact]
    public async Task New_player_is_created_without_password_and_found_next_time()
    {
        var info = Info("g-2", "nieuw@voorbeeld.be");
        Assert.Equal(GoogleNext.ChooseUserName, (await _google.FindOrLinkAsync(info)).Next);

        var (user, result) = await _google.CreateAsync(info, "Nieuwkomer");

        Assert.True(result.Succeeded);
        Assert.False(await _users.HasPasswordAsync(user!));
        Assert.Equal("nieuw@voorbeeld.be", user!.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(user.Id, (await _google.FindOrLinkAsync(info)).User?.Id);
    }

    [Fact]
    public async Task New_player_needs_a_verified_google_email()
    {
        var (user, result) = await _google.CreateAsync(Info("g-6", "nieuw@voorbeeld.be", verified: false), "Nieuwkomer");

        Assert.Null(user);
        Assert.Contains(result.Errors, e => e.Code == GoogleLoginService.EmailNotVerifiedCode);
        Assert.Null(await _users.FindByNameAsync("Nieuwkomer"));
    }

    [Fact]
    public async Task Taken_user_name_is_refused()
    {
        Assert.True((await _users.CreateAsync(new AppUser { UserName = "sander", Email = "sander@voorbeeld.be" }, "Wachtwoord1!")).Succeeded);

        var (user, result) = await _google.CreateAsync(Info("g-3", "ander@voorbeeld.be"), "SANDER");

        Assert.Null(user);
        Assert.Contains(result.Errors, e => e.Code == "DuplicateUserName");
        Assert.Null(await _users.FindByEmailAsync("ander@voorbeeld.be"));
    }

    [Fact]
    public async Task Suggestion_is_a_free_clean_user_name()
    {
        Assert.True((await _users.CreateAsync(new AppUser { UserName = "Jose", Email = "jose@voorbeeld.be" }, "Wachtwoord1!")).Succeeded);

        Assert.Equal("Jose2", await _google.SuggestUserNameAsync(Info("g-4", "x@voorbeeld.be", givenName: "José")));
        Assert.Equal("kevin.db", await _google.SuggestUserNameAsync(Info("g-5", "kevin.db@voorbeeld.be", givenName: null)));
    }

    [Theory]
    [InlineData("Zoë Van den Bossche-Ünal", "ZoeVandenBossche-Una")]
    [InlineData("李", "speler")]
    [InlineData(null, "speler")]
    public void Clean_user_name_keeps_allowed_characters(string? name, string expected) =>
        Assert.Equal(expected, GoogleLoginService.CleanUserName(name));

    [Theory]
    [InlineData("online/12", "online/12")]
    [InlineData("daily?dag=3", "daily?dag=3")]
    [InlineData(null, "")]
    [InlineData("/evil.example", "")]
    [InlineData("//evil.example", "")]
    [InlineData("\t//evil.example", "")]
    [InlineData("\t/evil.example", "")]
    [InlineData(" //evil.example", "")]
    [InlineData("/\\evil.example", "")]
    [InlineData("\\\\evil.example", "")]
    [InlineData("https://evil.example", "")]
    [InlineData("javascript:alert(1)", "")]
    [InlineData("online\r\nSet-Cookie: x=1", "")]
    public void Return_path_stays_inside_the_app(string? terug, string expected) =>
        Assert.Equal(expected, ReturnPath.Safe(terug));

    [Theory]
    [InlineData("online/12")]
    [InlineData("%09//evil.example")]
    [InlineData("..//evil.example")]
    [InlineData("./%2F%2Fevil.example")]
    public void Safe_return_path_resolves_to_the_own_site(string terug)
    {
        var app = new Uri("https://truitjetrek.azurewebsites.net/");
        Assert.Equal(app.Host, new Uri(app, ReturnPath.Safe(terug)).Host);
        Assert.Equal(app.Host, new Uri(app, "/" + ReturnPath.Safe(terug)).Host);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }
}
