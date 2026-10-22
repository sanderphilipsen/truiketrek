using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class QuizServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly ResultsService _results;
    private readonly MonthRankingService _ranking;
    private readonly string _userId;

    public QuizServiceTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        for (var i = 1; i <= 15; i++)
        {
            _db.Players.Add(new Player
            {
                Name = $"Speler {i}",
                Era = i <= 5 ? "classic" : "modern",
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h} van speler {i}")],
            });
        }
        var user = new AppUser { UserName = "sander" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;

        _results = _t.Results;
        _ranking = _t.Ranking;
        _quiz = _t.Quiz;
    }

    private string AddUser(string name) => _t.AddUser(name);

    private async Task<string> NameOfQuestion(int number) =>
        (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Single(q => q.Number == number).Player!.Name;

    [Fact]
    public async Task Daily_challenge_has_ten_questions_and_max_two_classics()
    {
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);

        Assert.Equal(10, challenge.Questions.Count);
        Assert.True(challenge.Questions.Count(q => q.Player!.Era == "classic") <= 2);
        Assert.Equal(challenge.Id, (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Id);
    }

    [Fact]
    public async Task Hints_are_released_over_time()
    {
        var state = await _quiz.StartAsync(_userId, 1);
        Assert.Single(state.Hints);

        _clock.Advance(TimeSpan.FromSeconds(11));
        state = await _quiz.GetStateAsync(_userId, 1);
        Assert.Equal(3, state.Hints.Count);
    }

    [Fact]
    public async Task Correct_guess_scores_points_of_that_moment()
    {
        await _quiz.StartAsync(_userId, 1);
        _clock.Advance(TimeSpan.FromSeconds(7));
        var answered = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));

        Assert.True(answered.Result!.Correct);
        Assert.Equal(90, answered.Result.Points);
        Assert.Equal(5, answered.Result.AllHints.Count);
    }

    [Fact]
    public async Task Wrong_guesses_can_retry_with_penalty()
    {
        await _quiz.StartAsync(_userId, 1);
        var wrong = await _quiz.GuessAsync(_userId, 1, "Iemand anders");
        Assert.Equal(QuestionStatus.InProgress, wrong.Status);
        Assert.Equal("Iemand anders", wrong.LastWrongGuess);

        await _quiz.GuessAsync(_userId, 1, "Nog iemand");
        var right = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));
        Assert.Equal(100 - 2 * GameRules.WrongGuessPenalty, right.Result!.Points);
        Assert.Equal(2, right.Result.WrongGuesses);
    }

    [Fact]
    public async Task Empty_guess_is_not_penalised()
    {
        await _quiz.StartAsync(_userId, 1);
        var state = await _quiz.GuessAsync(_userId, 1, "  ");
        Assert.Equal(0, state.WrongGuesses);
    }

    [Fact]
    public async Task Restarting_does_not_reset_the_clock()
    {
        await _quiz.StartAsync(_userId, 1);
        _clock.Advance(TimeSpan.FromSeconds(16));
        var state = await _quiz.StartAsync(_userId, 1);
        Assert.Equal(4, state.Hints.Count);
    }

    [Fact]
    public async Task Give_up_and_timeout_score_zero()
    {
        await _quiz.StartAsync(_userId, 1);
        var gaveUp = await _quiz.GiveUpAsync(_userId, 1);
        Assert.Equal(0, gaveUp.Result!.Points);

        await _quiz.StartAsync(_userId, 2);
        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + 1));
        var late = await _quiz.GuessAsync(_userId, 2, await NameOfQuestion(2));
        Assert.Equal(QuestionStatus.Answered, late.Status);
        Assert.False(late.Result!.Correct);
        Assert.Equal(0, late.Result.Points);
    }

    // ---------- Karakterisering van de pogingflow (vangnet voor refactorplan stap 1.1) ----------

    [Fact]
    public async Task A_question_stops_after_the_maximum_number_of_guesses()
    {
        await _quiz.StartAsync(_userId, 1);
        QuestionState state = null!;
        for (var i = 1; i < GameRules.MaxGuessesPerQuestion; i++) state = await _quiz.GuessAsync(_userId, 1, $"Fout {i}");
        Assert.Equal(QuestionStatus.InProgress, state.Status);

        var last = await _quiz.GuessAsync(_userId, 1, "Nog eens fout");
        Assert.Equal(QuestionStatus.Answered, last.Status);
        Assert.False(last.Result!.Correct);
        Assert.Equal(0, last.Result.Points);
        Assert.Equal(GameRules.MaxGuessesPerQuestion, last.WrongGuesses);

        var after = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));
        Assert.False(after.Result!.Correct);
        Assert.Null(after.LastWrongGuess);
    }

    [Fact]
    public async Task An_answer_within_the_latency_grace_still_counts()
    {
        await _quiz.StartAsync(_userId, 1);
        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + GameRules.LatencyGraceSeconds - 0.1));
        var inTime = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));
        Assert.True(inTime.Result!.Correct);
        Assert.Equal(GameRules.LastChancePoints, inTime.Result.Points);
    }

    [Fact]
    public async Task After_the_grace_the_question_closes_at_the_time_limit()
    {
        await _quiz.StartAsync(_userId, 1);
        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + GameRules.LatencyGraceSeconds + 0.1));
        var late = await _quiz.GetStateAsync(_userId, 1);
        Assert.Equal(QuestionStatus.Answered, late.Status);
        Assert.False(late.Result!.Correct);
        Assert.Equal(GameRules.TimeLimitSeconds, late.ElapsedSeconds); // afgesloten op de limiet, niet op het moment van kijken
        Assert.Equal(GameRules.HintCount, late.Hints.Count);
        Assert.Null(late.LastWrongGuess);
    }

    [Fact]
    public async Task The_overview_closes_expired_questions_so_the_next_one_can_start()
    {
        await _quiz.StartAsync(_userId, 1);
        var during = await _quiz.GetOverviewAsync(_userId);
        Assert.Equal(QuestionStatus.InProgress, during.Questions[0].Status);
        Assert.Null(during.Questions[0].PlayerName);

        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + 1));
        var overview = await _quiz.GetOverviewAsync(_userId);
        Assert.Equal(QuestionStatus.Answered, overview.Questions[0].Status);
        Assert.Equal(0, overview.Questions[0].Points);
        Assert.Equal(await NameOfQuestion(1), overview.Questions[0].PlayerName);
        Assert.Equal(QuestionStatus.NotStarted, overview.Questions[1].Status);

        var next = await _quiz.StartAsync(_userId, 2);
        Assert.Equal(QuestionStatus.InProgress, next.Status);
    }

    [Fact]
    public async Task A_give_up_sent_together_with_a_guess_is_dropped()
    {
        await _quiz.StartAsync(_userId, 1);

        // Een tweede verzoek dat tegelijk binnenkomt werkt met een oudere kopie van de poging.
        using var other = _t.NewContext();
        var stale = _t.NewQuiz(other);
        await stale.GetStateAsync(_userId, 1);

        await _quiz.GuessAsync(_userId, 1, "Iemand");
        var dropped = await stale.GiveUpAsync(_userId, 1);
        Assert.Equal(QuestionStatus.InProgress, dropped.Status);
        Assert.Equal(1, dropped.WrongGuesses);

        var right = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));
        Assert.Equal(100 - GameRules.WrongGuessPenalty, right.Result!.Points);
    }

    [Fact]
    public async Task Questions_must_be_played_in_order()
    {
        await Assert.ThrowsAsync<QuizException>(() => _quiz.StartAsync(_userId, 2));
    }

    [Fact]
    public async Task Monthly_leaderboard_sums_points()
    {
        await _quiz.StartAsync(_userId, 1);
        await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));

        var board = await _ranking.GetAsync(_userId, 2026, 10);
        var me = Assert.Single(board.Entries);
        Assert.Equal(100, me.Total);
        Assert.Equal(100, me.Daily);
        Assert.Equal(1, me.DaysPlayed);
        Assert.True(me.IsMe);
    }

    [Fact]
    public async Task Everyone_gets_the_same_challenge()
    {
        var other = AddUser("vriend");
        await _quiz.StartAsync(_userId, 1);
        await _quiz.StartAsync(other, 1);

        Assert.Single(await _db.DailyChallenges.ToListAsync());
        var questionIds = await _db.Attempts.Select(a => a.DailyQuestionId).Distinct().ToListAsync();
        Assert.Single(questionIds);
    }

    [Fact]
    public async Task A_finished_question_cannot_be_played_again()
    {
        await _quiz.StartAsync(_userId, 1);
        await _quiz.GiveUpAsync(_userId, 1);

        var again = await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1));
        Assert.False(again.Result!.Correct);
        Assert.Equal(0, again.Result.Points);

        var restarted = await _quiz.StartAsync(_userId, 1);
        Assert.Equal(QuestionStatus.Answered, restarted.Status);
    }

    [Fact]
    public async Task Players_used_longest_ago_come_first()
    {
        foreach (var p in _db.Players) p.Era = "modern";
        await _db.SaveChangesAsync();

        var day1 = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Select(q => q.PlayerId).ToHashSet();
        _clock.Advance(TimeSpan.FromDays(1));
        var day2 = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Select(q => q.PlayerId).ToHashSet();

        // Dag 2 bevat eerst de 5 spelers die nog nooit gevraagd werden, en vult aan met spelers van dag 1.
        var neverUsed = _db.Players.Select(p => p.Id).AsEnumerable().Where(id => !day1.Contains(id)).ToList();
        Assert.All(neverUsed, id => Assert.Contains(id, day2));
        Assert.Equal(5, day2.Intersect(day1).Count());
    }

    [Fact]
    public async Task Daily_has_exactly_one_question_of_one_star_and_one_of_five_stars()
    {
        // Spelers 6-11: iedereen kent ze; 12-15: enkel voor kenners. Daarbij 10 gewone spelers van 3 sterren.
        foreach (var p in _db.Players.AsEnumerable())
        {
            var n = int.Parse(p.Name.Split(' ')[1]);
            p.Difficulty = n is >= 6 and <= 11 ? 1 : n >= 12 ? 5 : 3;
        }
        for (var i = 16; i <= 25; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Era = "modern", Difficulty = 3, Hints = ["Tip"] });
        await _db.SaveChangesAsync();

        for (var day = 0; day < 3; day++)
        {
            var questions = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions;
            Assert.Equal(10, questions.Count);
            Assert.Single(questions, q => q.Player!.Difficulty == 1);
            Assert.Single(questions, q => q.Player!.Difficulty == 5);
            Assert.True(questions.Count(q => q.Player!.Era == "classic") <= 2);
            _clock.Advance(TimeSpan.FromDays(1));
        }
    }

    [Fact]
    public async Task Daily_follows_the_difficulty_plan_when_every_level_is_available()
    {
        foreach (var p in _db.Players) p.Era = "modern";
        for (var i = 16; i <= 40; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Era = "modern", Difficulty = i % 5 + 1, Hints = ["Tip"] });
        await _db.SaveChangesAsync();

        var questions = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions;

        Assert.Equal(Difficulty.Plan(10), questions.Select(q => q.Player!.Difficulty).Order().ToList());
    }

    [Fact]
    public async Task Day_podium_gets_bonus_in_monthly_leaderboard()
    {
        var other = AddUser("vriend");
        await _quiz.StartAsync(_userId, 1);
        await _quiz.StartAsync(other, 1);
        await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1)); // 100
        _clock.Advance(TimeSpan.FromSeconds(6));
        await _quiz.GuessAsync(other, 1, await NameOfQuestion(1));   // 90

        // Vandaag is er nog geen winnaar.
        var during = await _ranking.GetAsync(_userId, 2026, 10);
        Assert.Equal(100, during.Entries.Single(e => e.IsMe).Total);

        _clock.Advance(TimeSpan.FromDays(1));
        var board = await _ranking.GetAsync(_userId, 2026, 10);
        var me = board.Entries.Single(e => e.IsMe);
        Assert.Equal(100 + MonthRules.PodiumBonus[0], me.Total);
        Assert.Equal(1, me.DaysWon);
        var friend = board.Entries.Single(e => !e.IsMe);
        Assert.Equal(90 + MonthRules.PodiumBonus[1], friend.Total);
        Assert.Equal(2, friend.Rank);

        var winner = await _results.GetLastWinnerAsync(other);
        Assert.Equal("sander", winner!.UserName);
        Assert.False(winner.IsMe);

        var stats = await _results.GetStatsAsync(_userId);
        Assert.Contains(stats.HallOfFame, c => c.Title == "Meeste dagzeges" && c.IsMe);
        Assert.Contains(stats.HallOfFame, c => c.Title == "Eeuwige tweede" && c.UserName == "vriend");
        Assert.Equal(1, stats.Me.DaysWon);
        Assert.Equal(1, stats.Me.CurrentStreak);
    }

    [Fact]
    public async Task Tie_is_broken_by_fewer_wrong_guesses()
    {
        var other = AddUser("vriend");
        await _quiz.StartAsync(_userId, 1);
        await _quiz.StartAsync(other, 1);
        await _quiz.GuessAsync(_userId, 1, "fout");
        _clock.Advance(TimeSpan.FromSeconds(6));
        await _quiz.GuessAsync(_userId, 1, await NameOfQuestion(1)); // 90 - 5 = 85
        _clock.Advance(TimeSpan.FromSeconds(5));
        await _quiz.GuessAsync(other, 1, "fout");
        await _quiz.GuessAsync(other, 1, "fout");
        await _quiz.GuessAsync(other, 1, await NameOfQuestion(1));   // 80 - 10 = 70

        _clock.Advance(TimeSpan.FromDays(1));
        var winner = await _results.GetLastWinnerAsync(_userId);
        Assert.True(winner!.IsMe);
        Assert.Equal(85, winner.Points);
    }

    [Fact]
    public async Task Xp_and_level_grow_with_play()
    {
        Assert.Equal(1, (await _results.LevelOfAsync(_userId)).Level);

        for (var n = 1; n <= GameRules.QuestionsPerDay; n++)
        {
            await _quiz.StartAsync(_userId, n);
            await _quiz.GuessAsync(_userId, n, await NameOfQuestion(n));
        }
        _clock.Advance(TimeSpan.FromDays(1));

        // 10 vragen (100) + 1000 punten / 5 (200) + volledige dag (50) + perfecte dag (50) + dagzege (100) = 500 XP
        var level = await _results.LevelOfAsync(_userId);
        Assert.Equal(500, level.Xp);
        Assert.Equal(3, level.Level);
    }

    [Fact]
    public async Task Announced_update_blocks_new_questions_but_lets_you_finish_the_current_one()
    {
        var other = AddUser("jan");
        await _quiz.StartAsync(other, 1);
        _t.AnnounceUpdate(_clock.GetUtcNow().AddMinutes(3));

        var ex = await Assert.ThrowsAsync<QuizException>(() => _quiz.StartAsync(_userId, 1));
        Assert.Equal(MaintenanceNotice.BlockedMessage, ex.Message);

        // De gestarte vraag herladen en afwerken lukt; de volgende vraag start pas na de update.
        await _quiz.StartAsync(other, 1);
        Assert.Equal(QuestionStatus.Answered, (await _quiz.GiveUpAsync(other, 1)).Status);
        await Assert.ThrowsAsync<QuizException>(() => _quiz.StartAsync(other, 2));
    }

    [Fact]
    public async Task Forgotten_update_notice_expires()
    {
        _t.AnnounceUpdate(_clock.GetUtcNow().AddMinutes(3));
        Assert.NotNull(_t.Maintenance.UpdateAt());

        _clock.Advance(TimeSpan.FromMinutes(3) + MaintenanceNotice.MaxAge + TimeSpan.FromSeconds(1));
        Assert.Null(_t.Maintenance.UpdateAt());
        await _quiz.StartAsync(_userId, 1);
    }

    public void Dispose() => _t.Dispose();
}
