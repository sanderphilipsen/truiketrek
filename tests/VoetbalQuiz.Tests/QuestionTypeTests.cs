using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class QuestionTypeTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;

    // 17 oktober 2026 is een zaterdag.
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly AdminService _admin;
    private readonly string _me;

    public QuestionTypeTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });

        for (var i = 0; i < 12; i++)
        {
            Add(QuestionType.Snelvuur, $"V{i}", new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], 2));
            Add(QuestionType.Jaar, $"J{i}", new JaarData($"Gebeurtenis {i}", 2000 + i));
            Add(QuestionType.Volgorde, $"R{i}", new VolgordeData($"Opdracht {i}", [.. Enumerable.Range(0, QuestionRules.VolgordeCards).Select(n => new VolgordeItem($"Kaart {i}-{n}", $"Waarde {n}"))]));
            Add(QuestionType.Carriere, $"Speler C{i}", new CarriereData($"Speler C{i}", [], ["Club 1 (2001–2003)", "Club 2 (2003–2006)", "Club 3 (2006–2010)", "Club 4 (2010–2014)"]));
            Add(QuestionType.Emoji, $"E{i}", new EmojiData("⚽🇧🇪", $"Emoji Speler {i}", [], "speler", "uitleg"));
            Add(QuestionType.Opstelling, $"O{i}", new OpstellingData($"Finale {i}", "Ploeg",
                [.. Enumerable.Range(0, 11).Select(n => new LineupPlayer($"Lid{(char)('A' + n)} Ploeg{i}", [], n == 0 ? "K" : "V"))]));
        }

        var user = new AppUser { UserName = "sander" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _me = user.Id;

        (_quiz, _admin) = (_t.Quiz, _t.Admin);
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

    [Theory]
    [InlineData(new[] { 0, 1, 2, 3 }, 100)]
    [InlineData(new[] { 1, 0, 2, 3 }, 83)] // twee buren omgewisseld: 5 van de 6 paren
    [InlineData(new[] { 3, 0, 1, 2 }, 50)]
    [InlineData(new[] { 3, 2, 1, 0 }, 0)]
    public void Volgorde_scores_the_pairs_in_the_right_order(int[] order, int points) =>
        Assert.Equal(points, QuestionRules.VolgordePoints(order));

    /// <summary>De gok voor de juiste volgorde: per plaats het getoonde kaartje (index) dat daar hoort.</summary>
    private static string VolgordeGuess(QuestionState state, VolgordeData data, params int[] correctIndexes) =>
        string.Join(",", correctIndexes.Select(c => state.View!.Options!.IndexOf(data.Items[c].Label)));

    [Fact]
    public async Task Volgorde_shuffles_hides_the_values_and_scores_the_order()
    {
        await PlanTodayAsync(QuestionType.Volgorde);
        var data = QuestionEngine.Parse<VolgordeData>((await FirstQuestionAsync()).QuizItem!.Data);
        var start = await _quiz.StartAsync(_me, 1);

        Assert.Equal(QuestionRules.VolgordeCards, start.View!.Options!.Count);
        Assert.NotEqual(data.Items.Select(i => i.Label), start.View.Options); // nooit al juist
        Assert.DoesNotContain("Waarde", System.Text.Json.JsonSerializer.Serialize(start));

        // Herladen toont dezelfde volgorde.
        Assert.Equal(start.View.Options, (await _quiz.GetStateAsync(_me, 1)).View!.Options);

        var done = await _quiz.GuessAsync(_me, 1, VolgordeGuess(start, data, 0, 1, 2, 3));
        Assert.True(done.Result!.Correct);
        Assert.Equal(100, done.Result.Points);
        Assert.All(done.Result.AllHints, h => Assert.StartsWith("✅", h));
        Assert.Contains("Waarde 0", done.Result.AllHints[0]);

        var second = await _quiz.StartAsync(_me, 2);
        var data2 = QuestionEngine.Parse<VolgordeData>((await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Single(q => q.Number == 2).QuizItem!.Data);
        var swapped = await _quiz.GuessAsync(_me, 2, VolgordeGuess(second, data2, 1, 0, 2, 3));
        Assert.False(swapped.Result!.Correct);
        Assert.Equal(83, swapped.Result.Points);
    }

    [Fact]
    public async Task Volgorde_ignores_invalid_orders_and_puts_untapped_cards_last()
    {
        await PlanTodayAsync(QuestionType.Volgorde);
        var q = await FirstQuestionAsync();
        var data = QuestionEngine.Parse<VolgordeData>(q.QuizItem!.Data);
        var start = await _quiz.StartAsync(_me, 1);

        foreach (var bad in new[] { "0,0", "9", "a,b", "" })
            Assert.Equal(QuestionStatus.InProgress, (await _quiz.GuessAsync(_me, 1, bad)).Status);

        // Alleen het eerste kaartje aangetikt (juist); de rest volgt in de getoonde volgorde.
        var partial = await _quiz.GuessAsync(_me, 1, VolgordeGuess(start, data, 0));
        var shownRest = QuestionEngine.VolgordeShown(q).Where(i => i != 0).ToList();
        Assert.Equal(QuestionRules.VolgordePoints([0, .. shownRest]), partial.Result!.Points);
    }

    [Fact]
    public void Volgorde_never_shows_the_cards_already_in_order()
    {
        var item = new QuizItem { Type = QuestionType.Volgorde, Key = "x",
            Data = QuestionEngine.Serialize(new VolgordeData("?", [.. Enumerable.Range(0, QuestionRules.VolgordeCards).Select(n => new VolgordeItem($"K{n}", $"W{n}"))])) };
        for (var id = 1; id <= 500; id++)
        {
            var shown = QuestionEngine.VolgordeShown(new DailyQuestion { Id = id, Type = QuestionType.Volgorde, QuizItem = item });
            Assert.Equal(Enumerable.Range(0, QuestionRules.VolgordeCards), shown.Order());
            Assert.NotEqual(Enumerable.Range(0, QuestionRules.VolgordeCards), shown);
        }
    }

    [Fact]
    public void Volgorde_checks_its_content()
    {
        static List<string> Problems(params (string Label, string Value)[] items) =>
            QuestionEngine.Problems(QuestionType.Volgorde, QuestionEngine.Serialize(new VolgordeData("Opdracht", [.. items.Select(i => new VolgordeItem(i.Label, i.Value))])));

        Assert.Empty(Problems(("A", "1"), ("B", "2"), ("C", "3"), ("D", "4")));
        Assert.NotEmpty(Problems(("A", "1"), ("B", "2"), ("C", "3")));                // te weinig kaartjes
        Assert.NotEmpty(Problems(("A", "1"), ("B", ""), ("C", "3"), ("D", "4")));     // lege waarde
        Assert.NotEmpty(Problems(("A", "1"), ("a", "2"), ("C", "3"), ("D", "4")));    // dubbele tekst
        Assert.NotEmpty(Problems(("A", "1"), ("B", "1"), ("C", "3"), ("D", "4")));    // dubbele waarde
    }

    [Fact]
    public async Task Volgorde_sent_completely_wrong_is_a_miss_not_a_pass()
    {
        await PlanTodayAsync(QuestionType.Volgorde);
        var data = QuestionEngine.Parse<VolgordeData>((await FirstQuestionAsync()).QuizItem!.Data);
        var start = await _quiz.StartAsync(_me, 1);
        var done = await _quiz.GuessAsync(_me, 1, VolgordeGuess(start, data, 3, 2, 1, 0));
        Assert.Equal(0, done.Result!.Points);
        Assert.Equal([3, 2, 1, 0], done.Result.MyOrder);
        Assert.NotEqual("Gepast.", Commentary.ForResult(done.Result, 100, 1));
    }

    [Fact]
    public async Task Volgorde_without_an_answer_gives_nothing_when_time_runs_out()
    {
        await PlanTodayAsync(QuestionType.Volgorde);
        await _quiz.StartAsync(_me, 1);
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.VolgordeSeconds + 1));
        var end = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(0, end.Result!.Points);
        Assert.True(end.Result.TimedOut);
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
    public async Task Opstelling_stops_after_the_maximum_number_of_guesses()
    {
        await PlanTodayAsync(QuestionType.Opstelling);
        var q = await FirstQuestionAsync();
        var data = QuestionEngine.Parse<OpstellingData>(q.QuizItem!.Data);

        await _quiz.StartAsync(_me, 1);
        await _quiz.GuessAsync(_me, 1, data.Players[q.Blanks[0]].Name);
        // Een script dat namen afvuurt: fout gokken kost niets, maar het aantal pogingen is begrensd.
        QuestionState state = null!;
        for (var i = 1; i < GameRules.MaxGuessesPerQuestion; i++) state = await _quiz.GuessAsync(_me, 1, $"Gok {i}");
        Assert.Equal(QuestionStatus.Answered, state.Status);
        Assert.Equal(QuestionRules.OpstellingPoints(1, QuestionRules.OpstellingBlanks), state.Result!.Points);

        var after = await _quiz.GuessAsync(_me, 1, data.Players[q.Blanks[1]].Name);
        Assert.Equal(state.Result.Points, after.Result!.Points);
    }

    [Fact]
    public async Task Guesses_sent_at_the_same_time_count_only_once()
    {
        await PlanTodayAsync(QuestionType.Emoji);
        var answer = QuestionEngine.Parse<EmojiData>((await FirstQuestionAsync()).QuizItem!.Data).Answer;
        await _quiz.StartAsync(_me, 1);

        // Een tweede verzoek dat tegelijk binnenkomt werkt met een oudere kopie van de poging.
        using var other = _t.NewContext();
        var stale = _t.NewQuiz(other);
        await stale.GetStateAsync(_me, 1);

        await _quiz.GuessAsync(_me, 1, "Iemand");
        var lost = await stale.GuessAsync(_me, 1, "Iemand Anders");
        Assert.Equal(1, lost.WrongGuesses); // de tweede gok vervalt in plaats van de straf te omzeilen
        Assert.Null(lost.LastWrongGuess);

        var right = await _quiz.GuessAsync(_me, 1, answer);
        Assert.Equal(100 - GameRules.WrongGuessPenalty, right.Result!.Points);
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
    public async Task Special_prefers_themed_questions_even_when_still_in_review()
    {
        for (var i = 0; i < 3; i++)
            _db.QuizItems.Add(new QuizItem { Type = QuestionType.Snelvuur, Key = $"Kerst{i}", Status = PlayerStatus.Review, Tags = ["kerst"],
                Data = QuestionEngine.Serialize(new SnelvuurData($"Kerstvraag {i}?", ["A", "B", "C", "D"], 1)) });
        await _db.SaveChangesAsync();

        var id = await _admin.SaveSpecialAsync(new SpecialEdit(0, "kerst", "Kerst", "🎄", "", _quiz.Today, _quiz.Today, "kerst",
            [], [QuestionType.Snelvuur]));
        var keys = (await _quiz.GetOrCreateSpecialChallengeAsync(id)).Questions.Select(q => q.QuizItem!.Key).ToList();

        Assert.Equal(10, keys.Count);
        Assert.All(Enumerable.Range(0, 3), i => Assert.Contains($"Kerst{i}", keys)); // eerst het thema
        Assert.Equal(7, keys.Count(k => k.StartsWith('V'))); // daarna aangevuld met gewone vragen
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
                     QuestionType.Wissel, QuestionType.Rugnummer, QuestionType.Mannetje, QuestionType.Kluts, QuestionType.Counter, QuestionType.Volgorde })
            await _admin.SetTypeEnabledAsync(t, false);

        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.DoesNotContain(challenge.Questions, q => q.Type == QuestionType.Snelvuur);
        Assert.Equal([QuestionType.WieBenIk, QuestionType.Jaar], challenge.Questions.Select(q => q.Type).Distinct().Order());

        // Het laatste type kan niet uit, en een type terug aanzetten kan altijd.
        await _admin.SetTypeEnabledAsync(QuestionType.Jaar, false);
        await Assert.ThrowsAsync<QuizException>(() => _admin.SetTypeEnabledAsync(QuestionType.WieBenIk, false));
        await _admin.SetTypeEnabledAsync(QuestionType.Snelvuur, true);
        Assert.Equal([QuestionType.Snelvuur], await _t.Picker.TypesForAsync(_quiz.Today));
    }

    public void Dispose() => _t.Dispose();
}
