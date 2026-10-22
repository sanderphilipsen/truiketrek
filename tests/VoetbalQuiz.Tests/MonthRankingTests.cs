using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class MonthRankingTests : IDisposable
{
    private readonly TestDb _t;
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly MonthRankingService _ranking;

    public MonthRankingTests() : this(null) { }

    private MonthRankingTests(DateTimeOffset? now)
    {
        _t = new TestDb(now);
        (_db, _clock) = (_t.Db, _t.Clock);
        _ranking = _t.Ranking;
        for (var i = 1; i <= 30; i++)
        {
            _db.Players.Add(new Player
            {
                Name = $"Speler {i}", Status = PlayerStatus.Approved,
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")], Sources = ["https://example.org"],
            });
        }
        _db.SaveChanges();
    }

    private string User(string name) => _t.AddUser(name);

    /// <summary>Een uitgespeelde online wedstrijd met de opgegeven eindplaatsen.</summary>
    private void Match(params (string UserId, int Rank)[] players)
    {
        _db.Matches.Add(new Match
        {
            Name = "Partijtje", HostUserId = players[0].UserId, Seats = players.Length, Status = MatchStatus.Finished,
            CreatedAt = _clock.GetUtcNow().UtcDateTime, FinishedAt = _clock.GetUtcNow().UtcDateTime,
            Players = [.. players.Select(p => new MatchPlayer { UserId = p.UserId, Rank = p.Rank, JoinedAt = _clock.GetUtcNow().UtcDateTime })],
        });
        _db.SaveChanges();
    }

    /// <summary>De daily van vandaag helemaal spelen: alles juist bij de eerste tip, of alles passen.</summary>
    private async Task PlayAllAsync(string userId, bool correct)
    {
        var quiz = _t.Quiz;
        var challenge = await quiz.GetOrCreateChallengeAsync(quiz.Today);
        foreach (var q in challenge.Questions.OrderBy(q => q.Number))
        {
            await quiz.StartAsync(userId, q.Number);
            if (correct) await quiz.GuessAsync(userId, q.Number, q.Player!.Name);
            else await quiz.GiveUpAsync(userId, q.Number);
        }
    }

    // ---------- Oude telling: tot en met september 2026 telde online mee ----------

    /// <summary>Een testopzet in september 2026, toen online nog meetelde.</summary>
    private static MonthRankingTests September() => new(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Online_points_depend_on_opponents_beaten()
    {
        using var t = September();
        var a = t.User("a"); var b = t.User("b"); var c = t.User("c"); var d = t.User("d");
        t.Match((a, 1), (b, 2), (c, 3), (d, 4));

        var entries = (await t._ranking.GetAsync(a, 2026, 9)).Entries.ToDictionary(e => e.UserName);
        Assert.Equal(MonthRules.OnlinePoints(3), entries["a"].Online); // 400
        Assert.Equal(MonthRules.OnlinePoints(2), entries["b"].Online); // 300
        Assert.Equal(MonthRules.OnlinePoints(0), entries["d"].Online); // 100: meedoen telt ook
        Assert.Equal(1, entries["a"].MatchesWon);
    }

    [Fact]
    public async Task Shared_place_counts_half_an_opponent()
    {
        using var t = September();
        var a = t.User("a"); var b = t.User("b"); var c = t.User("c");
        t.Match((a, 2), (b, 2), (c, 1));
        var entries = (await t._ranking.GetAsync(a, 2026, 9)).Entries.ToDictionary(e => e.UserName);
        Assert.Equal(MonthRules.OnlinePoints(0.5), entries["a"].Online);
    }

    [Fact]
    public async Task Draw_gives_half_an_opponent_but_no_win()
    {
        using var t = September();
        var a = t.User("a"); var b = t.User("b");
        t.Match((a, 1), (b, 1));
        var entries = (await t._ranking.GetAsync(a, 2026, 9)).Entries.ToDictionary(e => e.UserName);
        Assert.Equal(MonthRules.OnlinePoints(0.5), entries["a"].Online);
        Assert.Equal(MonthRules.OnlinePoints(0.5), entries["b"].Online);
        Assert.Equal(0, entries["a"].MatchesWon);
        Assert.Equal(0, entries["b"].MatchesWon);
    }

    [Fact]
    public async Task Only_the_best_online_matches_count()
    {
        using var t = September();
        var a = t.User("a"); var b = t.User("b");
        for (var i = 0; i < MonthRules.OnlineBestMatches + 5; i++) t.Match((a, 1), (b, 2));

        var me = (await t._ranking.GetAsync(a, 2026, 9)).Entries.Single(e => e.UserName == "a");
        Assert.Equal(MonthRules.OnlineBestMatches * MonthRules.OnlinePoints(1), me.Online);
        Assert.Equal(MonthRules.OnlineBestMatches + 5, me.Matches);
    }

    // ---------- Sinds oktober 2026: enkel dailies en specials ----------

    [Fact]
    public async Task Online_matches_no_longer_count_from_october_2026()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2)); // 17 oktober 2026

        var october = await _ranking.GetAsync(a, 2026, 10);
        Assert.Empty(october.Entries); // online alleen zet je niet in het maandklassement
        Assert.DoesNotContain("online", october.Explanation, StringComparison.OrdinalIgnoreCase);

        // Een maand met enkel online wedstrijden levert geen seizoensbekers meer op.
        Assert.Empty(await _ranking.ActiveMonthsAsync(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Online_counts_until_september_2026()
    {
        Assert.True(MonthRules.OnlineCounts(2026, 9));
        Assert.False(MonthRules.OnlineCounts(2026, 10));
        Assert.False(MonthRules.OnlineCounts(2027, 1));
        Assert.Contains("online", MonthRules.Explanation(2026, 9), StringComparison.OrdinalIgnoreCase);
    }

    // ---------- De stand als op tv: zones en pijltjes ----------

    [Theory]
    [InlineData(1, 5, RankZone.Prize)]
    [InlineData(3, 5, RankZone.Prize)]
    [InlineData(4, 5, RankZone.None)]
    [InlineData(5, 5, RankZone.None)]   // te weinig spelers voor een degradatiezone
    [InlineData(9, 9, RankZone.None)]
    [InlineData(7, 10, RankZone.None)]
    [InlineData(8, 10, RankZone.Drop)]  // onderste 3 van 10
    [InlineData(10, 10, RankZone.Drop)]
    [InlineData(3, 10, RankZone.Prize)]
    public void Zones_follow_the_month_rules(int rank, int players, RankZone expected) =>
        Assert.Equal(expected, MonthRules.Zone(rank, players));

    [Fact]
    public async Task Movement_compares_with_the_standing_at_the_end_of_yesterday()
    {
        var a = User("a"); var b = User("b");

        // Dag 1: allebei alles juist; a was eerst klaar en wint de dag (+100), b tweede (+50).
        await PlayAllAsync(a, correct: true);
        await PlayAllAsync(b, correct: true);
        _clock.Advance(TimeSpan.FromDays(1));

        // Dag 2: enkel b speelt (en haalt a in).
        await PlayAllAsync(b, correct: true);

        var ranking = await _ranking.GetAsync(a, 2026, 10);
        Assert.True(ranking.ShowMovement);
        var entries = ranking.Entries.ToDictionary(e => e.UserName);
        Assert.Equal(1, entries["b"].Rank);
        Assert.Equal(2, entries["b"].PreviousRank);
        Assert.Equal(1, entries["b"].Change);
        Assert.Equal(2, entries["a"].Rank);
        Assert.Equal(1, entries["a"].PreviousRank);
        Assert.Equal(-1, entries["a"].Change);
        Assert.True(entries["a"].IsMe);

        // Wie vandaag voor het eerst meedoet, is nieuw: geen pijltje.
        var c = User("c");
        await PlayAllAsync(c, correct: false);
        var newcomer = (await _ranking.GetAsync(a, 2026, 10)).Entries.Single(e => e.UserName == "c");
        Assert.Null(newcomer.PreviousRank);
        Assert.Null(newcomer.Change);
    }

    [Fact]
    public async Task A_finished_month_shows_no_movement()
    {
        var a = User("a");
        await PlayAllAsync(a, correct: true);
        _clock.Advance(TimeSpan.FromDays(20)); // november

        var october = await _ranking.GetAsync(a, 2026, 10);
        Assert.False(october.ShowMovement);
        Assert.All(october.Entries, e => Assert.Null(e.PreviousRank));
    }

    public void Dispose() => _t.Dispose();
}
