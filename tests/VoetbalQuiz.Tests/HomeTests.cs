using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>Wat Home van de server krijgt: je plaats van vandaag (en of je steeg), de startdag van een special en "Verder met vraag N".</summary>
public sealed class HomeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;
    // Zaterdag 17 oktober, 12:00 in België.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly ResultsService _results;
    private readonly QuizService _quiz;
    private readonly AdminService _admin;
    private readonly string _me, _tom;

    public HomeTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        for (var i = 1; i <= 30; i++)
        {
            _db.Players.Add(new Player
            {
                Name = $"Speler {i}",
                Status = PlayerStatus.Approved,
                Tags = ["halloween"], // genoeg voor een special met counter (3 × 10 spelers)
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")],
                Sources = ["https://example.org"],
            });
        }
        _me = AddUser("sander");
        _tom = AddUser("tom");

        _results = new ResultsService(_db, _clock, new MemoryCache(new MemoryCacheOptions()));
        _quiz = new QuizService(_db, _clock, _results);
        _admin = new AdminService(_db, _quiz);
    }

    private string AddUser(string name)
    {
        var user = new AppUser { UserName = name };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }

    /// <summary>Speelt de vragen <paramref name="from"/> tot en met <paramref name="to"/> van de daily.</summary>
    private async Task PlayAsync(string userId, int from, int to, bool correct)
    {
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        foreach (var q in challenge.Questions.Where(q => q.Number >= from && q.Number <= to).OrderBy(q => q.Number))
        {
            await _quiz.StartAsync(userId, q.Number);
            if (correct) await _quiz.GuessAsync(userId, q.Number, q.Player!.Name);
            else await _quiz.GiveUpAsync(userId, q.Number);
        }
    }

    [Fact]
    public async Task No_rank_before_you_answered_a_question_today()
    {
        await PlayAsync(_tom, 1, 1, correct: true);

        var rank = await _quiz.VisitDailyRankAsync(_me);

        Assert.Null(rank.Rank);
        Assert.Equal(1, rank.Players);
        Assert.Null(rank.PreviousRank);
        Assert.Equal(0, rank.RisenBy);
    }

    [Fact]
    public async Task Rank_matches_the_daily_leaderboard_and_remembers_the_previous_visit()
    {
        await PlayAsync(_tom, 1, 1, correct: true);   // tom: ±100 punten
        await PlayAsync(_me, 1, 1, correct: false);   // ik: 0 punten

        var first = await _quiz.VisitDailyRankAsync(_me);
        Assert.Equal(2, first.Rank);
        Assert.Equal(2, first.Players);
        Assert.Null(first.PreviousRank); // eerste bezoek vandaag: niets om mee te vergelijken

        await PlayAsync(_me, 2, 10, correct: true);   // ik haal tom in

        var second = await _quiz.VisitDailyRankAsync(_me);
        Assert.Equal(1, second.Rank);
        Assert.Equal(2, second.PreviousRank);
        Assert.Equal(1, second.RisenBy);
        var board = await _results.GetDailyLeaderboardAsync(_me);
        Assert.Equal(second.Rank, board.Entries.Single(e => e.IsMe).Rank);

        // Het oplichten gebeurt maar één keer: bij het volgende bezoek is de vorige plaats al de huidige.
        var third = await _quiz.VisitDailyRankAsync(_me);
        Assert.Equal(1, third.PreviousRank);
        Assert.Equal(0, third.RisenBy);

        // Wie daalt, krijgt geen pijltje.
        var tom = await _quiz.VisitDailyRankAsync(_tom);
        Assert.Equal(2, tom.Rank);
        Assert.Equal(0, tom.RisenBy);
    }

    [Fact]
    public async Task Previous_rank_of_yesterday_does_not_count()
    {
        await PlayAsync(_me, 1, 1, correct: true);
        Assert.Equal(1, (await _quiz.VisitDailyRankAsync(_me)).Rank);

        _clock.Advance(TimeSpan.FromDays(1));

        var today = await _quiz.VisitDailyRankAsync(_me);
        Assert.Null(today.Rank);
        Assert.Null(today.PreviousRank);
        Assert.Equal(0, today.Players);
    }

    [Fact]
    public async Task Rank_ignores_specials()
    {
        var id = await _admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Halloween", "🎃", "", _quiz.Today, _quiz.Today, "halloween", []));
        var special = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        await _quiz.StartAsync(_me, 1, id);
        await _quiz.GuessAsync(_me, 1, special.Questions.Single(q => q.Number == 1).Player!.Name, id);

        var rank = await _quiz.VisitDailyRankAsync(_me);
        Assert.Null(rank.Rank);
        Assert.Equal(0, rank.Players);
    }

    [Fact]
    public async Task Start_day_of_a_special_follows_the_Brussels_date()
    {
        // 17 oktober 23:30 UTC is al 18 oktober in België (zomertijd, +2).
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 17, 22, 30, 0, TimeSpan.Zero));
        var start = new DateOnly(2026, 10, 18);
        var id = await _admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Halloween", "🎃", "", start, start.AddDays(3), "halloween", []));

        var firstDay = Assert.Single(await _results.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.True(firstDay.IsActive);
        Assert.True(firstDay.StartsToday);
        Assert.Equal(3, firstDay.DaysLeft);

        _clock.Advance(TimeSpan.FromDays(1));
        var secondDay = Assert.Single(await _results.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.True(secondDay.IsActive);
        Assert.False(secondDay.StartsToday);
        Assert.Equal(2, secondDay.DaysLeft);
    }

    [Fact]
    public async Task Upcoming_special_does_not_start_today()
    {
        var id = await _admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Later", "🎃", "", _quiz.Today.AddDays(2), _quiz.Today.AddDays(2), "halloween", []));

        var special = Assert.Single(await _results.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.True(special.IsUpcoming);
        Assert.False(special.StartsToday);
        Assert.Equal(0, special.DaysLeft);
        Assert.Equal(2, special.StartsIn);
    }

    [Fact]
    public async Task An_opened_question_shows_as_in_progress_so_Home_can_say_continue()
    {
        await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        await _quiz.StartAsync(_me, 1);

        var overview = await _quiz.GetOverviewAsync(_me);
        Assert.Equal(QuestionStatus.InProgress, overview.Questions.Single(q => q.Number == 1).Status);
        Assert.Equal(0, overview.Questions.Count(q => q.Status == QuestionStatus.Answered));
    }

    [Fact]
    public async Task Expired_questions_are_closed_before_the_rank_is_taken()
    {
        await PlayAsync(_tom, 1, 1, correct: false); // tom: 0 punten, klaar om 12:00
        await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        await _quiz.StartAsync(_me, 1);              // ik open vraag 1 en laat de tijd verlopen
        _clock.Advance(TimeSpan.FromMinutes(5));

        // Zonder overzicht ervoor (Home laadt beide tegelijk): de verlopen vraag telt toch al mee.
        var rank = await _quiz.VisitDailyRankAsync(_me);
        Assert.Equal(2, rank.Players);
        Assert.NotNull(rank.Rank);
        Assert.Equal(_quiz.Today, rank.Date);
        var attempt = await _db.Attempts.SingleAsync(a => a.UserId == _me);
        Assert.NotNull(attempt.AnsweredAt);
    }

    [Fact]
    public async Task Special_reports_its_real_question_count_started_and_finished()
    {
        // Wie ben ik? + Counter: een counter telt voor 200 punten, dus de reeks heeft minder dan 10 vragen.
        var id = await _admin.SaveSpecialAsync(new SpecialEdit(0, "halloween", "Halloween", "🎃", "", _quiz.Today, _quiz.Today,
            "halloween", [], [QuestionType.WieBenIk, QuestionType.Counter]));
        var expected = QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Counter]).Values.Sum();
        Assert.True(expected < GameRules.QuestionsPerDay);

        // Nog geen reeks: het aantal volgt uit de mix.
        var before = Assert.Single(await _results.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.Equal(expected, before.QuestionCount);
        Assert.False(before.Started);
        Assert.False(before.Finished);

        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        Assert.Equal(expected, challenge.Questions.Count);

        // Eén vraag geopend maar niet beantwoord: begonnen, niet klaar.
        await _quiz.StartAsync(_me, 1, id);
        var opened = Assert.Single(await _quiz.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.True(opened.Started);
        Assert.Equal(0, opened.Answered);
        Assert.Equal(expected, opened.QuestionCount);

        foreach (var q in challenge.Questions.OrderBy(q => q.Number))
        {
            await _quiz.StartAsync(_me, q.Number, id);
            await _quiz.GiveUpAsync(_me, q.Number, id);
        }
        var done = Assert.Single(await _quiz.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.Equal(expected, done.Answered);
        Assert.True(done.Finished);

        // Tom opende niets.
        Assert.False(Assert.Single(await _results.GetSpecialsAsync(_tom), s => s.Id == id).Started);
    }

    [Fact]
    public async Task Days_left_is_zero_on_the_last_day()
    {
        var id = await _admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Halloween", "🎃", "", _quiz.Today.AddDays(-2), _quiz.Today, "halloween", []));

        var special = Assert.Single(await _results.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.True(special.IsActive);
        Assert.False(special.StartsToday);
        Assert.Equal(0, special.DaysLeft);
    }

    [Theory]
    [InlineData(1, "ste")] [InlineData(2, "de")] [InlineData(8, "ste")] [InlineData(19, "de")] [InlineData(20, "ste")]
    [InlineData(100, "ste")] [InlineData(101, "ste")] [InlineData(102, "de")] [InlineData(108, "ste")] [InlineData(120, "ste")]
    public void Ordinal_suffix_follows_Dutch_spelling(int n, string suffix) => Assert.Equal(suffix, DutchDates.OrdinalSuffix(n));

    [Fact]
    public void Only_level_one_is_a_newcomer()
    {
        Assert.True(Levels.IsNewcomer(1));
        Assert.False(Levels.IsNewcomer(2));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
