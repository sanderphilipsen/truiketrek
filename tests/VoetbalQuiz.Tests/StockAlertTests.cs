using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class StockAlertTests : IDisposable
{
    /// <summary>Maandag 19 oktober 2026, 12:00 in België.</summary>
    private readonly TestDb _t = new(new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero));
    private readonly string _admin;
    private readonly string _tom;

    public StockAlertTests()
    {
        (_admin, _tom) = (_t.AddUser("admin"), _t.AddUser("tom"));
    }

    private void Plan(QuestionType type)
    {
        foreach (var day in QuestionTypes.Week) _t.Db.DailySchedule.Add(new DailyScheduleDay { Day = day, Type = type });
        _t.Db.SaveChanges();
    }

    private void AddPlayers(int count)
    {
        for (var i = 1; i <= count; i++)
            _t.Db.Players.Add(new Player
            {
                Name = $"Speler {i}", Era = "modern", Status = PlayerStatus.Approved,
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h} van speler {i}")],
            });
        _t.Db.SaveChanges();
    }

    private void AddSnelvuur(int count, PlayerStatus status)
    {
        for (var i = 1; i <= count; i++)
            _t.Db.QuizItems.Add(new QuizItem
            {
                Type = QuestionType.Snelvuur, Key = $"{status}{i}", Status = status,
                Data = QuestionEngine.Serialize(new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], 0)),
            });
        _t.Db.SaveChanges();
    }

    private async Task<List<NotificationItem>> NextDayAsync()
    {
        _t.Clock.Advance(TimeSpan.FromDays(1));
        await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        return await _t.Notifications.ListAsync(_admin);
    }

    [Fact]
    public async Task The_admins_hear_once_when_players_start_coming_back()
    {
        Plan(QuestionType.WieBenIk);
        AddPlayers(15);

        await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        Assert.Empty(await _t.Notifications.ListAsync(_admin));

        // Dinsdag: nog 5 nieuwe spelers, dus 5 van de 10 komen terug.
        var item = Assert.Single(await NextDayAsync());
        Assert.Equal((NotificationKind.Voorraad, "Spelers komen terug", "admin/spelers"), (item.Kind, item.Title, item.Link));
        Assert.Equal("Vandaag 5 van de 10 al eerder gevraagd. Nog 0 ongebruikte goedgekeurde en 0 te keuren spelers.", item.Text);
        Assert.Empty(await _t.Notifications.ListAsync(_tom));

        // Woensdag komen er weer spelers terug, maar dat wist de admin al.
        Assert.Single(await NextDayAsync());
    }

    [Fact]
    public async Task The_admins_are_warned_once_when_the_unused_questions_no_longer_cover_the_coming_weeks()
    {
        // 12 snelvuurdagen in de komende 14 (de zondagen zijn De Zondag) = 120 vragen nodig.
        Plan(QuestionType.Snelvuur);
        AddSnelvuur(130, PlayerStatus.Approved);
        AddSnelvuur(3, PlayerStatus.Review);

        await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        Assert.Empty(await _t.Notifications.ListAsync(_admin));

        var item = Assert.Single(await NextDayAsync());
        Assert.Equal((NotificationKind.Voorraad, "Snelvuur: voorraad raakt op", "admin/vragen"), (item.Kind, item.Title, item.Link));
        Assert.Equal("Nog 110 ongebruikte goedgekeurde vragen, de komende 14 dagen zijn er 120 nodig. 3 te keuren.", item.Text);

        Assert.Single(await NextDayAsync());
    }

    [Fact]
    public async Task The_admins_hear_when_the_daily_falls_back_to_wie_ben_ik()
    {
        Plan(QuestionType.WieBenIk);
        AddPlayers(40);

        // Zondag is De Zondag, maar er zijn geen krantenartikels.
        _t.Clock.Advance(TimeSpan.FromDays(5));
        var item = Assert.Single(await NextDayAsync());
        Assert.Equal(DayOfWeek.Sunday, _t.Quiz.Today.DayOfWeek);
        Assert.Equal(("Daily viel terug op Wie ben ik?", "Te weinig vragen voor De Zondag (0 beschikbaar, 4 nodig)."), (item.Title, item.Text));
    }

    public void Dispose() => _t.Dispose();
}
