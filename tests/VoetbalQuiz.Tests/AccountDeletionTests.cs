using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class AccountDeletionTests : IDisposable
{
    private readonly TestDb _t = new();
    private AccountDeletionService Deletion => new(_t.Db, _t.Clock, _t.Notifications, _t.Results);

    /// <summary>Een speler zoals Identity hem aanmaakt: met genormaliseerde naam en e-mail.</summary>
    private string AddPlayer(string name)
    {
        var user = new AppUser
        {
            UserName = name, NormalizedUserName = name.ToUpperInvariant(),
            Email = $"{name}@example.com", NormalizedEmail = $"{name}@EXAMPLE.COM", PasswordHash = "hash",
            AvatarId = "a1", LastSeenAt = TestDb.DefaultNow.UtcDateTime,
        };
        _t.Db.Users.Add(user);
        _t.Db.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task A_request_notifies_the_admins_and_can_be_withdrawn()
    {
        var admin = AddPlayer("admin");
        var me = AddPlayer("sander");

        await Deletion.RequestAsync(me);

        Assert.Equal(TestDb.DefaultNow.UtcDateTime, (await _t.Db.Users.FindAsync(me))!.DeletionRequestedAt);
        var note = Assert.Single(await _t.Db.Notifications.ToListAsync());
        Assert.Equal((admin, NotificationKind.Verwijderaanvraag), (note.UserId, note.Kind));
        Assert.Equal(TestDb.DefaultNow.UtcDateTime, Assert.Single(await _t.Admin.UsersAsync(), u => u.UserName == "sander").DeletionRequestedAt);

        await Deletion.CancelAsync(me);
        _t.Db.ChangeTracker.Clear();
        Assert.Null((await _t.Db.Users.FindAsync(me))!.DeletionRequestedAt);
    }

    [Fact]
    public async Task Only_a_requested_account_can_be_deleted()
    {
        AddPlayer("sander");

        Assert.Null(await Deletion.DeleteAsync("sander"));
        Assert.Null(await Deletion.DeleteAsync("onbekend"));
        Assert.NotNull(await _t.Db.Users.SingleAsync(u => u.UserName == "sander"));
    }

    [Fact]
    public async Task An_admin_can_delete_without_a_request()
    {
        var me = AddPlayer("sander");

        Assert.Equal("sander", await Deletion.DeleteAsync("sander", onlyRequested: false));

        _t.Db.ChangeTracker.Clear();
        var user = await _t.Db.Users.SingleAsync(u => u.Id == me);
        Assert.NotNull(user.DeletedAt);
        Assert.Null(user.Email);
        Assert.Null(await Deletion.DeleteAsync(user.UserName, onlyRequested: false));
    }

    [Fact]
    public async Task The_member_list_shows_email_login_and_admins()
    {
        var me = AddPlayer("sander");
        var google = AddPlayer("tom");
        _t.Db.UserLogins.Add(new IdentityUserLogin<string> { UserId = google, LoginProvider = "Google", ProviderKey = "1", ProviderDisplayName = "Google" });
        (await _t.Db.Users.FindAsync(google))!.PasswordHash = null;
        await _t.Db.SaveChangesAsync();

        var users = (await _t.Admin.UsersAsync(["SANDER"])).ToDictionary(u => u.UserName);

        Assert.Equal(("sander@example.com", true, false, true),
            (users["sander"].Email, users["sander"].HasPassword, users["sander"].HasGoogle, users["sander"].IsAdmin));
        Assert.Equal((false, true, false), (users["tom"].HasPassword, users["tom"].HasGoogle, users["tom"].IsAdmin));
    }

    [Fact]
    public async Task Deleting_wipes_personal_data_but_keeps_the_scores_anonymous()
    {
        var me = AddPlayer("sander");
        var other = AddPlayer("tom");
        _t.Db.UserLogins.Add(new IdentityUserLogin<string> { UserId = me, LoginProvider = "Google", ProviderKey = "123", ProviderDisplayName = "Google" });
        _t.Db.Feedback.Add(new Feedback { UserId = me, Type = FeedbackType.Bug, Title = "Fout", Description = "Iets is fout." });
        _t.Db.Feedback.Add(new Feedback { UserId = other, Type = FeedbackType.Bug, Title = "Ook fout", Description = "Nog iets." });
        _t.Db.Notifications.Add(new Notification { UserId = me, Kind = NotificationKind.Nieuws, Title = "Hallo" });
        var daily = new DailyChallenge { Date = new DateOnly(2026, 10, 16), WinnerUserId = me };
        _t.Db.DailyChallenges.Add(daily);
        await _t.Db.SaveChangesAsync();
        _t.Db.TrainingRuns.Add(new TrainingRun { UserId = me, DailyChallengeId = daily.Id });
        await _t.Db.SaveChangesAsync();
        await Deletion.RequestAsync(me);

        Assert.Equal("sander", await Deletion.DeleteAsync(" Sander "));

        _t.Db.ChangeTracker.Clear();
        var user = await _t.Db.Users.SingleAsync(u => u.Id == me);
        Assert.StartsWith(AccountDeletionService.AnonymousPrefix, user.UserName);
        Assert.Equal(user.UserName!.ToUpperInvariant(), user.NormalizedUserName);
        Assert.Null(user.Email);
        Assert.Null(user.NormalizedEmail);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.AvatarId);
        Assert.Null(user.LastSeenAt);
        Assert.Equal(TestDb.DefaultNow.UtcDateTime, user.DeletedAt);
        Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);

        Assert.Empty(await _t.Db.UserLogins.Where(l => l.UserId == me).ToListAsync());
        Assert.Equal("Ook fout", Assert.Single(await _t.Db.Feedback.ToListAsync()).Title);
        Assert.Empty(await _t.Db.Notifications.Where(n => n.UserId == me).ToListAsync());
        Assert.Empty(await _t.Db.TrainingRuns.ToListAsync());
        // De dagzege blijft, nu op naam van de anonieme speler.
        Assert.Equal(me, (await _t.Db.DailyChallenges.SingleAsync()).WinnerUserId);

        // Niet meer in de ledenlijst of de tellers, en de naam is weer vrij.
        Assert.Equal(["tom"], (await _t.Admin.UsersAsync()).Select(u => u.UserName));
        Assert.Equal(1, (await _t.Admin.UserCountsAsync()).Total);
        Assert.Null(await Deletion.DeleteAsync(user.UserName));
    }

    public void Dispose() => _t.Dispose();
}
