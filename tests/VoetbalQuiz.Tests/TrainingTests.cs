using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class TrainingTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly ResultsService _results;
    private readonly MonthRankingService _ranking;
    private readonly TrainingService _training;
    private readonly string _me;
    private readonly string _other;
    private readonly DateOnly _day;

    public TrainingTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

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

        _results = _t.Results;
        _ranking = _t.Ranking;
        _quiz = _t.Quiz;
        _training = _t.Training;
        _day = _quiz.Today;
    }

    public void Dispose() => _t.Dispose();

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
        Assert.Equal(2, archived.Runs); // de lege run (geen vraag gestart) telt niet

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

        _clock.Advance(TimeSpan.FromSeconds(GameRules.HintIntervalSeconds + 2));
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
        Assert.Equal(_day, archived.Date); // op haar startdatum, zoals in het maandklassement
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

    [Fact]
    public async Task Would_be_rank_matches_the_real_day_ranking()
    {
        var challenge = await _quiz.GetOrCreateChallengeAsync(_day);
        var (q1, q2) = (challenge.Questions.Single(q => q.Number == 1), challenge.Questions.Single(q => q.Number == 2));
        var start = _clock.GetUtcNow().UtcDateTime;
        void Play(string user, DailyQuestion q, int points, bool correct, int wrong, int second) =>
            _db.Attempts.Add(new Attempt
            {
                UserId = user, DailyQuestionId = q.Id, StartedAt = start, AnsweredAt = start.AddSeconds(second),
                Points = points, Correct = correct, WrongGuesses = wrong,
            });

        // Gelijke punten: eerst meer juist, dan minder fout. Wie het eerst klaar was, beslist pas daarna.
        var (a, b, c, d) = (_t.AddUser("a"), _t.AddUser("b"), _t.AddUser("c"), _t.AddUser("d"));
        Play(a, q1, 100, true, 0, 9); Play(a, q2, 50, true, 0, 9);  // 150, 2 juist
        Play(b, q1, 150, true, 0, 1);                               // 150, 1 juist, 0 fout
        Play(c, q1, 100, true, 3, 2); Play(c, q2, 50, false, 0, 2); // 150, 1 juist, 3 fout
        Play(d, q1, 90, true, 0, 1);
        await _db.SaveChangesAsync();

        var standings = (await _results.DayStandingsAsync([challenge.Id]))[challenge.Id];
        Assert.Equal([a, b, c, d], standings.Select(s => s.UserId));
        for (var i = 0; i < standings.Count; i++)
        {
            var s = standings[i];
            Assert.Equal(i + 1, TrainingService.WouldBeRank(standings.Where(o => o != s), s.Points, s.Correct, s.Wrong));
        }
        // Volledig gelijk met b: in de academie ben je later klaar dan wie echt speelde.
        Assert.Equal(3, TrainingService.WouldBeRank(standings, 150, 1, 0));
    }

    // ---------- Nagespeeld: je eerste keer in de academie komt in het klassement van die dag (backlog 037) ----------

    [Fact]
    public async Task A_replay_shows_in_the_day_board_but_never_changes_winner_podium_month_or_xp()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();
        var challenge = await _quiz.GetOrCreateChallengeAsync(_day);
        await _results.SettleFinishedDaysAsync();
        var monthBefore = (await _ranking.GetAsync(_me, _day.Year, _day.Month)).Entries.Single(e => e.IsMe);
        var xpBefore = (await _results.XpInputsAsync())[_me];
        var podiumBefore = (await _results.PodiumsAsync(_day, _day.AddDays(1))).Single(p => p.ChallengeId == challenge.Id).UserIds;

        await TrainAsync(_me, 10);

        // In het klassement: bovenaan, maar zonder eigen plaats; jan blijft 1ste.
        var board = await _training.GetDayBoardAsync(_me, _day);
        Assert.Equal([("sander", 1, true), ("jan", 1, false)], board.Entries.Select(e => (e.UserName, e.Rank, e.Replayed)));
        Assert.True(board.Entries[0].IsMe);

        // Nergens anders.
        await _db.Entry(challenge).ReloadAsync();
        Assert.Equal(_other, challenge.WinnerUserId);
        Assert.Equal(podiumBefore, (await _results.PodiumsAsync(_day, _day.AddDays(1))).Single(p => p.ChallengeId == challenge.Id).UserIds);
        Assert.Equal([_other], (await _results.DayStandingsAsync([challenge.Id]))[challenge.Id].Select(s => s.UserId));
        Assert.Equal(monthBefore, (await _ranking.GetAsync(_me, _day.Year, _day.Month)).Entries.Single(e => e.IsMe));
        Assert.Equal(xpBefore, (await _results.XpInputsAsync())[_me]);
    }

    [Fact]
    public async Task Real_players_keep_their_place_and_replays_slot_in_between()
    {
        var third = _t.AddUser("piet");
        await PlayRealAsync(_other, 6);
        await PlayRealAsync(third, 2);
        await NextDayAsync();

        await TrainAsync(_me, 4);

        var board = await _training.GetDayBoardAsync(_me, _day);
        Assert.Equal([("jan", 1, false), ("sander", 2, true), ("piet", 2, false)], board.Entries.Select(e => (e.UserName, e.Rank, e.Replayed)));
    }

    [Fact]
    public async Task Only_the_first_run_counts_even_unfinished_and_never_after_a_real_attempt()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();

        // Eerste run: één vraag juist, dan gestopt. Die telt, met de punten tot dan.
        var first = await _training.StartRunAsync(_me, _day);
        await _training.StartAsync(_me, first.Id, 1);
        await _training.GuessAsync(_me, first.Id, 1, await NameOf(1));
        var second = await TrainAsync(_me, 10);

        var firstSummary = await _training.GetSummaryAsync(_me, first.Id);
        var day = await _training.GetDayAsync(_me, _day);
        Assert.NotNull(day.Replayed);
        Assert.Equal(firstSummary.Points, day.Replayed.Points);
        Assert.Equal(1, day.Replayed.Correct);
        Assert.Equal(2, day.Replayed.Players);
        Assert.True(firstSummary.CountsForBoard);
        Assert.False((await _training.GetSummaryAsync(_me, second)).CountsForBoard);

        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single();
        Assert.Equal(day.Replayed, archived.Replayed);
        Assert.False(archived.Missed);

        // Jan speelde echt: zijn runs komen er nooit bij.
        await PlayRealAsync(_other, 0); // de daily van vandaag, anders blijft gisteren dicht
        await TrainAsync(_other, 10);
        var board = await _training.GetDayBoardAsync(_me, _day);
        Assert.Equal([("jan", false), ("sander", true)], board.Entries.Select(e => (e.UserName, e.Replayed)));
        Assert.False((await _training.GetDayAsync(_other, _day)).Replayed is not null);
    }

    [Fact]
    public async Task Archive_suggests_the_most_recent_missed_day_then_nothing()
    {
        var day0 = _day;
        await _quiz.GetOrCreateChallengeAsync(day0);
        _clock.Advance(TimeSpan.FromDays(1));
        var day1 = _quiz.Today;
        await _quiz.GetOrCreateChallengeAsync(day1);
        _clock.Advance(TimeSpan.FromDays(1));
        await PlayRealAsync(_me, 0);

        var archive = await _training.GetArchiveAsync(_me);
        Assert.Equal((day1, false), (archive.Suggestion!.Day.Date, archive.Suggestion.Revenge));
        Assert.True(archive.Suggestion.Day.Missed);

        var run = await _training.StartRunAsync(_me, day1);
        await _training.StartAsync(_me, run.Id, 1);
        Assert.Equal(day0, (await _training.GetArchiveAsync(_me)).Suggestion!.Day.Date);

        run = await _training.StartRunAsync(_me, day0);
        await _training.StartAsync(_me, run.Id, 1);
        Assert.Null((await _training.GetArchiveAsync(_me)).Suggestion);
    }

    [Fact]
    public async Task Archive_suggests_a_revenge_when_nothing_is_missed()
    {
        await PlayRealAsync(_other, 5);
        await PlayRealAsync(_me, 1);
        await NextDayAsync();

        var suggestion = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Suggestion;
        Assert.NotNull(suggestion);
        Assert.True(suggestion.Revenge);
        Assert.Equal(_day, suggestion.Day.Date);
        Assert.Equal(2, suggestion.Day.Real!.Rank);
    }

    [Fact]
    public async Task Archive_marks_blocked_days_and_skips_them_as_suggestion()
    {
        await _quiz.GetOrCreateChallengeAsync(_day);
        _clock.Advance(TimeSpan.FromDays(1));

        var archive = await _training.GetArchiveAsync(_me, _day.Year, _day.Month);
        Assert.True(archive.Days.Single().Blocked);
        Assert.Null(archive.Suggestion);

        await PlayRealAsync(_me, 0);
        archive = await _training.GetArchiveAsync(_me, _day.Year, _day.Month);
        Assert.False(archive.Days.Single().Blocked);
        Assert.Equal(_day, archive.Suggestion!.Day.Date);
    }

    [Fact]
    public async Task Days_before_you_registered_are_not_missed()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();
        var me = await _db.Users.FindAsync(_me);
        me!.CreatedAt = _clock.GetUtcNow().UtcDateTime; // ingeschreven vandaag, de dag na de daily
        await _db.SaveChangesAsync();

        var archive = await _training.GetArchiveAsync(_me, _day.Year, _day.Month);
        var day = archive.Days.Single();
        Assert.True(day.BeforeYourTime);
        Assert.False(day.Missed);
        Assert.Null(archive.Suggestion);
        Assert.True((await _training.StartRunAsync(_me, _day)).Id > 0); // wel speelbaar
    }

    [Fact]
    public async Task A_replay_with_the_same_score_comes_after_the_real_player()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();

        await TrainAsync(_me, 3); // zelfde punten, juist en fout als jan

        var board = await _training.GetDayBoardAsync(_me, _day);
        Assert.Equal(board.Entries[0].Points, board.Entries[1].Points);
        Assert.Equal([("jan", 1, false), ("sander", 2, true)], board.Entries.Select(e => (e.UserName, e.Rank, e.Replayed)));
    }

    [Fact]
    public async Task The_board_of_today_is_not_available_in_the_academy()
    {
        await PlayRealAsync(_other, 3);
        await Assert.ThrowsAsync<QuizException>(() => _training.GetDayBoardAsync(_me, _day));
        await Assert.ThrowsAsync<QuizException>(() => _training.GetDayBoardAsync(_me, _day.AddDays(1)));
    }

    [Fact]
    public async Task An_empty_run_does_not_count_as_played_and_the_next_real_run_counts()
    {
        await PlayRealAsync(_other, 3);
        await NextDayAsync();

        await _training.StartRunAsync(_me, _day); // op "Spelen" getikt, geen vraag gestart
        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single();
        Assert.Equal(0, archived.Runs);
        Assert.True(archived.Missed);

        var run = await TrainAsync(_me, 2);
        Assert.True((await _training.GetSummaryAsync(_me, run)).CountsForBoard);
    }

    [Fact]
    public async Task A_real_daily_started_but_never_answered_is_not_missed_and_never_replayed()
    {
        await PlayRealAsync(_other, 3);
        await _quiz.StartAsync(_me, 1); // vlak voor middernacht begonnen, de tips gezien
        await NextDayAsync();

        var archived = (await _training.GetArchiveAsync(_me, _day.Year, _day.Month)).Days.Single();
        Assert.True(archived.Started);
        Assert.False(archived.Missed);
        Assert.Null(archived.Real);

        await TrainAsync(_me, 10);
        Assert.DoesNotContain((await _training.GetDayBoardAsync(_me, _day)).Entries, e => e.Replayed);
    }

    [Fact]
    public async Task A_special_board_shows_replays_after_it_ended_but_not_when_it_runs_again()
    {
        var id = await AddSpecialAsync(_day, _day);
        var special = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        foreach (var q in special.Questions.OrderBy(q => q.Number))
        {
            await _quiz.StartAsync(_other, q.Number, id);
            await _quiz.GuessAsync(_other, q.Number, q.Player!.Name, id);
        }
        await NextDayAsync();
        await _results.SettleFinishedDaysAsync();

        var run = await _training.StartSpecialRunAsync(_me, id);
        foreach (var q in special.Questions.OrderBy(q => q.Number))
        {
            await _training.StartAsync(_me, run.Id, q.Number);
            await _training.GiveUpAsync(_me, run.Id, q.Number);
        }

        var board = await _results.GetSpecialLeaderboardAsync(_me, id);
        Assert.Equal([("jan", 1, false), ("sander", 2, true)], board.Entries.Select(e => (e.UserName, e.Rank, e.Replayed)));
        await _db.Entry(special).ReloadAsync();
        Assert.Equal(_other, special.WinnerUserId);
        Assert.Equal([_other], (await _results.PodiumsAsync(_day, _day.AddDays(1))).Single(p => p.ChallengeId == special.Id).UserIds);

        // Een admin verlengt de special: zolang ze weer loopt, geen nagespeelde rijen (ook niet op het podium op Home).
        var ev = await _db.SpecialEvents.FindAsync(id);
        ev!.EndDate = _quiz.Today.AddDays(3);
        await _db.SaveChangesAsync();
        Assert.DoesNotContain((await _results.GetSpecialLeaderboardAsync(_me, id)).Entries, e => e.Replayed);
    }
}
