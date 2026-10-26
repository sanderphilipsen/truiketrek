using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>De rondes wissel, rugnummer, mannetje, kluts in de zestien en counter, en de derby-specials.</summary>
public sealed class NewRoundTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;

    // 17 oktober 2026 is een zaterdag.
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly AdminService _admin;
    private readonly string _me;

    private static readonly string[] FirstNames = ["Arne", "Bert", "Cas", "Dirk", "Emiel", "Frank", "Gert", "Hugo", "Ivo", "Jef"];
    private static readonly string[] LastNames = ["Aerts", "Boon", "Claes", "Dewit", "Engels", "Foket"];

    public NewRoundTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        // 60 spelers met duidelijk verschillende namen: genoeg voor 2 counters van 25.
        var n = 0;
        foreach (var first in FirstNames)
            foreach (var last in LastNames)
            {
                n++;
                _db.Players.Add(new Player
                {
                    Name = $"{first} {last}",
                    Status = PlayerStatus.Approved,
                    Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h} van speler {n}")],
                    Tags = n <= 8 ? ["anderlecht"] : n <= 16 ? ["club-brugge"] : [],
                });
            }

        for (var i = 0; i < 12; i++)
        {
            Add(QuestionType.Wissel, new WisselData($"Wissel Speler {i}", [], "Zomer 2019", "€100 miljoen", $"Club {i}", "Real Madrid", "Een tip"));
            Add(QuestionType.Rugnummer, new RugnummerData($"Nummer Speler {i}", [], 7, "Aanvaller", "België", $"Club {i}", "Een tip"));
            Add(QuestionType.Mannetje, new MannetjeData($"Mannetje Speler {i}", [], Avatars.All[i].Id, ["Tip een", "Tip twee"]));
            Add(QuestionType.Fiche, new FicheData($"Fiche Speler {i}", [], 189, 1987, "Centrale verdediger", 157, 10, "België",
                $"Club {i}", new DateOnly(2026, 10, 1)));
        }

        var user = new AppUser { UserName = "sander" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _me = user.Id;

        (_quiz, _admin) = (_t.Quiz, _t.Admin);
    }

    private void Add<T>(QuestionType type, T data)
    {
        var json = QuestionEngine.Serialize(data);
        _db.QuizItems.Add(new QuizItem { Type = type, Key = QuestionEngine.KeyFor(type, json), Data = json, Status = PlayerStatus.Approved });
    }

    private async Task PlanTodayAsync(QuestionType type) =>
        await _admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, type)]);

    private async Task<DailyQuestion> FirstQuestionAsync() =>
        (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.Single(q => q.Number == 1);

    private void NextStep(QuestionType type = QuestionType.Wissel) =>
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.StepSeconds(type) + 0.5));

    [Fact]
    public async Task Wissel_reveals_one_fact_per_step()
    {
        await PlanTodayAsync(QuestionType.Wissel);
        var data = QuestionEngine.Parse<WisselData>((await FirstQuestionAsync()).QuizItem!.Data);

        var start = await _quiz.StartAsync(_me, 1);
        Assert.Single(start.Hints);
        Assert.Contains(data.When, start.Hints[0]);
        Assert.DoesNotContain(start.Hints, h => h.Contains(data.Answer));

        NextStep();
        var later = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(2, later.Hints.Count);
        Assert.Equal(2, later.View!.Step);

        var done = await _quiz.GuessAsync(_me, 1, data.Answer);
        Assert.Equal(90, done.Result!.Points);
        Assert.Equal(QuestionRules.RevealSteps, done.Result.AllHints.Count);
    }

    [Fact]
    public async Task Rugnummer_shows_the_number_first()
    {
        await PlanTodayAsync(QuestionType.Rugnummer);
        var data = QuestionEngine.Parse<RugnummerData>((await FirstQuestionAsync()).QuizItem!.Data);

        var start = await _quiz.StartAsync(_me, 1);
        Assert.Equal(7, start.View!.Shirt);
        Assert.Empty(start.Hints);

        var wrong = await _quiz.GuessAsync(_me, 1, "Iemand Anders");
        Assert.Equal(1, wrong.WrongGuesses);
        var done = await _quiz.GuessAsync(_me, 1, data.Answer);
        Assert.Equal(100 - GameRules.WrongGuessPenalty, done.Result!.Points);
    }

    [Fact]
    public async Task Mannetje_starts_as_a_silhouette_without_the_real_colours()
    {
        await PlanTodayAsync(QuestionType.Mannetje);
        var data = QuestionEngine.Parse<MannetjeData>((await FirstQuestionAsync()).QuizItem!.Data);
        var real = Avatars.Get(data.Avatar);

        var start = await _quiz.StartAsync(_me, 1);
        var silhouette = Avatars.Get(start.View!.Avatar);
        Assert.StartsWith("c1:", start.View.Avatar); // nooit de id met de naam van de avatar
        Assert.Equal(0, silhouette.Number);
        Assert.Equal(3, silhouette.Shirt);
        Assert.Equal(real.Style, silhouette.Style);

        NextStep();
        NextStep();
        var sharp = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(real.Number, Avatars.Get(sharp.View!.Avatar).Number);
        Assert.Empty(sharp.Hints); // de tips komen pas in stap 4 en 5

        NextStep();
        Assert.Single((await _quiz.GetStateAsync(_me, 1)).Hints);
    }

    [Fact]
    public async Task Fiche_shows_only_the_open_fields_and_a_wrong_name_opens_the_next_without_penalty()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        var data = QuestionEngine.Parse<FicheData>((await FirstQuestionAsync()).QuizItem!.Data);

        var start = await _quiz.StartAsync(_me, 1);
        var fields = start.View!.Fiche!;
        Assert.Equal(QuestionRules.FicheSteps, fields.Count);
        Assert.Equal("1,89 m", fields[0].Value);
        Assert.All(fields.Skip(1), f => Assert.Null(f.Value));
        Assert.Null(start.View.Note); // de peildatum verraadt "nog actief": pas bij het veld met de interlands

        var wrong = await _quiz.GuessAsync(_me, 1, "Iemand Anders");
        Assert.Equal(2, wrong.View!.Step);
        Assert.Equal("1987", wrong.View.Fiche![1].Value);
        Assert.Null(wrong.View.Fiche[2].Value);

        var done = await _quiz.GuessAsync(_me, 1, data.Answer);
        Assert.Equal(90, done.Result!.Points);
        Assert.Contains($"Club: {data.Club}", done.Result.AllHints);
    }

    [Fact]
    public async Task Fiche_gives_at_least_50_with_every_field_open()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        var data = QuestionEngine.Parse<FicheData>((await FirstQuestionAsync()).QuizItem!.Data);

        await _quiz.StartAsync(_me, 1);
        for (var i = 0; i < QuestionRules.FicheSteps; i++) NextStep(QuestionType.Fiche);
        var state = await _quiz.GetStateAsync(_me, 1);
        Assert.All(state.View!.Fiche!, f => Assert.NotNull(f.Value));

        var done = await _quiz.GuessAsync(_me, 1, data.Answer);
        Assert.Equal(50, done.Result!.Points);
    }

    [Fact]
    public async Task Fiche_wrong_name_opens_a_field_on_top_of_the_clock()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        var data = QuestionEngine.Parse<FicheData>((await FirstQuestionAsync()).QuizItem!.Data);

        await _quiz.StartAsync(_me, 1);
        NextStep(QuestionType.Fiche);
        NextStep(QuestionType.Fiche);
        var wrong = await _quiz.GuessAsync(_me, 1, "Iemand Anders");
        Assert.Equal(4, wrong.View!.Step);
        Assert.Equal("Interlands en club: stand op 1/10/2026", wrong.View.Note);

        var done = await _quiz.GuessAsync(_me, 1, data.Answer);
        Assert.Equal(70, done.Result!.Points);
    }

    [Fact]
    public async Task Fiche_ends_after_a_wrong_name_with_every_field_open()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        await _quiz.StartAsync(_me, 1);

        // Vijf foute namen openen de velden 2 tot 6; de vraag loopt nog.
        QuestionState state = null!;
        for (var i = 1; i < QuestionRules.FicheSteps; i++) state = await _quiz.GuessAsync(_me, 1, $"Fout {i}");
        Assert.Equal(QuestionStatus.InProgress, state.Status);
        Assert.Equal(QuestionRules.FicheSteps, state.View!.Step);

        state = await _quiz.GuessAsync(_me, 1, "Nog eens fout");
        Assert.Equal(QuestionStatus.Answered, state.Status);
        Assert.False(state.Result!.Correct);
        Assert.Equal(0, state.Result.Points);
    }

    [Fact]
    public async Task Fiche_ends_on_the_first_wrong_name_once_the_clock_opened_every_field()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        await _quiz.StartAsync(_me, 1);
        for (var i = 0; i < QuestionRules.FicheSteps; i++) NextStep(QuestionType.Fiche);

        var state = await _quiz.GuessAsync(_me, 1, "Fout");
        Assert.Equal(QuestionStatus.Answered, state.Status);
        Assert.Equal(0, state.Result!.Points);
        // Geen "Gepast.": de speler gokte fout.
        Assert.True(state.Result.EndedOnWrong);
        Assert.NotEqual("Gepast.", Commentary.ForResult(state.Result, 100, 1));
    }

    [Fact]
    public async Task Fiche_pass_after_a_wrong_name_still_counts_as_passing()
    {
        await PlanTodayAsync(QuestionType.Fiche);
        await _quiz.StartAsync(_me, 1);
        await _quiz.GuessAsync(_me, 1, "Fout");
        var state = await _quiz.GiveUpAsync(_me, 1);

        Assert.False(state.Result!.EndedOnWrong);
        Assert.Equal("Gepast.", Commentary.ForResult(state.Result, 100, 1));
    }

    [Fact]
    public async Task Question_with_an_old_peildatum_is_stale_and_never_picked()
    {
        // Twee vragen die al lang niet meer nagekeken zijn; de andere tien hebben geen of een verse peildatum (vandaag 17/10/2026).
        var items = _db.QuizItems.Where(i => i.Type == QuestionType.Fiche).OrderBy(i => i.Id).ToList();
        foreach (var item in items.Take(2)) item.AsOf = new DateOnly(2026, 1, 1);
        items[2].AsOf = new DateOnly(2026, 10, 1);
        await _db.SaveChangesAsync();
        var stale = items.Take(2).Select(i => i.Id).ToList();

        await PlanTodayAsync(QuestionType.Fiche);
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.Equal(GameRules.QuestionsPerDay, challenge.Questions.Count);
        Assert.DoesNotContain(challenge.Questions, q => stale.Contains(q.QuizItemId ?? 0));
        Assert.Contains(challenge.Questions, q => q.QuizItemId == items[2].Id);
    }

    [Fact]
    public async Task Player_with_an_old_peildatum_is_stale_and_never_picked()
    {
        var stale = _db.Players.OrderBy(p => p.Id).Take(40).ToList();
        foreach (var p in stale) p.AsOf = new DateOnly(2026, 1, 1);
        await _db.SaveChangesAsync();

        await PlanTodayAsync(QuestionType.WieBenIk);
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.NotEmpty(challenge.Questions);
        Assert.DoesNotContain(challenge.Questions, q => stale.Any(p => p.Id == q.PlayerId));
    }

    [Fact]
    public void Peildatum_rule_counts_days_and_no_peildatum_never_goes_stale()
    {
        var asOf = new DateOnly(2026, 10, 1);
        Assert.False(QuestionRules.IsStale(null, new DateOnly(2040, 1, 1)));
        Assert.False(QuestionRules.IsStale(asOf, asOf.AddDays(QuestionRules.MaxAgeDays)));
        Assert.True(QuestionRules.IsStale(asOf, asOf.AddDays(QuestionRules.MaxAgeDays + 1)));
    }

    [Fact]
    public async Task Kluts_scrambles_the_name_and_fixes_a_letter_per_step()
    {
        await PlanTodayAsync(QuestionType.Kluts);
        var q = await FirstQuestionAsync();
        Assert.NotNull(q.Player);

        var start = await _quiz.StartAsync(_me, 1);
        var words = start.View!.Letters!;
        var expected = q.Player!.Name.ToUpperInvariant().Split(' ');
        Assert.Equal(expected.Length, words.Count);
        for (var w = 0; w < words.Count; w++)
            Assert.Equal(expected[w].Order(), string.Concat(words[w].Select(t => t.Letter)).Order());
        Assert.All(words.SelectMany(w => w), t => Assert.False(t.Fixed));

        NextStep();
        var later = await _quiz.GetStateAsync(_me, 1);
        Assert.All(later.View!.Letters!, word =>
        {
            Assert.True(word[0].Fixed);
            Assert.Equal(1, word.Count(t => t.Fixed));
        });
        Assert.Equal(expected[0][0].ToString(), later.View.Letters![0][0].Letter);

        var done = await _quiz.GuessAsync(_me, 1, q.Player.Name);
        Assert.Equal(90, done.Result!.Points);
    }

    [Fact]
    public void Scramble_keeps_punctuation_in_place()
    {
        var words = QuestionEngine.Scramble("Michel Preud'homme", 0, 42);
        Assert.Equal("'", words[1][5].Letter);
        Assert.True(words[1][5].Fixed);
    }

    [Fact]
    public async Task Counter_counts_players_skips_without_penalty_and_keeps_partial_points()
    {
        await PlanTodayAsync(QuestionType.Counter);
        var q = await FirstQuestionAsync();
        Assert.Equal(QuestionRules.CounterPlayers, q.CounterPlayers.Count);
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        var all = challenge.Questions.SelectMany(x => x.CounterPlayerIds).ToList();
        Assert.Equal(all.Count, all.Distinct().Count()); // nooit twee keer dezelfde speler op één dag

        var start = await _quiz.StartAsync(_me, 1);
        Assert.Equal(q.CounterPlayers[0].Hints[^1], start.View!.Prompt);

        var hit = await _quiz.GuessAsync(_me, 1, q.CounterPlayers[0].Name);
        Assert.Equal(1, hit.View!.Found);
        Assert.Equal(q.CounterPlayers[1].Hints[^1], hit.View.Prompt);

        var miss = await _quiz.GuessAsync(_me, 1, "Niemand Bekend");
        Assert.Equal(0, miss.WrongGuesses);
        Assert.Equal(q.CounterPlayers[1].Hints[^1], miss.View!.Prompt);

        var skipped = await _quiz.GuessAsync(_me, 1, QuestionRules.CounterSkip);
        Assert.Equal(q.CounterPlayers[2].Hints[^1], skipped.View!.Prompt);

        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.CounterSeconds + 1));
        var end = await _quiz.GetStateAsync(_me, 1);
        Assert.Equal(QuestionRules.CounterPointsPerPlayer, end.Result!.Points);
        Assert.Equal(QuestionRules.CounterPlayers, end.Result.AllHints.Count);
        Assert.StartsWith("✅", end.Result.AllHints[0]);
        Assert.StartsWith("⏭️", end.Result.AllHints[1]);
    }

    [Fact]
    public async Task Derby_special_combines_both_clubs_without_repeating_players()
    {
        var topper = SpecialTemplates.Find("topper")!;
        var id = await _admin.SaveSpecialAsync(new SpecialEdit(0, topper.Theme, topper.Name, topper.Icon, "", _quiz.Today, _quiz.Today,
            topper.Tag, [], [QuestionType.WieBenIk, QuestionType.Kluts]));

        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        var players = challenge.Questions.Select(q => q.Player!).ToList();
        Assert.Equal(10, players.Count);
        Assert.All(players, p => Assert.True(p.Tags.Contains("anderlecht") || p.Tags.Contains("club-brugge")));
        Assert.Equal(10, players.Select(p => p.Id).Distinct().Count());
        Assert.Contains(players, p => p.Tags.Contains("anderlecht"));
        Assert.Contains(players, p => p.Tags.Contains("club-brugge"));

        var summary = (await _t.NewResults(_db).GetSpecialsAsync(_me)).Single();
        Assert.Equal("Anderlecht", summary.Affiche!.Home);
    }

    [Fact]
    public async Task Special_rejects_unknown_themes_and_too_small_counter_pools()
    {
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveSpecialAsync(new SpecialEdit(0, "custom", "X", "✨", "",
            _quiz.Today, _quiz.Today, "anderlecht,bestaat-niet", [], [QuestionType.WieBenIk])));

        // 8 Anderlecht-spelers is te weinig voor een counter van 10.
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveSpecialAsync(new SpecialEdit(0, "custom", "X", "✨", "",
            _quiz.Today, _quiz.Today, "anderlecht", [], [QuestionType.Counter])));
    }

    [Fact]
    public async Task Player_based_rounds_are_not_part_of_the_question_bank()
    {
        var edit = new QuizItemEdit(0, QuestionType.Kluts, "{}", 2, [], [], PlayerStatus.Review, null, null, null, 0);
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveQuizItemAsync(edit, "sander"));
    }

    [Fact]
    public void Commentary_matches_the_result()
    {
        Assert.Contains(Commentary.ForResult(true, 100, 100, 1), Commentary.Perfect);
        Assert.Contains(Commentary.ForResult(true, 80, 100, 1), Commentary.Goal);
        Assert.Contains(Commentary.ForResult(false, 50, 100, 1), Commentary.Almost);
        Assert.Contains(Commentary.ForResult(false, 25, 100, 1), Commentary.Partial); // minder dan de helft is geen "bijna"
        Assert.Contains(Commentary.ForResult(false, 0, 100, -3), Commentary.Missed);
    }

    public void Dispose() => _t.Dispose();
}
