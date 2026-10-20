using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class TrainingTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly QuizService _quiz;
    private readonly ResultsService _results;
    private readonly MonthRankingService _ranking;
    private readonly TrainingService _training;
    private readonly string _me;
    private readonly string _other;
    private readonly DateOnly _day;

    public TrainingTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player
            {
                Name = $"Speler {i}",
                Era = i <= 5 ? "classic" : "modern",
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h} van speler {i}")],
            });
        var me = new AppUser { UserName = "sander" };
        var other = new AppUser { UserName = "jan" };
        _db.Users.AddRange(me, other);
        _db.SaveChanges();
        (_me, _other) = (me.Id, other.Id);

        _results = new ResultsService(_db, _clock, new MemoryCache(new MemoryCacheOptions()));
        _ranking = new MonthRankingService(_db, _results);
        _quiz = new QuizService(_db, _clock, _results);
        _training = new TrainingService(_db, _clock, _quiz, _results);
        _day = _quiz.Today;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<string> NameOf(int number) =>
        (await _quiz.GetOrCreateChallengeAsync(_day)).Questions.Single(q => q.Number == number).Player!.Name;

    /// <summary>
    /// Speelt de echte daily van vandaag: de eerste <paramref name="correct"/> vragen juist (enkel op de eerste dag), de rest gepast.
    /// </summary>
    private async Task PlayRealAsync(string userId, int correct)
    {
        var count = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Count;
        for (var n = 1; n <= count; n++)
        {
            await _quiz.StartAsync(userId, n);
            if (n <= correct) await _quiz.GuessAsync(userId, n, await NameOf(n));
            else await _quiz.GiveUpAsync(userId, n);
        }
    }

    /// <summary>Speelt een volledige trainingsrun: de eerste <paramref name="correct"/> vragen juist, de rest gepast.</summary>
    private async Task<int> TrainAsync(string userId, int correct)
    {
        var run = await _training.StartRunAsync(userId, _day);
        var count = (await _quiz.GetOrCreateChallengeAsync(_day)).Questions.Count;
        for (var n = 1; n <= count; n++)
        {
            await _training.StartAsync(userId, run.Id, n);
            if (n <= correct) await _training.GuessAsync(userId, run.Id, n, await NameOf(n));
            else await _training.GiveUpAsync(userId, run.Id, n);
        }
        return run.Id;
    }

    /// <summary>
    /// Naar de volgende dag, waar ik eerst de nieuwe daily afwerk: met maar 15 spelers deelt die altijd spelers met gisteren,
    /// en zolang ik ze niet speelde, blijft de daily van gisteren dicht (zie <see cref="Shared_questions_wait_until_todays_daily_is_played"/>).
    /// </summary>
    private async Task NextDayAsync()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        await PlayRealAsync(_me, 0);
    }

    [Fact]
    public async Task Todays_daily_can_only_be_trained_once_the_day_is_over()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        await Assert.ThrowsAsync<QuizException>(() => _training.StartRunAsync(_me, _day));
        Assert.Empty((await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days);

        await NextDayAsync();
        var run = await _training.StartRunAsync(_me, _day);
        Assert.True(run.Id > 0);
        Assert.Single((await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days);
    }

    [Fact]
    public async Task Days_without_a_daily_are_not_created_afterwards()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        await Assert.ThrowsAsync<QuizException>(() => _training.StartRunAsync(_me, _day));
        Assert.Empty(_db.DailyChallenges);
    }

    [Fact]
    public async Task Training_counts_nowhere()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();
        var monthBefore = (await _ranking.GetAsync(_me, _day.Year, _day.Month)).Entries.Single(e => e.IsMe);
        var xpBefore = (await _results.XpInputsAsync())[_me];

        await TrainAsync(_me, 10);

        var challenge = await _quiz.GetOrCreateChallengeAsync(_day);
        Assert.DoesNotContain(_db.Attempts, a => a.UserId == _me && a.DailyQuestion.DailyChallengeId == challenge.Id);
        var standings = (await _results.DayStandingsAsync([challenge.Id]))[challenge.Id];
        Assert.Equal([_other], standings.Select(s => s.UserId));
        Assert.Equal(monthBefore, (await _ranking.GetAsync(_me, _day.Year, _day.Month)).Entries.Single(e => e.IsMe));
        Assert.Equal(xpBefore, (await _results.XpInputsAsync())[_me]);
    }

    [Fact]
    public async Task Shared_questions_wait_until_todays_daily_is_played()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        _clock.Advance(TimeSpan.FromDays(1));

        // 15 spelers, 10 per dag: de daily van vandaag deelt zeker spelers met die van gisteren.
        Assert.True((await _training.GetDayAsync(_me, _day)).SharesWithToday);
        await Assert.ThrowsAsync<QuizException>(() => _training.StartRunAsync(_me, _day));

        await PlayRealAsync(_me, 0);
        Assert.False((await _training.GetDayAsync(_me, _day)).SharesWithToday);
        Assert.True((await _training.StartRunAsync(_me, _day)).Id > 0);
    }

    [Fact]
    public async Task The_day_turns_at_midnight_in_Brussels()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);

        _clock.Advance(new TimeSpan(11, 59, 0)); // 21:59 UTC = 23:59 in Brussel (zomertijd)
        await Assert.ThrowsAsync<QuizException>(() => _training.StartRunAsync(_me, _day));

        _clock.Advance(TimeSpan.FromMinutes(2)); // 00:01 in Brussel
        await PlayRealAsync(_me, 0);
        Assert.True((await _training.StartRunAsync(_me, _day)).Id > 0);
    }

    [Fact]
    public async Task An_empty_run_is_reused_instead_of_piling_up()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        await NextDayAsync();

        var first = await _training.StartRunAsync(_me, _day);
        Assert.Equal(first.Id, (await _training.StartRunAsync(_me, _day)).Id);

        await _training.StartAsync(_me, first.Id, 1);
        Assert.NotEqual(first.Id, (await _training.StartRunAsync(_me, _day)).Id);
    }

    [Fact]
    public async Task Archive_opens_on_the_month_of_yesterday()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        _clock.Advance(TimeSpan.FromDays(15)); // 1 november: gisteren was nog oktober

        var archive = await _training.GetArchiveAsync(_me);
        Assert.Equal((2026, 10), (archive.Year, archive.Month));
        Assert.Single(archive.Days);
        Assert.False(archive.HasLater);
        Assert.True((await _training.GetArchiveAsync(_me, 2026, 9)).HasLater);
    }

    [Fact]
    public async Task Summary_compares_with_the_real_players_of_that_day()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();
        var run = await TrainAsync(_me, 5);

        var summary = await _training.GetSummaryAsync(_me, run);
        Assert.True(summary.Finished);
        Assert.Equal(5, summary.Correct);
        Assert.Equal(1, summary.WouldBeRank);
        Assert.Equal(2, summary.Players);
        Assert.Null(summary.Real);
    }

    [Fact]
    public async Task Day_shows_the_real_attempt_and_every_run()
    {
        await PlayRealAsync(_me, 4);
        await PlayRealAsync(_other, 6);
        await NextDayAsync();
        var first = await TrainAsync(_me, 7);
        var second = await TrainAsync(_me, 2);
        var unfinished = await _training.StartRunAsync(_me, _day);

        var day = await _training.GetDayAsync(_me, _day);
        Assert.NotNull(day.Real);
        Assert.Equal(4, day.Real.Correct);
        Assert.Equal(2, day.Real.Rank);
        Assert.Equal(2, day.Players);
        Assert.Equal([unfinished.Id, second, first], day.Runs.Select(r => r.Id));
        Assert.False(day.Runs[0].Finished);
        Assert.Equal(first, day.Runs.Single(r => r.IsBest).Id);

        // Tegenover de anderen (zonder je eigen echte poging): met 7 juist was je eerste geweest.
        Assert.Equal(1, (await _training.GetSummaryAsync(_me, first)).WouldBeRank);

        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single();
        Assert.True(archived.Played);
        Assert.Equal(3, archived.Runs);
        Assert.Equal(day.Runs.Single(r => r.IsBest).Points, archived.BestTraining);
    }

    [Fact]
    public async Task Archive_marks_days_you_never_played()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();

        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single();
        Assert.False(archived.Played);
        Assert.Null(archived.Real);
        Assert.Null(archived.BestTraining);
    }

    [Fact]
    public async Task Training_questions_are_played_in_order_and_runs_are_private()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        await NextDayAsync();
        var run = await _training.StartRunAsync(_me, _day);

        await Assert.ThrowsAsync<QuizException>(() => _training.StartAsync(_me, run.Id, 2));
        await Assert.ThrowsAsync<QuizException>(() => _training.StartAsync(_other, run.Id, 1));
        await Assert.ThrowsAsync<QuizException>(() => _training.GetSummaryAsync(_other, run.Id));
    }

    [Fact]
    public async Task Training_uses_the_same_clock_and_points_as_the_daily()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        await NextDayAsync();
        var run = await _training.StartRunAsync(_me, _day);

        var state = await _training.StartAsync(_me, run.Id, 1);
        Assert.Single(state.Hints);
        Assert.Null(state.Result);

        _clock.Advance(TimeSpan.FromSeconds(7));
        await _training.GuessAsync(_me, run.Id, 1, "Iemand anders");
        var answered = await _training.GuessAsync(_me, run.Id, 1, await NameOf(1));
        Assert.True(answered.Result!.Correct);
        Assert.Equal(90 - GameRules.WrongGuessPenalty, answered.Result.Points);

        await _training.StartAsync(_me, run.Id, 2);
        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + 1));
        var late = await _training.GetStateAsync(_me, run.Id, 2);
        Assert.Equal(QuestionStatus.Answered, late.Status);
        Assert.Equal(0, late.Result!.Points);
    }

    private async Task<int> AddSpecialAsync(DateOnly start, DateOnly end)
    {
        var special = new SpecialEvent
        {
            Theme = "eigen", Name = "Weekend", Icon = "🎉", StartDate = start, EndDate = end,
            PlayerIds = [.. _db.Players.Select(p => p.Id)], CreatedAt = _clock.GetUtcNow().UtcDateTime,
        };
        _db.SpecialEvents.Add(special);
        await _db.SaveChangesAsync();
        return special.Id;
    }

    [Fact]
    public async Task Finished_specials_can_be_played_again()
    {
        var id = await AddSpecialAsync(_day, _day.AddDays(1));
        var special = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        foreach (var q in special.Questions.OrderBy(q => q.Number))
        {
            await _quiz.StartAsync(_other, q.Number, id);
            await _quiz.GiveUpAsync(_other, q.Number, id);
        }

        // Zolang de special loopt, telt ze nog voor haar klassement: niet in de academie.
        await Assert.ThrowsAsync<QuizException>(() => _training.StartSpecialRunAsync(_me, id));
        await NextDayAsync();
        await Assert.ThrowsAsync<QuizException>(() => _training.StartSpecialRunAsync(_me, id));
        Assert.DoesNotContain((await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days, d => d.Special is not null);

        await NextDayAsync();
        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single(d => d.Special is not null);
        Assert.Equal(id, archived.Special!.Id);
        Assert.Equal(_day.AddDays(1), archived.Date); // op haar einddatum
        Assert.False(archived.Played);

        var run = await _training.StartSpecialRunAsync(_me, id);
        foreach (var q in special.Questions.OrderBy(q => q.Number))
        {
            await _training.StartAsync(_me, run.Id, q.Number);
            await _training.GuessAsync(_me, run.Id, q.Number, q.Player!.Name);
        }

        var summary = await _training.GetSummaryAsync(_me, run.Id);
        Assert.Equal(id, summary.Special!.Id);
        Assert.Equal(special.Questions.Count, summary.Correct);
        Assert.Equal((1, 2), (summary.WouldBeRank, summary.Players));
        Assert.Equal([_other], (await _results.DayStandingsAsync([special.Id]))[special.Id].Select(s => s.UserId));

        var again = await _training.StartAgainAsync(_me, run.Id);
        Assert.NotEqual(run.Id, again.Id);
        Assert.Equal(2, (await _training.GetSpecialAsync(_me, id)).Runs.Count);
    }

    [Fact]
    public async Task Specials_that_were_never_played_are_not_created_afterwards()
    {
        var id = await AddSpecialAsync(_day.AddDays(-5), _day.AddDays(-3));

        await Assert.ThrowsAsync<QuizException>(() => _training.StartSpecialRunAsync(_me, id));
        Assert.DoesNotContain(_db.DailyChallenges, c => c.SpecialEventId == id);
    }

    [Fact]
    public void Would_be_rank_follows_the_daily_order()
    {
        List<ResultsService.DayStanding> others = [new("a", 500, 5, 0), new("b", 300, 3, 1), new("c", 300, 3, 4)];

        Assert.Equal(1, TrainingService.WouldBeRank(others, 600, 6, 0));
        Assert.Equal(2, TrainingService.WouldBeRank(others, 300, 4, 0)); // meer juist dan b en c
        Assert.Equal(3, TrainingService.WouldBeRank(others, 300, 3, 2)); // minder fout dan c
        Assert.Equal(3, TrainingService.WouldBeRank(others, 300, 3, 1)); // volledig gelijk met b: b was eerder klaar
        Assert.Equal(4, TrainingService.WouldBeRank(others, 0, 0, 0));
    }
}
