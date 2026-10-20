using Microsoft.Extensions.Configuration;
using VoetbalQuiz.Server.Endpoints;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class FeedbackTests : IDisposable
{
    private readonly TestDb _t = new();

    [Fact]
    public async Task Players_see_their_own_feedback_and_admins_see_everything()
    {
        var (me, other) = (_t.AddUser("sander"), _t.AddUser("tom"));
        await _t.Feedback.AddAsync(me, new FeedbackRequest(FeedbackType.Bug, "  Tip klopt niet ", "De derde tip is fout."));
        _t.Clock.Advance(TimeSpan.FromMinutes(1));
        await _t.Feedback.AddAsync(other, new FeedbackRequest(FeedbackType.Feature, "Meer specials", "Graag een derby-special."));

        var mine = await _t.Feedback.ListAsync(me, admin: false);
        var item = Assert.Single(mine.Items);
        Assert.Equal("Tip klopt niet", item.Title);
        Assert.Equal(TestDb.DefaultNow.UtcDateTime, item.CreatedAt);
        Assert.True(item.IsMine);

        var all = await _t.Feedback.ListAsync(me, admin: true);
        Assert.True(all.IsAdmin);
        Assert.Equal(["Meer specials", "Tip klopt niet"], all.Items.Select(i => i.Title)); // nieuwste eerst
    }

    [Fact]
    public async Task Too_short_feedback_is_refused()
    {
        var me = _t.AddUser("sander");
        await Assert.ThrowsAsync<QuizException>(() => _t.Feedback.AddAsync(me, new FeedbackRequest(FeedbackType.Bug, "Hm", "Iets is fout.")));
        await Assert.ThrowsAsync<QuizException>(() => _t.Feedback.AddAsync(me, new FeedbackRequest(FeedbackType.Bug, "Een titel", "Kort")));
        Assert.Empty((await _t.Feedback.ListAsync(me, admin: true)).Items);
    }

    [Fact]
    public async Task An_admin_answers_and_unknown_feedback_is_not_found()
    {
        var me = _t.AddUser("sander");
        await _t.Feedback.AddAsync(me, new FeedbackRequest(FeedbackType.Bug, "Tip klopt niet", "De derde tip is fout."));
        var id = (await _t.Feedback.ListAsync(me, admin: false)).Items.Single().Id;

        Assert.True(await _t.Feedback.UpdateAsync(id, new FeedbackUpdate(FeedbackStatus.Opgelost, "  Aangepast, merci! ")));
        var item = (await _t.Feedback.ListAsync(me, admin: false)).Items.Single();
        Assert.Equal((FeedbackStatus.Opgelost, "Aangepast, merci!"), (item.Status, item.Reply));

        Assert.True(await _t.Feedback.UpdateAsync(id, new FeedbackUpdate(FeedbackStatus.Bekeken, " ")));
        Assert.Null((await _t.Feedback.ListAsync(me, admin: false)).Items.Single().Reply);
        Assert.False(await _t.Feedback.UpdateAsync(id + 100, new FeedbackUpdate(FeedbackStatus.Bekeken, null)));
    }

    [Fact]
    public void Admins_come_from_the_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admins:0"] = "Sander" }).Build();
        Assert.True(CurrentUser.IsAdminName("sander", config));
        Assert.False(CurrentUser.IsAdminName("tom", config));
        Assert.False(CurrentUser.IsAdminName(null, config));
    }

    public void Dispose() => _t.Dispose();
}
