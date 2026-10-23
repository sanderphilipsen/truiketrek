using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class GoldenBallTests : IDisposable
{
    private readonly TestDb _t = new(new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly GoldenBallService _goldenBall;
    private readonly ProgressService _progress;

    public GoldenBallTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);
        (_goldenBall, _progress) = (_t.GoldenBall, _t.Progress);
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

    /// <summary>De daily van vandaag helemaal spelen: alles juist bij de eerste tip, of alles passen.</summary>
    private async Task PlayDailyAsync(string userId, bool correct)
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

        // Enkel op de eerste dag de strook bovenaan Home; de rand blijft de hele maand.
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
        await PlayDailyAsync(b, correct: true);  // oktober: b (online telt niet meer mee)
        await PlayDailyAsync(a, correct: false);
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
        await PlayDailyAsync(a, correct: true);  // oktober: a (met de daily; online telt niet meer mee)
        await PlayDailyAsync(b, correct: false);
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

        // De dag nadat de special afliep: toegekend. Geen strook meer (enkel op de eerste), wel nog de uitreiking.
        GoTo(2026, 10, 3);
        var overview = await _goldenBall.GetAsync(a);
        Assert.Equal("a", Assert.Single(overview.Holders).UserName);
        Assert.False(overview.Announce);
        Assert.NotNull(overview.Ceremony);
    }

    /// <summary>Sinds oktober 2026 telt online niet meer mee: de maand wordt met de daily gewonnen.</summary>
    [Fact]
    public async Task Hattrick_and_title_defence_over_the_year_end()
    {
        var a = User("a"); var b = User("b");
        foreach (var (year, month) in new[] { (2026, 11), (2026, 12), (2027, 1) })
        {
            GoTo(year, month, 10);
            await PlayDailyAsync(a, correct: true);
            await PlayDailyAsync(b, correct: false);
            Match((b, 1), (a, 2)); // online telt niet meer mee: b wint de wedstrijd, a de maand
        }
        GoTo(2027, 2, 1);

        var badges = (await _progress.GetProfileAsync(a, "a")).Badges.Where(x => x.Earned).Select(x => x.Id).ToList();
        Assert.Contains("gouden-hattrick", badges);
        Assert.Contains("titelverdediger", badges);
        Assert.DoesNotContain("messi-niveau", badges);
        Assert.Equal(3, (await _goldenBall.GetAsync(a)).Stars["a"]);
    }

    [Fact]
    public async Task Ceremony_is_shown_once_per_player_with_the_podium()
    {
        var a = User("a"); var b = User("b"); var c = User("c"); var d = User("d");
        Match((a, 1), (b, 2), (c, 3), (d, 4));
        GoTo(2026, 10, 12); // ook wie pas later in de maand binnenkomt, krijgt de uitreiking

        var ceremony = (await _goldenBall.GetAsync(b)).Ceremony;
        Assert.NotNull(ceremony);
        Assert.Equal((2026, 9), (ceremony.Year, ceremony.Month));
        var winner = Assert.Single(ceremony.Winners);
        Assert.Equal("a", winner.UserName);
        Assert.False(winner.IsMe);
        Assert.Equal([(2, "b", true), (3, "c", false)], ceremony.RunnersUp.Select(r => (r.Rank, r.UserName, r.IsMe)));

        // Gezien: niet meer voor b, wel nog voor de anderen.
        await _goldenBall.MarkCeremonySeenAsync(b);
        Assert.Null((await _goldenBall.GetAsync(b)).Ceremony);
        Assert.True(Assert.Single((await _goldenBall.GetAsync(a)).Ceremony!.Winners).IsMe);

        // Volgende maand een nieuwe uitreiking.
        await PlayDailyAsync(b, correct: true);
        GoTo(2026, 11, 3);
        Assert.Equal("b", Assert.Single((await _goldenBall.GetAsync(b)).Ceremony!.Winners).UserName);
    }

    [Fact]
    public async Task No_ceremony_for_accounts_made_after_the_award_or_months_without_winner()
    {
        var a = User("a"); var b = User("b");
        Match((a, 1), (b, 2));
        GoTo(2026, 10, 1);
        Assert.NotNull((await _goldenBall.GetAsync(a)).Ceremony); // legt september vast

        _clock.Advance(TimeSpan.FromHours(1));
        var late = new AppUser { UserName = "nieuw", CreatedAt = _clock.GetUtcNow().UtcDateTime };
        _db.Users.Add(late);
        await _db.SaveChangesAsync();
        Assert.Null((await _goldenBall.GetAsync(late.Id)).Ceremony);

        // Oktober zonder spel: geen winnaar, dus ook geen uitreiking.
        GoTo(2026, 11, 2);
        Assert.Null((await _goldenBall.GetAsync(a)).Ceremony);
    }

    [Fact]
    public async Task Shared_first_place_is_one_ceremony_with_both_winners()
    {
        var a = User("a"); var b = User("b"); var c = User("c");
        Match((a, 1), (b, 1), (c, 3));
        GoTo(2026, 10, 1);

        var ceremony = (await _goldenBall.GetAsync(c)).Ceremony!;
        Assert.Equal(["a", "b"], ceremony.Winners.Select(w => w.UserName).Order());
        Assert.Equal([(3, "c")], ceremony.RunnersUp.Select(r => (r.Rank, r.UserName)));
    }

    public void Dispose() => _t.Dispose();
}
