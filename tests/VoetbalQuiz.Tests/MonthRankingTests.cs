using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class MonthRankingTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly MonthRankingService _ranking;

    public MonthRankingTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);
        _ranking = _t.Ranking;
    }

    private string User(string name)
    {
        var user = new AppUser { UserName = name };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }

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

    [Fact]
    public async Task Online_points_depend_on_opponents_beaten()
    {
        var a = User("a"); var b = User("b"); var c = User("c"); var d = User("d");
        Match((a, 1), (b, 2), (c, 3), (d, 4));

        var entries = (await _ranking.GetAsync(a, 2026, 10)).Entries.ToDictionary(e => e.UserName);
        Assert.Equal(MonthRules.OnlinePoints(3), entries["a"].Online); // 400
        Assert.Equal(MonthRules.OnlinePoints(2), entries["b"].Online); // 300
        Assert.Equal(MonthRules.OnlinePoints(0), entries["d"].Online); // 100: meedoen telt ook
        Assert.Equal(1, entries["a"].MatchesWon);
    }

    [Fact]
    public async Task Shared_place_counts_half_an_opponent()
    {
        var a = User("a"); var b = User("b"); var c = User("c");
        Match((a, 2), (b, 2), (c, 1));
        var entries = (await _ranking.GetAsync(a, 2026, 10)).Entries.ToDictionary(e => e.UserName);
        Assert.Equal(MonthRules.OnlinePoints(0.5), entries["a"].Online);
    }

    [Fact]
    public async Task Only_the_best_online_matches_count()
    {
        var a = User("a"); var b = User("b");
        for (var i = 0; i < MonthRules.OnlineBestMatches + 5; i++) Match((a, 1), (b, 2));

        var me = (await _ranking.GetAsync(a, 2026, 10)).Entries.Single(e => e.UserName == "a");
        Assert.Equal(MonthRules.OnlineBestMatches * MonthRules.OnlinePoints(1), me.Online);
        Assert.Equal(MonthRules.OnlineBestMatches + 5, me.Matches);
    }

    public void Dispose() => _t.Dispose();
}
