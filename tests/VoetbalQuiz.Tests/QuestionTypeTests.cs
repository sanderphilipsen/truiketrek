using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class QuestionTypeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;

    // 17 oktober 2026 is een zaterdag.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly QuizService _quiz;
    private readonly AdminService _admin;
    private readonly string _me;

    public QuestionTypeTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });

        for (var i = 0; i < 12; i++)
        {
            Add(QuestionType.Snelvuur, $"V{i}", new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], 2));
            Add(QuestionType.Jaar, $"J{i}", new JaarData($"Gebeurtenis {i}", 2000 + i));
            Add(QuestionType.Carriere, $"Speler C{i}", new CarriereData($"Speler C{i}", [], ["Club 1 (2001–2003)", "Club 2 (2003–2006)", "Club 3 (2006–2010)", "Club 4 (2010–2014)"]));
            Add(QuestionType.Emoji, $"E{i}", new EmojiData("⚽🇧🇪", $"Emoji Speler {i}", [], "speler", "uitleg"));
            Add(QuestionType.Opstelling, $"O{i}", new OpstellingData($"Finale {i}", "Ploeg",
                [.. Enumerable.Range(0, 11).Select(n => new LineupPlayer($"Lid{(char)('A' + n)} Ploeg{i}", [], n == 0 ? "K" : "V"))]));
        }

        var user = new AppUser { UserName = "sander" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _me = user.Id;

        var results = new ResultsService(_db, _clock, new MemoryCache(new MemoryCacheOptions()));
        _quiz = new QuizService(_db, _clock, results);
        _admin = new AdminService(_db, _quiz);
    }

    private void Add<T>(QuestionType type, string key, T data) =>
        _db.QuizItems.Add(new QuizItem { Type = type, Key = key, Data = QuestionEngine.Serialize(data), Status = PlayerStatus.Approved });

    private async Task PlanTodayAsync(QuestionType type) =>
        await _admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, type)]);

    private async Task<DailyQuestion> FirstQuestionAsync() =>
        (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Single(q => q.Number == 1);

    [Fact]
    public async Task Daily_follows_the_weekday_schedule()
    {
        await PlanTodayAsync(QuestionType.Snelvuur);
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.Equal(10, challenge.Questions.Count);
        Assert.All(challenge.Questions, q => Assert.Equal(QuestionType.Snelvuur, q.Type));
    }

    [Fact]
    public async Task Daily_falls_back_to_who_am_i_when_the_bank_is_too_small()
    {
        await PlanTodayAsync(QuestionType.NietBij); // geen enkele niet-bij-vraag in de bank
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.All(challenge.Questions, q => Assert.Equal(QuestionType.WieBenIk, q.Type));
    }

    [Fact]
    public async Task Snelvuur_rewards_speed_and_ends_after_one_choice()
    {
        await PlanTodayAsync(QuestionType.Snelvuur);
        var state = await _quiz.StartAsync(_me, 1);
        Assert.Equal(4, state.View!.Options!.Count);
        Assert.Null(state.Result);

        var fast = await _quiz.GuessAsync(_me, 1, "2");
        Assert.True(fast.Result!.Correct);
        Assert.Equal(100, fast.Result.Points);

        await _quiz.StartAsync(_me, 2);
        _clock.Advance(TimeSpan.FromSeconds(3));
        var wrong = await _quiz.GuessAsync(_me, 2, "0");
        Assert.False(wrong.Result!.Correct);
        Assert.Equal(0, wrong.Result.Points);
        Assert.Equal(2, wrong.Result.CorrectIndex);
    }

    [Fact]
    public async Task Jaar_gives_points_for_being_close()
    {
        await PlanTodayAsync(QuestionType.Jaar);
        var year = QuestionEngine.Parse<JaarData>((await FirstQuestionAsync()).QuizItem!.Data).Year;
        await _quiz.StartAsync(_me, 1);
        var state = await _quiz.GuessAsync(_me, 1, (year + 1).ToString());
        Assert.Equal(75, state.Result!.Points);
        Assert.Equal(year, state.Result.CorrectYear);
    }

    [Fact]
    public async Task Carriere_reveals_clubs_and_costs_points_per_club()
    {
        await PlanTodayAsync(QuestionType.Carriere);
        var answer = QuestionEngine.Parse<CarriereData>((await FirstQuestionAsync()).QuizItem!.Data).Answer;
        var start = await _quiz.StartAsync(_me, 1);
        Assert.Single(start.Hints);

        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.CarriereClubSeconds + 1));
        var later = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(2, later.Hints.Count);

        var done = await _quiz.GuessAsync(_me, 1, answer);
        Assert.Equal(90, done.Result!.Points);
    }

    [Fact]
    public async Task Opstelling_gives_partial_points_when_time_runs_out()
    {
        await PlanTodayAsync(QuestionType.Opstelling);
        var q = await FirstQuestionAsync();
        var data = QuestionEngine.Parse<OpstellingData>(q.QuizItem!.Data);
        Assert.Equal(QuestionRules.OpstellingBlanks, q.Blanks.Count);
        Assert.DoesNotContain(0, q.Blanks); // de keeper blijft zichtbaar

        await _quiz.StartAsync(_me, 1);
        await _quiz.GuessAsync(_me, 1, data.Players[q.Blanks[0]].Name);
        await _quiz.GuessAsync(_me, 1, data.Players[q.Blanks[1]].Name);
        var notThere = await _quiz.GuessAsync(_me, 1, "Iemand Anders");
        Assert.Equal(0, notThere.WrongGuesses); // geen straf in een opstelling

        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.OpstellingSeconds + 1));
        var end = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(QuestionRules.OpstellingPoints(2, QuestionRules.OpstellingBlanks), end.Result!.Points);
        Assert.Equal(11, end.Result.FullLineup!.Count);
    }

    [Fact]
    public async Task Emoji_allows_retries_with_penalty()
    {
        await PlanTodayAsync(QuestionType.Emoji);
        var answer = QuestionEngine.Parse<EmojiData>((await FirstQuestionAsync()).QuizItem!.Data).Answer;
        await _quiz.StartAsync(_me, 1);
        var wrong = await _quiz.GuessAsync(_me, 1, "Iemand");
        Assert.Equal(QuestionStatus.InProgress, wrong.Status);
        var right = await _quiz.GuessAsync(_me, 1, answer);
        Assert.Equal(100 - GameRules.WrongGuessPenalty, right.Result!.Points);
    }

    [Fact]
    public async Task Same_emoji_answer_never_appears_twice_on_one_day()
    {
        // Extra raadsels over dezelfde speler (bv. een feit- en een rebus-raadsel).
        for (var i = 0; i < 6; i++) Add(QuestionType.Emoji, $"dubbel{i}", new EmojiData($"🔭👅{i}", "Emoji Speler 0", [], "speler", ""));
        await _db.SaveChangesAsync();
        await PlanTodayAsync(QuestionType.Emoji);

        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        var answers = challenge.Questions.Select(q => QuestionEngine.Parse<EmojiData>(q.QuizItem!.Data).Answer).ToList();
        Assert.Equal(answers.Count, answers.Distinct().Count());
    }

    [Fact]
    public async Task Special_mixes_question_types()
    {
        var id = await _admin.SaveSpecialAsync(new SpecialEdit(0, "custom", "Mix", "✨", "", _quiz.Today, _quiz.Today, null,
            [.. _db.Players.Select(p => p.Id).Take(12)], [QuestionType.WieBenIk, QuestionType.Snelvuur, QuestionType.Jaar]));

        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        var types = challenge.Questions.OrderBy(q => q.Number).Select(q => q.Type).ToList();
        Assert.Equal(10, types.Count);
        Assert.Equal(4, types.Count(t => t == QuestionType.WieBenIk));
        Assert.Equal(3, types.Count(t => t == QuestionType.Snelvuur));
        Assert.Equal(3, types.Count(t => t == QuestionType.Jaar));
        Assert.NotEqual(types[0], types[1]); // afgewisseld, niet gegroepeerd
    }

    [Fact]
    public async Task Admin_rejects_incomplete_or_unsourced_bank_questions()
    {
        var empty = new QuizItemEdit(0, QuestionType.Snelvuur, QuestionEngine.Serialize(new SnelvuurData("", ["A", "", "C", "D"], 0)),
            2, [], [], PlayerStatus.Review, null, null, null, 0);
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveQuizItemAsync(empty, "sander"));

        var good = empty with { Data = QuestionEngine.Serialize(new SnelvuurData("Nieuwe vraag?", ["A", "B", "C", "D"], 1)), Status = PlayerStatus.Approved };
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveQuizItemAsync(good, "sander")); // geen bron
        var id = await _admin.SaveQuizItemAsync(good with { Sources = ["https://example.org"] }, "sander");
        Assert.True(id > 0);
    }

    [Fact]
    public void Mix_stays_within_ten_questions_and_a_thousand_points()
    {
        var withLineups = QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Opstelling]);
        Assert.Equal(4, withLineups[QuestionType.WieBenIk]);
        Assert.Equal(3, withLineups[QuestionType.Opstelling]); // 4×100 + 3×200 = 1000

        var all = QuestionTypes.Mix(QuestionTypes.All.Select(t => t.Type));
        Assert.True(all.Values.Sum() <= GameRules.QuestionsPerDay);
        Assert.Equal(QuestionTypes.DayMaxPoints, all.Sum(m => m.Value * QuestionTypes.Get(m.Key).MaxPoints));
        Assert.All(all.Values, count => Assert.Equal(1, count)); // geen type twee keer zolang er nog andere aan bod moeten komen
    }

    [Fact]
    public async Task Daily_can_be_a_mix_of_types()
    {
        await _admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, QuestionType.Jaar, [QuestionType.Snelvuur, QuestionType.Jaar, QuestionType.WieBenIk])]);

        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.Equal(10, challenge.Questions.Count);
        Assert.Equal(4, challenge.Questions.Count(q => q.Type == QuestionType.WieBenIk));
        Assert.Equal(3, challenge.Questions.Count(q => q.Type == QuestionType.Snelvuur));
        Assert.Equal(3, challenge.Questions.Count(q => q.Type == QuestionType.Jaar));
        // Afgewisseld, niet gegroepeerd.
        Assert.NotEqual(challenge.Questions.Single(q => q.Number == 1).Type, challenge.Questions.Single(q => q.Number == 2).Type);

        var overview = await _quiz.GetOverviewAsync(_me);
        Assert.True(overview.IsMix);
        Assert.Equal(1000, overview.MaxPoints);

        var schedule = await _admin.ScheduleAsync();
        Assert.True(schedule.Days.Single(d => d.Day == DayOfWeek.Saturday).IsMix);
    }

    [Fact]
    public async Task Disabled_type_is_skipped_and_its_day_becomes_a_mix_of_the_rest()
    {
        await PlanTodayAsync(QuestionType.Snelvuur);
        foreach (var t in new[] { QuestionType.Snelvuur, QuestionType.NietBij, QuestionType.Opstelling, QuestionType.Carriere, QuestionType.Emoji,
                     QuestionType.Wissel, QuestionType.Rugnummer, QuestionType.Mannetje, QuestionType.Kluts, QuestionType.Counter })
            await _admin.SetTypeEnabledAsync(t, false);

        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.DoesNotContain(challenge.Questions, q => q.Type == QuestionType.Snelvuur);
        Assert.Equal([QuestionType.WieBenIk, QuestionType.Jaar], challenge.Questions.Select(q => q.Type).Distinct().Order());

        // Het laatste type kan niet uit, en een type terug aanzetten kan altijd.
        await _admin.SetTypeEnabledAsync(QuestionType.Jaar, false);
        await Assert.ThrowsAsync<QuizException>(() => _admin.SetTypeEnabledAsync(QuestionType.WieBenIk, false));
        await _admin.SetTypeEnabledAsync(QuestionType.Snelvuur, true);
        Assert.Equal([QuestionType.Snelvuur], await _quiz.TypesForAsync(_quiz.Today));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
