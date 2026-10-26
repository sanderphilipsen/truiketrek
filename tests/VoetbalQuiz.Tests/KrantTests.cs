using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>De Krant: de vaste rubriek "De Zondag", vier artikels met elk drie gaten.</summary>
public sealed class KrantTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly string _me;

    // Drie gaten: twee namen en één getal (een minuut, met deelpunten per minuut ernaast).
    private const string Text = """
        In de [[#69 : minuut]]e minuut kopte [[Jan Vertonghen / Vertonghen : speler]] de 1-2 binnen.

        Daarna scoorde [[Nacer Chadli / Chadli : speler]] de 3-2.
        """;

    public KrantTests()
    {
        _db = _t.Db;
        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        // Vier artikels: precies genoeg voor één zondag. In de tests speel je telkens het eerste.
        for (var i = 1; i <= 4; i++)
            _db.QuizItems.Add(new QuizItem
            {
                Type = QuestionType.Krant, Key = $"Artikel {i}",
                Data = QuestionEngine.Serialize(new KrantData("verslag", $"Artikel {i}", "WK 2018", Text)),
                Status = PlayerStatus.Approved,
            });
        _me = _t.AddUser("sander");
        _db.SaveChanges();
    }

    /// <summary>Van zaterdag 17/10/2026 naar zondag: De Zondag, wat de planning ook zegt.</summary>
    private async Task ToSundayAsync()
    {
        await _t.Admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Sunday, QuestionType.Snelvuur)]);
        _t.Clock.Advance(TimeSpan.FromDays(1));
    }

    private static int Max => QuestionTypes.Get(QuestionType.Krant).MaxPoints;

    [Fact]
    public void Text_is_read_into_paragraphs_and_gaps()
    {
        var article = KrantText.Parse(Text);

        Assert.Empty(article.Problems);
        Assert.Equal(2, article.Paragraphs.Count);
        Assert.Equal(3, article.Gaps.Count);
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
    [InlineData(70, 1, 0.4)]
    [InlineData(67, 1, 0.2)]
    [InlineData(66, 1, 0)]
    [InlineData(110, 5, 0.4)] // 115 met een stap van 5
    [InlineData(106, 5, 0.2)]
    [InlineData(101, 5, 0)]
    public void A_number_close_to_the_answer_earns_part_of_the_gap(int guess, int tolerance, double score)
    {
        var answer = tolerance == 1 ? 69 : 115;
        Assert.Equal(score, QuestionRules.KrantNumberScore(guess, answer, tolerance));
    }

    [Fact]
    public void A_newspaper_fills_a_day_on_its_own_and_never_joins_a_mix()
    {
        Assert.Equal(new Dictionary<QuestionType, int> { [QuestionType.Krant] = 4 }, QuestionTypes.Mix([QuestionType.Krant]));
        Assert.DoesNotContain(QuestionType.Krant, QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Krant]).Keys);
        Assert.DoesNotContain(QuestionType.Krant, QuestionTypes.Mix([QuestionType.Snelvuur, QuestionType.Krant], 7).Keys);
    }

    [Fact]
    public async Task A_newspaper_is_not_offered_online()
    {
        Assert.DoesNotContain(QuestionType.Krant, await _t.Picker.ActiveTypesAsync());
    }

    [Fact]
    public async Task Every_sunday_is_four_articles_of_five_gaps_worth_the_whole_day()
    {
        await ToSundayAsync();
        var challenge = await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        Assert.Equal(4, challenge.Questions.Count);
        Assert.All(challenge.Questions, q => Assert.Equal(QuestionType.Krant, q.Type));
        Assert.Equal(QuestionTypes.DayMaxPoints, 4 * Max);
        Assert.Equal(83, Max / QuestionRules.KrantGaps);

        // Op een andere dag volgt de planning gewoon (hier: niets ingepland, dus de standaard voor maandag).
        Assert.DoesNotContain(QuestionType.Krant, await _t.Picker.TypesForAsync(_t.Quiz.Today.AddDays(1)));

        var state = await _t.Quiz.StartAsync(_me, 1);
        Assert.Equal(QuestionRules.KrantSeconds, state.TimeLimit);
        Assert.Equal(Max, state.View!.MaxPoints);
        Assert.Equal((0, 3), (state.View.Found, state.View.Total));
    }

    [Fact]
    public async Task The_client_never_sees_an_answer_before_the_end()
    {
        await ToSundayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);

        var gaps = state.View!.Article!.SelectMany(p => p).Where(p => p.Gap >= 0).ToList();
        Assert.Equal(3, gaps.Count);
        Assert.All(gaps, g => Assert.True(g.Filled is null && g.Answer is null && g.Score is null));
        Assert.DoesNotContain(state.View.Article!.SelectMany(p => p), p => p.Text.Contains("Vertonghen") || p.Text.Contains("69"));
        Assert.Null(state.Result);
    }

    [Fact]
    public async Task Gaps_can_be_filled_in_any_order_and_a_wrong_name_costs_nothing()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var wrong = await _t.Quiz.GuessAsync(_me, 1, "2|Fellaini"); // gat 2 is Chadli
        Assert.Equal((0, 0), (wrong.WrongGuesses, wrong.View!.Found));

        var right = await _t.Quiz.GuessAsync(_me, 1, "2|chadli");
        Assert.Equal(1, right.View!.Found);
        Assert.Equal("Nacer Chadli", right.View.Article!.SelectMany(p => p).Single(p => p.Gap == 2).Filled);

        var again = await _t.Quiz.GuessAsync(_me, 1, "2|Chadli"); // al ingevuld: telt niet dubbel
        Assert.Equal(1, again.View!.Found);
        Assert.Null(again.Result);
    }

    [Fact]
    public async Task A_number_is_guessed_once_and_scores_by_distance()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        var first = await _t.Quiz.GuessAsync(_me, 1, "0|70e");
        Assert.Equal("70", first.View!.Article!.SelectMany(p => p).Single(p => p.Gap == 0).Filled);

        await _t.Quiz.GuessAsync(_me, 1, "0|69"); // tweede gok op hetzelfde getal telt niet
        var done = await _t.Quiz.GiveUpAsync(_me, 1);

        // 0,4 van één gat uit drie: 250 × 0,4 / 3.
        Assert.Equal(QuestionRules.KrantPoints(0.4, 3), done.Result!.Points);
        var minute = done.Result.FullArticle!.SelectMany(p => p).Single(p => p.Gap == 0);
        Assert.Equal(("69", "70", 0.4), (minute.Answer, minute.Filled, minute.Score));
    }

    [Fact]
    public async Task Filling_every_gap_ends_the_article_with_the_full_day()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);

        foreach (var guess in new[] { "1|Vertonghen", "0|69" })
            Assert.Null((await _t.Quiz.GuessAsync(_me, 1, guess)).Result);
        var last = await _t.Quiz.GuessAsync(_me, 1, "2|Chadli");

        Assert.True(last.Result!.Correct);
        Assert.Equal(Max, last.Result.Points);
        Assert.All(last.Result.FullArticle!.SelectMany(p => p).Where(p => p.Gap >= 0), g => Assert.Equal(1, g.Score));
    }

    [Fact]
    public async Task Stopping_keeps_the_gaps_you_found_and_reveals_the_rest()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");

        var stopped = await _t.Quiz.GiveUpAsync(_me, 1);

        Assert.False(stopped.Result!.Correct);
        Assert.Equal(QuestionRules.KrantPoints(1, 3), stopped.Result.Points);
        var gaps = stopped.Result.FullArticle!.SelectMany(p => p).Where(p => p.Gap >= 0).ToList();
        Assert.Equal(["69", "Jan Vertonghen", "Nacer Chadli"], gaps.Select(g => g.Answer));
        Assert.Equal([0, 1, 0], gaps.Select(g => g.Score ?? -1));
    }

    [Fact]
    public async Task Time_up_keeps_the_partial_points()
    {
        await ToSundayAsync();
        var state = await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "2|Chadli");

        _t.Clock.Advance(TimeSpan.FromSeconds(state.TimeLimit + 2));
        var after = await _t.Quiz.GetStateAsync(_me, 1);

        Assert.True(after.Result!.TimedOut);
        Assert.Equal(QuestionRules.KrantPoints(1, 3), after.Result.Points);
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
        // Enkel de meldingen over verklappen (het extra gat maakt er vier, dat is een andere melding).
        var leaks = QuestionEngine.Problems(QuestionType.Krant,
                QuestionEngine.Serialize(new KrantData("verslag", headline, intro, Text + "\n\n" + text)))
            .Where(p => p.Contains("staat") || p.Contains("verraadt")).ToList();
        Assert.Equal(intro == "WK 1969", leaks.Count == 0);
    }

    [Fact]
    public async Task Invalid_input_is_ignored_without_points_or_penalty()
    {
        await ToSundayAsync();
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
        await ToSundayAsync();
        for (var n = 1; n <= 4; n++)
        {
            await _t.Quiz.StartAsync(_me, n);
            // Elk artikel geprobeerd (ook een foute gok telt), anders telt het niet als gespeeld (Levels.Tried).
            await _t.Quiz.GuessAsync(_me, n, n == 1 ? "1|Vertonghen" : "1|Niemand");
            await _t.Quiz.GiveUpAsync(_me, n);
        }

        var xp = (await _t.Results.XpInputsAsync())[_me];
        Assert.Equal(GameRules.QuestionsPerDay, xp.QuestionsPlayed); // vier artikels tellen samen als een volle dag
        Assert.Equal(QuestionRules.KrantPoints(1, 3), xp.CorrectPoints); // ook deelpunten tellen, al is het artikel niet foutloos
    }

    [Fact]
    public async Task The_day_leaderboard_counts_a_partly_filled_article_as_partly_correct()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");
        await _t.Quiz.GiveUpAsync(_me, 1);
        await _t.Quiz.StartAsync(_me, 2);
        await _t.Quiz.GiveUpAsync(_me, 2); // niets gevonden: fout, niet deels juist

        var me = Assert.Single((await _t.Results.GetDailyLeaderboardAsync(_me)).Entries);
        Assert.Equal((0, 1), (me.CorrectAnswers, me.PartialAnswers));
    }

    [Fact]
    public async Task A_played_article_keeps_its_gaps_in_the_admin()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");
        var item = _db.QuizItems.Single(i => i.Key == "Artikel 1");

        QuizItemEdit Edit(string text) => new(item.Id, QuestionType.Krant,
            QuestionEngine.Serialize(new KrantData("verslag", "België – Japan", "WK 2018", text)), 3, ["https://example.org"], [], PlayerStatus.Approved, null, null, null, 0);

        // Een hint of wat ook goed is aanpassen mag nog.
        await _t.Admin.SaveQuizItemAsync(Edit(Text.Replace("Vertonghen / Vertonghen : speler", "Vertonghen / Vertonghen / Jan V. : verdediger")), "admin");
        // Een gat vooraan erbij niet: dan schuiven de gevonden gaten op.
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveQuizItemAsync(Edit("Vooraf [[Hazard : speler]]. " + Text), "admin"));
    }

    [Fact]
    public async Task The_seeder_does_not_change_the_gaps_of_a_played_article()
    {
        await ToSundayAsync();
        await _t.Quiz.StartAsync(_me, 1);
        await _t.Quiz.GuessAsync(_me, 1, "1|Vertonghen");
        var played = _db.Attempts.Select(a => a.DailyQuestion.QuizItem!).Single(); // het artikel van vraag 1 (de volgorde is willekeurig)
        var before = played.Data;

        async Task SeedAsync(string text)
        {
            var root = Directory.CreateTempSubdirectory("krant-seed-").FullName;
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Data", "questions"));
                var article = new { kind = "verslag", headline = played.Key, intro = "WK 2018", text, difficulty = 4, status = "approved", sources = new[] { "https://example.org" } };
                await File.WriteAllTextAsync(Path.Combine(root, "Data", "questions", "krant.json"),
                    System.Text.Json.JsonSerializer.Serialize(new[] { article }));
                await new QuestionSeeder(_db, ServerContent.Environment(root), Microsoft.Extensions.Logging.Abstractions.NullLogger<QuestionSeeder>.Instance).SeedAsync();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        // Andere gaten: de oude blijven, want de poging onthoudt gat 1 als Vertonghen. De rest wordt wel bijgewerkt.
        await SeedAsync(Text.Replace("[[Jan Vertonghen / Vertonghen : speler]]", "Jan Vertonghen").Replace("de 3-2", "de 3-2 na werk van [[Eden Hazard / Hazard : speler]]"));
        Assert.Equal(before, played.Data);
        Assert.Equal(4, played.Difficulty);

        // Dezelfde gaten met een andere hint: dat neemt het artikel wel over.
        await SeedAsync(Text.Replace(" : speler]] de 1-2", " : verdediger]] de 1-2"));
        Assert.Contains("verdediger", played.Data);
    }

    [Fact]
    public void An_article_that_shows_an_answer_of_another_clashes_with_it()
    {
        var hazard = QuestionEngine.Serialize(new KrantData("verslag", "Een solo", "", HazardText));
        var mentions = QuestionEngine.Serialize(new KrantData("verslag", "Een assist", "", "Een pass van Hazard. " + LukakuText));

        Assert.Equal(["Hazard"], QuestionEngine.KrantShownBy(hazard, mentions));
        Assert.True(QuestionEngine.KrantClash(hazard, mentions));
        Assert.True(QuestionEngine.KrantClash(mentions, hazard)); // in beide richtingen
        Assert.False(QuestionEngine.KrantClash(hazard, QuestionEngine.Serialize(new KrantData("verslag", "Een kopbal", "", LukakuText))));
    }

    private const string HazardText = "[[Eden Hazard / Hazard : speler]] dribbelde langs [[#3 : verdedigers]] en scoorde in [[Kazan : stad]].";
    private const string LukakuText = "[[Romelu Lukaku / Lukaku : speler]] kopte binnen tegen [[Panama : land]] in [[Sotsji : stad]].";

    /// <summary>
    /// Op een zondag komen er vier artikels in willekeurige volgorde: twee artikels die elkaars antwoorden tonen, komen nooit
    /// samen. Hier kan dat: vijf kandidaten, waarvan één van het botsende paar mag blijven liggen.
    /// </summary>
    [Fact]
    public async Task Two_articles_that_give_away_each_others_answers_never_share_a_sunday()
    {
        foreach (var item in _db.QuizItems.Where(i => i.Key == "Artikel 3" || i.Key == "Artikel 4")) item.Status = PlayerStatus.Rejected;
        foreach (var (key, text) in new[] { ("Hazard", HazardText), ("Pass", "Een pass van Hazard. " + LukakuText),
                     ("Courtois", "[[Thibaut Courtois / Courtois : keeper]] stopte [[#9 : reddingen]] in [[Parijs : stad]].") })
            _db.QuizItems.Add(new QuizItem
            {
                Type = QuestionType.Krant, Key = key, Status = PlayerStatus.Approved,
                Data = QuestionEngine.Serialize(new KrantData("verslag", key, "", text)),
            });
        await _db.SaveChangesAsync();

        await ToSundayAsync();
        var ids = (await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today)).Questions.Select(q => q.QuizItemId).ToList();
        var keys = ids.Select(id => _db.QuizItems.Single(i => i.Id == id).Key).ToList();

        Assert.Equal(4, keys.Count);
        Assert.Contains("Courtois", keys);
        Assert.Single(keys, k => k is "Hazard" or "Pass");
    }

    /// <summary>
    /// De echte artikels (krant.json): elk artikel is geldig, en elk kan op een zondag komen met drie andere die zijn
    /// antwoorden niet tonen (en omgekeerd). Botsende artikels houdt de kiezer uit elkaar.
    /// </summary>
    [Fact]
    public void Every_article_in_the_bank_is_valid_and_fits_a_sunday_without_clashes()
    {
        var file = Path.Combine(ServerContent.Root, "Data", "questions", "krant.json");
        var articles = System.Text.Json.JsonSerializer.Deserialize<List<KrantData>>(File.ReadAllText(file),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!
            .Select(d => (d.Headline, Data: QuestionEngine.Serialize(d with { Intro = d.Intro ?? "" }))).ToList();

        var problems = articles.SelectMany(a => QuestionEngine.Problems(QuestionType.Krant, a.Data).Select(p => $"\"{a.Headline}\": {p}")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Equal(articles.Count, articles.DistinctBy(a => a.Headline).Count());

        var lonely = new List<string>();
        foreach (var a in articles)
        {
            var sunday = new List<string> { a.Data };
            foreach (var b in articles.Where(b => b.Headline != a.Headline))
                if (sunday.Count < 4 && sunday.All(s => !QuestionEngine.KrantClash(s, b.Data))) sunday.Add(b.Data);
            if (sunday.Count < 4) lonely.Add(a.Headline);
        }
        Assert.True(lonely.Count == 0, "Geen zondag zonder botsing voor: " + string.Join("\n", lonely));
    }

    public void Dispose() => _t.Dispose();
}
