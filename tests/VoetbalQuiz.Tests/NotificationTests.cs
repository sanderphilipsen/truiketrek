using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class NotificationTests : IDisposable
{
    private readonly TestDb _t = new();

    [Fact]
    public async Task New_feedback_notifies_the_admins_but_not_the_admin_who_sent_it()
    {
        var (admin, tom) = (_t.AddUser("Admin"), _t.AddUser("tom"));

        await _t.Feedback.AddAsync(tom, new FeedbackRequest(FeedbackType.Bug, "Timer hangt", "De timer loopt niet verder."));
        var item = Assert.Single(await _t.Notifications.ListAsync(admin));
        Assert.Equal((NotificationKind.NieuweFeedback, "Nieuwe bug gemeld", "tom: Timer hangt", "admin/feedback"), (item.Kind, item.Title, item.Text, item.Link));
        Assert.True(item.IsNew);
        Assert.Empty(await _t.Notifications.ListAsync(tom));

        await _t.Feedback.AddAsync(admin, new FeedbackRequest(FeedbackType.Feature, "Meer specials", "Graag een derby-special."));
        Assert.Equal(1, (await _t.Notifications.CountUnreadAsync(admin)).Unread);
    }

    [Fact]
    public async Task The_reporter_hears_about_a_reply_or_a_new_status_but_not_about_an_unchanged_save()
    {
        var tom = _t.AddUser("tom");
        await _t.Feedback.AddAsync(tom, new FeedbackRequest(FeedbackType.Bug, "Timer hangt", "De timer loopt niet verder."));
        var id = (await _t.Feedback.ListAsync(tom, admin: false)).Items.Single().Id;

        await _t.Feedback.UpdateAsync(id, new FeedbackUpdate(FeedbackStatus.Bekeken, null));
        _t.Clock.Advance(TimeSpan.FromMinutes(1));
        await _t.Feedback.UpdateAsync(id, new FeedbackUpdate(FeedbackStatus.Opgelost, "Opgelost, merci!"));
        _t.Clock.Advance(TimeSpan.FromMinutes(1));
        await _t.Feedback.UpdateAsync(id, new FeedbackUpdate(FeedbackStatus.Opgelost, " Opgelost, merci! "));

        var items = await _t.Notifications.ListAsync(tom);
        Assert.Equal(
            [("Antwoord op je melding", "Timer hangt: Opgelost, merci!"), ("Je melding staat op bekeken", "Timer hangt")],
            items.Select(n => (n.Title, n.Text!)));
        Assert.All(items, n => Assert.Equal((NotificationKind.FeedbackAntwoord, "feedback"), (n.Kind, n.Link)));
    }

    [Fact]
    public async Task Announcements_reach_existing_players_and_opening_the_list_marks_everything_read()
    {
        var tom = _t.AddUser("tom");
        _t.Clock.Advance(TimeSpan.FromMinutes(1));
        await _t.Notifications.AddAnnouncementAsync(new AnnouncementRequest(" Nieuw: de academie ", "Speel oude dailies opnieuw.", "/academie"));
        _t.Clock.Advance(TimeSpan.FromMinutes(1));
        var newcomer = _t.AddUser("jan");
        _t.Db.Users.Single(u => u.Id == newcomer).CreatedAt = _t.Clock.GetUtcNow().UtcDateTime;
        await _t.Db.SaveChangesAsync();

        Assert.Equal(1, (await _t.Notifications.CountUnreadAsync(tom)).Unread);
        var news = Assert.Single(await _t.Notifications.ListAsync(tom));
        Assert.Equal((NotificationKind.Nieuws, "Nieuw: de academie", "academie", true), (news.Kind, news.Title, news.Link, news.IsNew));
        // Wie zich na de releasenota registreert, krijgt ze niet.
        Assert.Empty(await _t.Notifications.ListAsync(newcomer));
        Assert.Equal(0, (await _t.Notifications.CountUnreadAsync(newcomer)).Unread);

        await _t.Notifications.MarkAllReadAsync(tom);
        _t.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await _t.Notifications.CountUnreadAsync(tom)).Unread);
        Assert.False(Assert.Single(await _t.Notifications.ListAsync(tom)).IsNew);
    }

    [Fact]
    public async Task Read_notifications_disappear_after_thirty_days()
    {
        var tom = _t.AddUser("tom");
        _t.Notifications.Add(tom, NotificationKind.FeedbackAntwoord, "Oud", null, null);
        await _t.Db.SaveChangesAsync();
        await _t.Notifications.MarkAllReadAsync(tom);

        _t.Clock.Advance(TimeSpan.FromDays(NotificationRules.KeepReadDays - 1));
        _t.Notifications.Add(tom, NotificationKind.FeedbackAntwoord, "Ongelezen", null, null);
        await _t.Db.SaveChangesAsync();
        Assert.Equal(2, (await _t.Notifications.ListAsync(tom)).Count);

        _t.Clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(["Ongelezen"], (await _t.Notifications.ListAsync(tom)).Select(n => n.Title));
    }

    [Theory]
    [InlineData("Hm", "Een tekst die lang genoeg is.", null)]
    [InlineData("Nieuw", "Kort", null)]
    [InlineData("Nieuw", "Een tekst die lang genoeg is.", "https://elders.be")]
    [InlineData("Nieuw", "Een tekst die lang genoeg is.", "javascript:alert(1)")]
    [InlineData("Nieuw", "Een tekst die lang genoeg is.", "//elders.be")]
    public async Task Invalid_announcements_are_refused(string title, string text, string? link)
    {
        await Assert.ThrowsAsync<QuizException>(() => _t.Notifications.AddAnnouncementAsync(new AnnouncementRequest(title, text, link)));
        Assert.Empty(await _t.Notifications.ListAnnouncementsAsync());
    }

    [Fact]
    public async Task An_admin_deletes_an_announcement()
    {
        await _t.Notifications.AddAnnouncementAsync(new AnnouncementRequest("Nieuw", "Een tekst die lang genoeg is.", ""));
        var a = Assert.Single(await _t.Notifications.ListAnnouncementsAsync());
        Assert.Null(a.Link);

        Assert.True(await _t.Notifications.DeleteAnnouncementAsync(a.Id));
        Assert.False(await _t.Notifications.DeleteAnnouncementAsync(a.Id));
        Assert.Empty(await _t.Notifications.ListAnnouncementsAsync());
    }

    public void Dispose() => _t.Dispose();
}
