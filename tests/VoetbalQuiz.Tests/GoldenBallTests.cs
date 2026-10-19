using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class GoldenBallTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly GoldenBallService _goldenBall;
    private readonly ProgressService _progress;

    public GoldenBallTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var results = new ResultsService(_db, _clock, new MemoryCache(new MemoryCacheOptions()));
        var ranking = new MonthRankingService(_db, results);
        _goldenBall = new GoldenBallService(_db, _clock, ranking, results);
        _progress = new ProgressService(_db, _clock, results, new OnlineService(_db, results), ranking, _goldenBall);
    }

    private string User(string name)
    {
        var user = new AppUser { UserName = name };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }

    /// <summary>Een uitgespeelde online wedstrijd "nu" met de opgegeven eindplaatsen.</summary>
    private void Match(params (string UserId, int Rank)[] players)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        _db.Matches.Add(new Match
        {
            Name = "Partijtje", HostUserId = players[0].UserId, Seats = players.Length, Status = MatchStatus.Finished,
            CreatedAt = now, FinishedAt = now,
            Players = [.. players.Select(p => new MatchPlayer { UserId = p.UserId, Rank = p.Rank, JoinedAt = now })],
        });
        _db.SaveChanges();
    }

    /// <summary>Naar een dag in een volgende maand, 08:00 UTC (9 of 10 uur in België).</summary>
    private void GoTo(int year, int month, int day) =>
        _clock.SetUtcNow(new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Winner_of_last_month_gets_the_golden_ball_and_the_border()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));

        // Zolang september loopt, is er nog niets uitgereikt.
        Assert.Empty((await _goldenBall.GetAsync(a)).History);

        GoTo(2026, 10, 1);
        var overview = await _goldenBall.GetAsync(b);
        var holder = Assert.Single(overview.Holders);
        Assert.Equal("a", holder.UserName);
        Assert.Equal("Gouden Bal september", holder.Title);
        Assert.False(holder.IsMe);
        Assert.True(overview.Announce);
        Assert.Equal(1, overview.Stars["a"]);
        Assert.False(overview.Stars.ContainsKey("b"));

        // Enkel op de eerste dag groot op het dashboard; de rand blijft de hele maand.
        GoTo(2026, 10, 2);
        overview = await _goldenBall.GetAsync(a);
        Assert.False(overview.Announce);
        Assert.Single(overview.Holders);
    }

    [Fact]
    public async Task Border_moves_on_but_stars_stay_forever()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));          // september: a
        GoTo(2026, 10, 15);
        Match((b, 1), (a, 2));          // oktober: b
        GoTo(2026, 11, 1);

        var overview = await _goldenBall.GetAsync(a);
        Assert.Equal("b", Assert.Single(overview.Holders).UserName);
        Assert.Equal(1, overview.Stars["a"]);
        Assert.Equal(1, overview.Stars["b"]);
        Assert.Equal(["b", "a"], overview.History.Select(h => h.UserName));

        // Een maand zonder spel: niemand draagt de rand, de sterren blijven.
        GoTo(2027, 1, 5);
        overview = await _goldenBall.GetAsync(a);
        Assert.Empty(overview.Holders);
        Assert.Equal(2, overview.Stars.Values.Sum());
    }

    [Fact]
    public async Task Shared_first_place_gives_both_a_golden_ball()
    {
        var a = User("a"); var b = User("b"); var c = User("c");
        Match((a, 1), (b, 1), (c, 3));
        GoTo(2026, 10, 1);

        var overview = await _goldenBall.GetAsync(a);
        Assert.Equal(["a", "b"], overview.Holders.Select(h => h.UserName).Order());
        Assert.True(overview.Holders.Single(h => h.UserName == "a").IsMe);
    }

    [Fact]
    public async Task A_month_is_settled_only_once()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));
        GoTo(2026, 10, 1);
        await _goldenBall.GetAsync(a);

        // Een late wijziging aan september verandert de uitgereikte Gouden Bal niet meer.
        foreach (var p in _db.MatchPlayers) p.Rank = p.UserId == b ? 1 : 2;
        await _db.SaveChangesAsync();
        Assert.Equal("a", Assert.Single((await _goldenBall.GetAsync(a)).Holders).UserName);
        Assert.Equal(1, await _db.GoldenBalls.CountAsync());
    }

    [Fact]
    public async Task Golden_ball_badges()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));          // september: a, b tweede
        GoTo(2026, 10, 10);
        Match((a, 1), (b, 2));          // oktober: a
        GoTo(2026, 11, 1);

        var badges = (await _progress.GetProfileAsync(a, "a")).Badges.Where(x => x.Earned).Select(x => x.Id).ToList();
        Assert.Contains("seizoenskampioen", badges);
        Assert.Contains("titelverdediger", badges);
        Assert.DoesNotContain("gouden-hattrick", badges);

        var second = (await _progress.GetProfileAsync(b, "b")).Badges.Where(x => x.Earned).Select(x => x.Id).ToList();
        Assert.Contains("zilveren-bal", second);
        Assert.DoesNotContain("seizoenskampioen", second);
    }

    [Fact]
    public async Task Month_is_only_settled_once_the_utc_month_is_over_too()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));

        // 1 oktober 00:30 in België, maar in UTC nog 30 september: een wedstrijd nu telt nog voor september.
        _clock.SetUtcNow(new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero));
        Match((b, 1), (a, 2));
        Match((b, 1), (a, 2));
        Assert.Empty((await _goldenBall.GetAsync(a)).History);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 0, 10, 0, TimeSpan.Zero));
        var overview = await _goldenBall.GetAsync(a);
        Assert.Equal("b", Assert.Single(overview.Holders).UserName);
        Assert.True(overview.Announce);
    }

    [Fact]
    public async Task Special_running_over_the_month_end_delays_the_golden_ball()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));
        _db.SpecialEvents.Add(new SpecialEvent
        {
            Theme = "eigen", Name = "Weekend", StartDate = new DateOnly(2026, 9, 29), EndDate = new DateOnly(2026, 10, 2),
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
        });
        await _db.SaveChangesAsync();

        GoTo(2026, 10, 1);
        Assert.Empty((await _goldenBall.GetAsync(a)).History);

        // De dag nadat de special afliep: toegekend, en dan ook die dag groot op het dashboard.
        GoTo(2026, 10, 3);
        var overview = await _goldenBall.GetAsync(a);
        Assert.Equal("a", Assert.Single(overview.Holders).UserName);
        Assert.True(overview.Announce);

        GoTo(2026, 10, 4);
        Assert.False((await _goldenBall.GetAsync(a)).Announce);
    }

    [Fact]
    public async Task Hattrick_and_title_defence_over_the_year_end()
    {
        var a = User("a"); var b = User("b");
        foreach (var (year, month) in new[] { (2026, 11), (2026, 12), (2027, 1) })
        {
            GoTo(year, month, 10);
            Match((a, 1), (b, 2));
        }
        GoTo(2027, 2, 1);

        var badges = (await _progress.GetProfileAsync(a, "a")).Badges.Where(x => x.Earned).Select(x => x.Id).ToList();
        Assert.Contains("gouden-hattrick", badges);
        Assert.Contains("titelverdediger", badges);
        Assert.DoesNotContain("messi-niveau", badges);
        Assert.Equal(3, (await _goldenBall.GetAsync(a)).Stars["a"]);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
