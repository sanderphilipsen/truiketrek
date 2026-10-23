using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>De Krant: een artikel met gaten dat de hele dag vult.</summary>
public sealed class KrantTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly string _me;

    // Vier gaten: drie namen en één getal (een minuut, met deelpunten per minuut ernaast).
    private const string Text = """
        In de [[#69 : minuut]]e minuut kopte [[Jan Vertonghen / Vertonghen : speler]] de 1-2 binnen.

        Daarna scoorden [[Marouane Fellaini / Fellaini : speler]] en [[Nacer Chadli / Chadli : speler]].
        """;

    public KrantTests()
    {
        _db = _t.Db;
        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        _db.QuizItems.Add(new QuizItem
        {
            Type = QuestionType.Krant, Key = "België – Japan",
            Data = QuestionEngine.Serialize(new KrantData("verslag", "België – Japan", "WK 2018", Text)),
            Status = PlayerStatus.Approved,
        });
        _me = _t.AddUser("sander");
        _db.SaveChanges();
    }

    private async Task PlanTodayAsync() =>
        await _t.Admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, QuestionType.Krant)]); // 17/10/2026 is een zaterdag

    private static int Max => QuestionTypes.Get(QuestionType.Krant).MaxPoints;

    [Fact]
    public void Text_is_read_into_paragraphs_and_gaps()
    {
        var article = KrantText.Parse(Text);

        Assert.Empty(article.Problems);
        Assert.Equal(2, article.Paragraphs.Count);
        Assert.Equal(4, article.Gaps.Count);
        Assert.Equal(("69", "minuut", 69, 1), (article.Gaps[0].Answer, article.Gaps[0].Hint, article.Gaps[0].Number, article.Gaps[0].Tolerance));
        Assert.Equal("Jan Vertonghen", article.Gaps[1].Answer);
        Assert.Equal(["Vertonghen"], article.Gaps[1].Aliases);
        Assert.Equal("speler", article.Gaps[1].Hint);

        var money = KrantText.Parse("Hij kostte [[#115~5 : miljoen euro]].").Gaps.Single();
        Assert.Equal((115, 5), (money.Number, money.Tolerance));

        Assert.NotEmpty(KrantText.Parse("Kapot [[#abc : minuut]] en [[ : leeg]]").Problems);
    }

    [Theory]
    [InlineData(69, 1, 1)]
    [InlineData(70, 1, 0.75)]
    [InlineData(66, 1, 0.25)]
    [InlineData(65, 1, 0)]
    [InlineData(110, 5, 0.75)] // 115 met een stap van 5
    [InlineData(101, 5, 0.25)]
    public void A_number_close_to_the_answer_earns_part_of_the_gap(int guess, int tolerance, double score)
    {
        var answer = tolerance == 1 ? 69 : 115;
        Assert.Equal(score, QuestionRules.KrantNumberScore(guess, answer, tolerance));
    }

    [Fact]
    public void A_newspaper_fills_a_day_on_its_own_and_never_joins_a_mix()
    {
        Assert.Equal(new Dictionary<QuestionType, int> { [QuestionType.Krant] = 1 }, QuestionTypes.Mix([QuestionType.Krant]));
        Assert.DoesNotContain(QuestionType.Krant, QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Krant]).Keys);
        Assert.DoesNotContain(QuestionType.Krant, QuestionTypes.Mix([QuestionType.Snelvuur, QuestionType.Krant], 7).Keys);
    }

    [Fact]
    public async Task A_newspaper_is_not_offered_online()
    {
        Assert.DoesNotContain(QuestionType.Krant, await _t.Picker.ActiveTypesAsync());
    }

    [Fact]
    public async Task The_day_is_one_article_worth_the_whole_day_with_one_clock()
    {
        await PlanTodayAsync();
        var challenge = await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        Assert.Equal(QuestionType.Krant, Assert.Single(challenge.Questions).Type);

        var state = await _t.Quiz.StartAsync(_me, 1);
        Assert.Equal(QuestionRules.KrantLimit(4), state.TimeLimit);
        Assert.Equal(Max, state.View!.MaxPoints);
        Assert.Equal((0, 4), (state.View.Found, state.View.Total));
    }

    [Fact]
    public async Task The_client_never_sees_an_answer_before_the_end()
    {
        await PlanTodayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);

        var gaps = state.View!.Article!.SelectMany(p => p).Where(p => p.Gap >= 0).ToList();
        Assert.Equal(4, gaps.Count);
        Assert.All(gaps, g => Assert.True(g.Filled is null && g.Answer is null && g.Score is null));
        Assert.DoesNotContain(state.View.Article!.SelectMany(p => p), p => p.Text.Contains("Vertonghen") || p.Text.Contains("69"));
        Assert.Null(state.Result);
    }

    [Fact]
    public async Task Gaps_can_be_filled_in_any_order_and_a_wrong_name_costs_nothing()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var wrong = await _t.Quiz.GuessAsync(_me, 1, "3|Fellaini"); // gat 3 is Chadli
        Assert.Equal((0, 0), (wrong.WrongGuesses, wrong.View!.Found));

        var right = await _t.Quiz.GuessAsync(_me, 1, "3|chadli");
        Assert.Equal(1, right.View!.Found);
        Assert.Equal("Nacer Chadli", right.View.Article!.SelectMany(p => p).Single(p => p.Gap == 3).Filled);

        var again = await _t.Quiz.GuessAsync(_me, 1, "3|Chadli"); // al ingevuld: telt niet dubbel
        Assert.Equal(1, again.View!.Found);
        Assert.Null(again.Result);
    }

    [Fact]
    public async Task A_number_is_guessed_once_and_scores_by_distance()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var first = await _t.Quiz.GuessAsync(_me, 1, "0|70e");
        Assert.Equal("70", first.View!.Article!.SelectMany(p => p).Single(p => p.Gap == 0).Filled);

        await _t.Quiz.GuessAsync(_me, 1, "0|69"); // tweede gok op hetzelfde getal telt niet
        var done = await _t.Quiz.GiveUpAsync(_me, 1);

        // 0,75 van één gat uit vier: 1000 × 0,75 / 4.
        Assert.Equal(QuestionRules.KrantPoints(0.75, 4), done.Result!.Points);
        var minute = done.Result.FullArticle!.SelectMany(p => p).Single(p => p.Gap == 0);
        Assert.Equal(("69", "70", 0.75), (minute.Answer, minute.Filled, minute.Score));
    }

    [Fact]
    public async Task Filling_every_gap_ends_the_article_with_the_full_day()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        foreach (var guess in new[] { "1|Vertonghen", "2|Marouane Fellaini", "0|69" })
            Assert.Null((await _t.Quiz.GuessAsync(_me, 1, guess)).Result);
        var last = await _t.Quiz.GuessAsync(_me, 1, "3|Chadli");

        Assert.True(last.Result!.Correct);
        Assert.Equal(Max, last.Result.Points);
        Assert.All(last.Result.FullArticle!.SelectMany(p => p).Where(p => p.Gap >= 0), g => Assert.Equal(1, g.Score));
    }

    [Fact]
    public async Task Stopping_keeps_the_gaps_you_found_and_reveals_the_rest()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");

        var stopped = await _t.Quiz.GiveUpAsync(_me, 1);

        Assert.False(stopped.Result!.Correct);
        Assert.Equal(Max / 4, stopped.Result.Points);
        var gaps = stopped.Result.FullArticle!.SelectMany(p => p).Where(p => p.Gap >= 0).ToList();
        Assert.Equal(["69", "Jan Vertonghen", "Marouane Fellaini", "Nacer Chadli"], gaps.Select(g => g.Answer));
        Assert.Equal([0, 1, 0, 0], gaps.Select(g => g.Score ?? -1));
    }

    [Fact]
    public async Task Time_up_keeps_the_partial_points()
    {
        await PlanTodayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "2|Fellaini");

        _t.Clock.Advance(TimeSpan.FromSeconds(state.TimeLimit + 2));
        var after = await _t.Quiz.GetStateAsync(_me, 1);

        Assert.True(after.Result!.TimedOut);
        Assert.Equal(Max / 4, after.Result.Points);
    }

    [Fact]
    public void An_article_needs_a_headline_hints_and_enough_gaps()
    {
        string Data(string headline, string text) => QuestionEngine.Serialize(new KrantData("verslag", headline, "", text));

        Assert.Empty(QuestionEngine.Problems(QuestionType.Krant, Data("België – Japan", Text)));
        Assert.NotEmpty(QuestionEngine.Problems(QuestionType.Krant, Data("", Text)));
        Assert.NotEmpty(QuestionEngine.Problems(QuestionType.Krant, Data("Kop", "Te weinig: [[Vertonghen : speler]].")));
        Assert.NotEmpty(QuestionEngine.Problems(QuestionType.Krant, Data("Vertonghen scoort", Text))); // antwoord in de kop
        Assert.NotEmpty(QuestionEngine.Problems(QuestionType.Krant, Data("Kop", Text.Replace(" : speler", ""))));
    }

    [Theory]
    [InlineData("69", 69)]
    [InlineData("69e minuut", 69)]
    [InlineData("€ 115 mln", 115)]
    [InlineData("2-1", 2)]
    [InlineData("69 of 70", 69)]
    [InlineData("geen idee", null)]
    public void A_typed_number_is_its_first_group_of_digits(string input, int? number) =>
        Assert.Equal(number, KrantText.ParseNumber(input));

    [Fact]
    public void Negative_numbers_are_not_a_valid_gap() =>
        Assert.NotEmpty(KrantText.Parse("Doelsaldo [[#-3 : doelsaldo]]").Problems);

    [Theory]
    [InlineData("Kop", "WK 1969", "In de [[#69 : minuut]]e minuut.")] // 69 als los woord, niet in 1969: geen probleem
    [InlineData("Kop", "", "In [[#1970 : jaar]], net als in 1970.")]
    [InlineData("Kop", "Sinds 1970", "Het was [[#1970 : jaar]].")]
    [InlineData("Kop", "", "Hij heet [[Vertonghen : Vertonghen]].")]
    [InlineData("Kop", "", "[[Fellaini : speler]] en Fellaini nog eens.")]
    public void Answers_that_are_already_visible_are_flagged(string headline, string intro, string text)
    {
        var leaks = QuestionEngine.Problems(QuestionType.Krant,
            QuestionEngine.Serialize(new KrantData("verslag", headline, intro, Text + "\n\n" + text)));
        Assert.Equal(intro == "WK 1969", leaks.Count == 0);
    }

    [Fact]
    public async Task Invalid_input_is_ignored_without_points_or_penalty()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        foreach (var guess in new[] { "99|Vertonghen", "-1|Vertonghen", "Vertonghen", "abc|Vertonghen", "1|", "0|geen idee" })
        {
            var state = await _t.Quiz.GuessAsync(_me, 1, guess);
            Assert.Equal((0, 0), (state.View!.Found, state.WrongGuesses));
            Assert.Null(state.Result);
        }
        // Na "0|geen idee" mag het getal nog gegokt worden.
        Assert.Equal(1, (await _t.Quiz.GuessAsync(_me, 1, "0|69")).View!.Found);
    }

    [Fact]
    public async Task A_newspaper_day_earns_xp_like_a_full_day()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");
        await _t.Quiz.GiveUpAsync(_me, 1);

        var xp = (await _t.Results.XpInputsAsync())[_me];
        Assert.Equal(GameRules.QuestionsPerDay, xp.QuestionsPlayed);
        Assert.Equal(Max / 4, xp.CorrectPoints); // ook deelpunten tellen, al is het artikel niet foutloos
    }

    [Fact]
    public async Task A_played_article_keeps_its_gaps_in_the_admin()
    {
        await PlanTodayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");
        var item = _db.QuizItems.Single(i => i.Type == QuestionType.Krant);

        QuizItemEdit Edit(string text) => new(item.Id, QuestionType.Krant,
            QuestionEngine.Serialize(new KrantData("verslag", "België – Japan", "WK 2018", text)), 3, ["https://example.org"], [], PlayerStatus.Approved, null, null, null, 0);

        // Een hint of wat ook goed is aanpassen mag nog.
        await _t.Admin.SaveQuizItemAsync(Edit(Text.Replace("Vertonghen / Vertonghen : speler", "Vertonghen / Vertonghen / Jan V. : verdediger")), "admin");
        // Een gat vooraan erbij niet: dan schuiven de gevonden gaten op.
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveQuizItemAsync(Edit("Vooraf [[Hazard : speler]]. " + Text), "admin"));
    }

    public void Dispose() => _t.Dispose();
}
