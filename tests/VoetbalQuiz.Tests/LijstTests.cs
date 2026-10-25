using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class LijstTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly string _me;

    private static readonly LijstData Winners = new("Alle winnaars sinds 2016",
    [
        new("2016", "Cristiano Ronaldo"), new("2017", "Cristiano Ronaldo"), new("2018", "Luka Modrić"), new("2019", "Lionel Messi"),
        new("2020", Note: "niet uitgereikt"), new("2021", "Lionel Messi"), new("2022", "Ronaldo"),
    ],
    [
        new("Ronaldo", ["R9"]), new("Cristiano Ronaldo", ["CR7"]), new("Luka Modrić", ["Modric"]), new("Lionel Messi", ["Messi"]),
    ]);

    public LijstTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);
        for (var i = 1; i <= 12; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        _db.QuizItems.Add(new QuizItem
        {
            Type = QuestionType.Lijst, Key = Winners.Question, Data = QuestionEngine.Serialize(Winners), Status = PlayerStatus.Approved,
            Tags = ["ballon-dor"],
        });
        var user = new AppUser { UserName = "sander" };
        _db.Users.Add(user);
        _db.SaveChanges();
        (_me, _quiz) = (user.Id, _t.Quiz);
    }

    public void Dispose() => _t.Dispose();

    private async Task<int> SpecialAsync() =>
        await _t.Admin.SaveSpecialAsync(new SpecialEdit(0, "ballon-dor", "Ballon d'Or Special", "⚽", "", _quiz.Today, _quiz.Today,
            "ballon-dor", [], [QuestionType.Lijst]));

    private static int Found(QuestionState s) => s.View!.Found;

    [Fact]
    public void Lijst_is_one_solo_question_of_a_full_day()
    {
        var info = QuestionTypes.Get(QuestionType.Lijst);
        Assert.True(info.Alone);
        Assert.Equal(QuestionTypes.DayMaxPoints, info.PerDay * info.MaxPoints);
        Assert.Equal(200, QuestionRules.LijstSeconds);
        Assert.True(QuestionRules.PartialScores(QuestionType.Lijst));
        Assert.Equal(500, QuestionRules.LijstPoints(2, 4));
    }

    [Fact]
    public async Task A_special_with_only_lijst_is_that_one_list()
    {
        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(await SpecialAsync());
        var q = Assert.Single(challenge.Questions);
        Assert.Equal(QuestionType.Lijst, q.Type);
    }

    [Fact]
    public async Task The_list_shows_only_the_labels_until_a_name_is_found()
    {
        var id = await SpecialAsync();
        var start = await _quiz.StartAsync(_me, 1, id);
        Assert.Equal(QuestionRules.LijstSeconds, start.TimeLimit);
        Assert.Equal((0, 4), (start.View!.Found, start.View.Total));
        Assert.Equal(["2016", "2017", "2018", "2019", "2020", "2021", "2022"], start.View.Rows!.Select(r => r.Label));
        // Enkel de rij zonder winnaar toont iets; geen enkele naam lekt.
        Assert.Equal(["niet uitgereikt"], start.View.Rows.Where(r => r.Name is not null).Select(r => r.Name));
        Assert.False(start.View.Rows.Single(r => r.Label == "2020").Open);
    }

    [Fact]
    public async Task One_name_fills_all_its_years_and_counts_once()
    {
        var id = await SpecialAsync();
        await _quiz.StartAsync(_me, 1, id);
        var messi = await _quiz.GuessAsync(_me, 1, "messi", id);
        Assert.Equal(1, Found(messi));
        Assert.Equal(["2019", "2021"], messi.View!.Rows!.Where(r => r.Name == "Lionel Messi").Select(r => r.Label));
        Assert.Equal(3, messi.View.Rows.Count(r => r.Name is not null)); // twee keer Messi en "niet uitgereikt"

        // Nog eens Messi of een naam die er niet in staat: niets erbij, en geen strafpunten.
        var again = await _quiz.GuessAsync(_me, 1, "Lionel Messi", id);
        var wrong = await _quiz.GuessAsync(_me, 1, "Eden Hazard", id);
        Assert.Equal((1, 1), (Found(again), Found(wrong)));
        Assert.Equal(0, wrong.WrongGuesses);
    }

    [Fact]
    public async Task Typing_ronaldo_twice_does_not_give_cristiano_ronaldo()
    {
        var id = await SpecialAsync();
        await _quiz.StartAsync(_me, 1, id);
        var r9 = await _quiz.GuessAsync(_me, 1, "Ronaldo", id);
        Assert.Equal("Ronaldo", r9.View!.Rows!.Single(r => r.Label == "2022").Name);
        Assert.Null(r9.View.Rows.Single(r => r.Label == "2016").Name);

        var twice = await _quiz.GuessAsync(_me, 1, "ronaldo", id);
        Assert.Equal(1, Found(twice));
        var cr7 = await _quiz.GuessAsync(_me, 1, "Cristiano Ronaldo", id);
        Assert.Equal(2, Found(cr7));
    }

    [Fact]
    public async Task All_names_is_correct_and_the_full_points()
    {
        var id = await SpecialAsync();
        await _quiz.StartAsync(_me, 1, id);
        QuestionState state = null!;
        foreach (var name in new[] { "CR7", "Modric", "Messi", "R9" }) state = await _quiz.GuessAsync(_me, 1, name, id);
        Assert.True(state.Result!.Correct);
        Assert.Equal(QuestionTypes.DayMaxPoints, state.Result.Points);
        Assert.All(state.Result.FullList!.Where(r => r.Open), r => Assert.True(r.Mine));
    }

    [Fact]
    public async Task Stopping_or_running_out_of_time_keeps_the_names_found()
    {
        var id = await SpecialAsync();
        await _quiz.StartAsync(_me, 1, id);
        await _quiz.GuessAsync(_me, 1, "Messi", id);
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.LijstSeconds + 1));
        var end = await _quiz.GetStateAsync(_me, 1, id);
        Assert.Equal(QuestionRules.LijstPoints(1, 4), end.Result!.Points);
        Assert.False(end.Result.Correct);
        // Na afloop staat de hele lijst er, met wat jij vond en wat je miste.
        var full = end.Result.FullList!;
        Assert.Equal("Cristiano Ronaldo", full[0].Name);
        Assert.Equal((true, false), (full.Single(r => r.Label == "2019").Mine!.Value, full[0].Mine!.Value));
        Assert.Null(full.Single(r => r.Label == "2020").Mine);
    }

    [Fact]
    public async Task Giving_up_keeps_the_partial_points()
    {
        var id = await SpecialAsync();
        await _quiz.StartAsync(_me, 1, id);
        await _quiz.GuessAsync(_me, 1, "Modric", id);
        await _quiz.GuessAsync(_me, 1, "Messi", id);
        var end = await _quiz.GiveUpAsync(_me, 1, id);
        Assert.Equal(500, end.Result!.Points);
    }

    [Fact]
    public void A_list_must_be_consistent()
    {
        static List<string> Problems(LijstData d) => QuestionEngine.Problems(QuestionType.Lijst, QuestionEngine.Serialize(d));
        Assert.Empty(Problems(Winners));
        Assert.NotEmpty(Problems(Winners with { Rows = [.. Winners.Rows, new LijstRij("2023", "Rodri")] })); // niet bij de namen
        Assert.NotEmpty(Problems(Winners with { Names = [.. Winners.Names, new LijstNaam("Rodri", [])] })); // in geen enkele rij
        Assert.NotEmpty(Problems(Winners with { Rows = [.. Winners.Rows, new LijstRij("2024")] })); // geen naam en geen uitleg
    }
}
