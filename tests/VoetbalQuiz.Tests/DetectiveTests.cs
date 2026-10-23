using System.Text.Json;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>Detectivedag: één zaak met verdachten en getuigen die de hele dag vult.</summary>
public sealed class DetectiveTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly string _me;

    // Zes verdachten (a..f), dader d. Elke verklaring sluit er één uit, de laatste twee.
    private static DetectiveData Case() => new("gestolen-trofee", "De gestolen beker", "Iemand nam de beker mee.",
        [.. "abcdef".Select(c => new DetectiveSuspect(c.ToString(), $"Speler {char.ToUpper(c)}", "Middenvelder"))],
        "d",
        [
            new("Verklaring 1", ["a"], new() { ["a"] = "Reden a" }),
            new("Verklaring 2", ["b"]),
            new("Verklaring 3", ["c"]),
            new("Verklaring 4", ["e", "f"]),
        ],
        "Speler D was het.");

    private const int Culprit = 3;

    public DetectiveTests()
    {
        _db = _t.Db;
        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        _db.QuizItems.Add(new QuizItem
        {
            Type = QuestionType.Detective, Key = "De gestolen beker", Data = QuestionEngine.Serialize(Case()), Status = PlayerStatus.Approved,
        });
        _me = _t.AddUser("sander");
        _db.SaveChanges();
    }

    private async Task PlanTodayAsync() =>
        await _t.Admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, QuestionType.Detective)]); // 17/10/2026 is een zaterdag

    private static int Max => QuestionTypes.Get(QuestionType.Detective).MaxPoints;

    [Theory]
    [InlineData(2, 0, 1000)]
    [InlineData(3, 0, 850)]
    [InlineData(6, 0, 400)]
    [InlineData(8, 0, 250)] // minimum
    [InlineData(2, 1, 500)] // na een foute beschuldiging de helft
    [InlineData(4, 1, 350)]
    [InlineData(2, 2, 0)]
    public void Points_drop_per_extra_witness_and_halve_after_a_wrong_accusation(int opened, int wrong, int points) =>
        Assert.Equal(points, QuestionRules.DetectivePoints(opened, wrong));

    [Fact]
    public void A_case_fills_a_day_on_its_own_and_never_joins_a_mix()
    {
        Assert.Equal(new Dictionary<QuestionType, int> { [QuestionType.Detective] = 1 }, QuestionTypes.Mix([QuestionType.Detective]));
        Assert.DoesNotContain(QuestionType.Detective, QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Detective]).Keys);
        Assert.DoesNotContain(QuestionType.Detective, QuestionTypes.Mix([QuestionType.Snelvuur, QuestionType.Detective], 7).Keys);
    }

    [Fact]
    public async Task A_case_is_not_offered_online()
    {
        Assert.DoesNotContain(QuestionType.Detective, await _t.Picker.ActiveTypesAsync());
    }

    [Fact]
    public async Task The_day_is_one_case_with_one_clock_and_two_witnesses_open()
    {
        await PlanTodayAsync();
        var challenge = await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        Assert.Equal(QuestionType.Detective, Assert.Single(challenge.Questions).Type);

        var state = await _t.Quiz.StartAsync(_me, 1);
        Assert.Equal(QuestionRules.DetectiveSeconds, state.TimeLimit);
        Assert.Equal(Max, state.View!.MaxPoints);
        var d = state.View.Detective!;
        Assert.Equal(["Verklaring 1", "Verklaring 2"], d.Clues);
        Assert.Equal((4, 6, 2, 1000, 850), (d.CluesTotal, d.Suspects.Count, d.AccusationsLeft, d.PointsNow, d.PointsNext));
    }

    [Fact]
    public async Task The_client_never_sees_the_culprit_or_unopened_witnesses()
    {
        await PlanTodayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);

        var json = JsonSerializer.Serialize(state);
        Assert.DoesNotContain("Verklaring 3", json);
        Assert.DoesNotContain("Speler D was het", json);
        Assert.DoesNotContain("Reden a", json);
        Assert.DoesNotContain("excludes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("culprit", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.Result);
    }

    [Fact]
    public async Task Calling_a_witness_opens_the_next_statement_until_none_are_left()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var third = await _t.Quiz.GuessAsync(_me, 1, QuestionRules.DetectiveNextClue);
        Assert.Equal(3, third.View!.Detective!.Clues.Count);
        Assert.Equal((850, 700), (third.View.Detective.PointsNow, third.View.Detective.PointsNext));
        Assert.Equal(0, third.WrongGuesses);

        await _t.Quiz.GuessAsync(_me, 1, QuestionRules.DetectiveNextClue);
        var all = await _t.Quiz.GuessAsync(_me, 1, QuestionRules.DetectiveNextClue); // er zijn er maar 4
        Assert.Equal(4, all.View!.Detective!.Clues.Count);
        Assert.Null(all.View.Detective.PointsNext);
        Assert.Null(all.Result);
    }

    [Fact]
    public async Task Naming_the_culprit_early_earns_the_full_day()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var done = await _t.Quiz.GuessAsync(_me, 1, Culprit.ToString());
        Assert.True(done.Result!.Correct);
        Assert.Equal(Max, done.Result.Points);
        Assert.Equal("Speler D", done.Result.PlayerName);
    }

    [Fact]
    public async Task A_wrong_accusation_halves_the_points_and_a_second_one_closes_the_case()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var wrong = await _t.Quiz.GuessAsync(_me, 1, "0");
        Assert.Null(wrong.Result);
        Assert.Equal(1, wrong.WrongGuesses);
        var d = wrong.View!.Detective!;
        Assert.True(d.Suspects[0].Accused);
        Assert.Equal((1, 500), (d.AccusationsLeft, d.PointsNow));

        var again = await _t.Quiz.GuessAsync(_me, 1, "0"); // dezelfde verdachte telt niet opnieuw
        Assert.Equal(1, again.WrongGuesses);

        var closed = await _t.Quiz.GuessAsync(_me, 1, "1");
        Assert.False(closed.Result!.Correct);
        Assert.Equal(0, closed.Result.Points);
    }

    [Fact]
    public async Task Right_after_a_wrong_accusation_and_a_witness_earns_half()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "0");
        await _t.Quiz.GuessAsync(_me, 1, QuestionRules.DetectiveNextClue);

        var done = await _t.Quiz.GuessAsync(_me, 1, Culprit.ToString());
        Assert.True(done.Result!.Correct);
        Assert.Equal(425, done.Result.Points); // 850 / 2
    }

    [Fact]
    public async Task The_dossier_tells_who_was_ruled_out_by_which_witness()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "4");
        var done = await _t.Quiz.GuessAsync(_me, 1, Culprit.ToString());

        var dossier = done.Result!.Dossier!;
        Assert.Equal(6, dossier.Count);
        Assert.Equal((true, true, (int?)null), (dossier[Culprit].IsCulprit, dossier[Culprit].Accused, dossier[Culprit].Clue));
        Assert.Equal((1, "Reden a", false), (dossier[0].Clue, dossier[0].Reason, dossier[0].Accused));
        Assert.Equal((4, true), (dossier[4].Clue, dossier[4].Accused));
        Assert.Equal(4, done.Result.AllHints.Count);
        Assert.Equal("Speler D was het.", done.Result.Explanation);
    }

    [Fact]
    public async Task Giving_up_or_time_up_earns_nothing()
    {
        await PlanTodayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);
        _t.Clock.Advance(TimeSpan.FromSeconds(state.TimeLimit + 2));

        var after = await _t.Quiz.GetStateAsync(_me, 1);
        Assert.Equal(0, after.Result!.Points);
        Assert.True(after.Result.TimedOut);
        Assert.Equal("Speler D", after.Result.PlayerName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("6")]
    [InlineData("/volgende")]
    public async Task Invalid_input_is_ignored_without_penalty(string guess)
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var state = await _t.Quiz.GuessAsync(_me, 1, guess);
        Assert.Null(state.Result);
        Assert.Equal(0, state.WrongGuesses);
        Assert.Equal(2, state.View!.Detective!.Clues.Count);
    }

    [Fact]
    public void A_valid_case_has_no_problems() =>
        Assert.Empty(QuestionEngine.Problems(QuestionType.Detective, QuestionEngine.Serialize(Case())));

    public static TheoryData<string, Func<DetectiveData, DetectiveData>> BrokenCases => new()
    {
        { "geen titel", c => c with { Title = "" } },
        { "te weinig verdachten", c => c with { Suspects = c.Suspects.Take(5).ToList(), Clues = [.. c.Clues.Select(x => x with { Excludes = x.Excludes.Where(k => k != "f").ToList() })] } },
        { "dader onbekend", c => c with { Culprit = "z" } },
        { "verklaring sluit de dader uit", c => c with { Clues = [.. c.Clues.Take(3), new("Verklaring 4", ["d", "e", "f"])] } },
        { "twee blijven over", c => c with { Clues = [.. c.Clues.Take(3), new("Verklaring 4", ["e"])] } },
        { "verklaring sluit niemand uit", c => c with { Clues = [.. c.Clues, new("Verklaring 5", [])] } },
        { "onbekende verdachte", c => c with { Clues = [.. c.Clues, new("Verklaring 5", ["x"])] } },
        { "te weinig verklaringen", c => c with { Clues = [new("Eén", ["a", "b"]), new("Twee", ["c"]), new("Drie", ["e", "f"])] } },
        { "dader gekend bij de start", c => c with { Clues = [new("Eén", ["a", "b", "c"]), new("Twee", ["e", "f"]), new("Drie", ["a"]), new("Vier", ["b"])] } },
        { "dubbele sleutel", c => c with { Suspects = [.. c.Suspects.Take(5), new("a", "Speler G", "")] } },
    };

    [Theory]
    [MemberData(nameof(BrokenCases))]
    public void A_case_that_is_not_watertight_is_refused(string why, Func<DetectiveData, DetectiveData> breakIt) =>
        Assert.True(QuestionEngine.Problems(QuestionType.Detective, QuestionEngine.Serialize(breakIt(Case()))).Count > 0, why);

    [Fact]
    public void Every_case_in_the_bank_is_watertight()
    {
        var file = Path.Combine(ServerContent.Root, "Data", "questions", "detective.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        Assert.NotEqual(0, doc.RootElement.GetArrayLength());
        foreach (var e in doc.RootElement.EnumerateArray())
            Assert.Empty(QuestionEngine.Problems(QuestionType.Detective, e.GetRawText()));
    }

    [Fact]
    public async Task A_played_case_keeps_its_suspects_and_culprit()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        var item = _db.QuizItems.Single(i => i.Type == QuestionType.Detective);
        var edit = await _t.Admin.QuizItemAsync(item.Id);

        // Teksten bijwerken mag nog.
        await _t.Admin.SaveQuizItemAsync(edit with { Data = QuestionEngine.Serialize(Case() with { Intro = "Beter verhaal." }), Sources = ["https://example.com"] }, "admin");

        var swapped = Case() with { Suspects = [.. Case().Suspects.AsEnumerable().Reverse()] };
        await Assert.ThrowsAsync<QuizException>(() =>
            _t.Admin.SaveQuizItemAsync(edit with { Data = QuestionEngine.Serialize(swapped), Sources = ["https://example.com"] }, "admin"));
    }

    public void Dispose() => _t.Dispose();
}
