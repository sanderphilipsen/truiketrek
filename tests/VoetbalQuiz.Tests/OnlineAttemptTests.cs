using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Karakteriseringstests van de online pogingflow per vraagtype (gokken, passen, tijd om, speling).
/// Ze leggen het huidige gedrag vast als vangnet voor het samenvoegen van de poginglogica (refactorplan B1).
/// </summary>
public sealed class OnlineAttemptTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly MatchService _matches;
    private readonly string _host, _guest;

    public OnlineAttemptTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        // Genoeg spelers voor drie counters van tien.
        for (var i = 1; i <= 35; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        for (var i = 0; i < 6; i++)
        {
            Add(QuestionType.Jaar, $"J{i}", new JaarData($"Gebeurtenis {i}", 2000 + i));
            Add(QuestionType.Opstelling, $"O{i}", new OpstellingData($"Finale {i}", "Ploeg",
                [.. Enumerable.Range(0, 11).Select(n => new LineupPlayer($"Lid{(char)('A' + n)} Ploeg{i}", [], n == 0 ? "K" : "V"))]));
        }
        _host = AddUser("host");
        _guest = AddUser("gast");

        _matches = _t.Matches();
    }

    private void Add<T>(QuestionType type, string key, T data) =>
        _db.QuizItems.Add(new QuizItem { Type = type, Key = key, Data = QuestionEngine.Serialize(data), Status = PlayerStatus.Approved });

    private string AddUser(string name) => _t.AddUser(name);

    private async Task Tick(double seconds)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        await _matches.AdvanceAllAsync();
    }

    private async Task<int> StartedAsync(QuestionType type)
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [type], Questions: 3));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);
        return id;
    }

    private async Task<MatchQuestion> CurrentAsync(int matchId)
    {
        _db.ChangeTracker.Clear();
        var match = await _db.Matches.Include(m => m.Questions).ThenInclude(q => q.QuizItem)
            .Include(m => m.Questions).ThenInclude(q => q.Player)
            .SingleAsync(m => m.Id == matchId);
        return MatchService.CurrentQuestion(match)!;
    }

    private async Task<List<string>> BlankNamesAsync(int matchId)
    {
        var q = await CurrentAsync(matchId);
        var data = QuestionEngine.Parse<OpstellingData>(q.QuizItem!.Data);
        return q.Blanks.Select(i => data.Players[i].Name).ToList();
    }

    private async Task<List<string>> CounterNamesAsync(int matchId)
    {
        var q = await CurrentAsync(matchId);
        var players = await _db.Players.Where(p => q.CounterPlayerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return q.CounterPlayerIds.Select(id => players[id].Name).ToList();
    }

    [Fact]
    public async Task Opstelling_counts_found_names_without_penalty_and_keeps_partial_points()
    {
        var id = await StartedAsync(QuestionType.Opstelling);
        var blanks = await BlankNamesAsync(id);
        Assert.Equal(QuestionRules.OpstellingBlanks, blanks.Count);

        var wrong = await _matches.GuessAsync(_host, id, "Niemand");
        Assert.Equal(0, wrong.MyWrongGuesses); // zoeken naar namen kost niets
        Assert.False(wrong.MyDone);

        await _matches.GuessAsync(_host, id, blanks[0]);
        var two = await _matches.GuessAsync(_host, id, blanks[1]);
        Assert.False(two.MyDone);
        Assert.Equal(2, two.View!.Lineup!.Count(s => s.IsBlank && s.Name is not null));

        // Passen ("Ik ben klaar") houdt de deelpunten.
        await _matches.GuessAsync(_guest, id, blanks[0]);
        var stopped = await _matches.GiveUpAsync(_guest, id);
        Assert.True(stopped.MyDone);
        Assert.False(stopped.MyCorrect);

        // De host laat de tijd lopen: ook dan de deelpunten.
        await Tick(QuestionRules.OpstellingSeconds + 1);
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(QuestionRules.OpstellingPoints(2, QuestionRules.OpstellingBlanks), reveal.MyPoints);
        Assert.Equal(QuestionRules.OpstellingPoints(1, QuestionRules.OpstellingBlanks),
            (await _matches.GetStateAsync(_guest, id)).MyPoints);
        Assert.Equal(11, reveal.Result!.FullLineup!.Count);
    }

    [Fact]
    public async Task Opstelling_all_names_found_is_correct_and_gets_the_position_bonus()
    {
        var id = await StartedAsync(QuestionType.Opstelling);
        AnswerSnapshot last = default;
        foreach (var name in await BlankNamesAsync(id))
        {
            var s = await _matches.GuessAsync(_host, id, name);
            last = new(s.MyDone, s.MyCorrect);
        }
        Assert.Equal(new AnswerSnapshot(true, true), last);

        await _matches.GiveUpAsync(_guest, id);
        await _matches.AdvanceAllAsync(); // iedereen klaar: vraag stopt meteen
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(QuestionTypes.Get(QuestionType.Opstelling).MaxPoints + MatchRules.BonusFor(1), reveal.MyPoints);
    }

    private readonly record struct AnswerSnapshot(bool Done, bool Correct);

    /// <summary>
    /// Huidig gedrag (bevinding B1): online is het aantal pogingen niet begrensd. De eigenaar besliste dat online dezelfde
    /// limiet krijgt als de daily (<see cref="GameRules.MaxGuessesPerQuestion"/>); deze test verandert dus bewust in stap 3.1.
    /// </summary>
    [Fact]
    public async Task Opstelling_online_has_no_guess_limit_yet()
    {
        var id = await StartedAsync(QuestionType.Opstelling);
        var blanks = await BlankNamesAsync(id);
        for (var i = 0; i < GameRules.MaxGuessesPerQuestion + 5; i++)
            await _matches.GuessAsync(_host, id, $"Gok {i}");

        var after = await _matches.GuessAsync(_host, id, blanks[0]);
        Assert.False(after.MyDone);
        Assert.Equal(1, after.View!.Lineup!.Count(s => s.IsBlank && s.Name is not null));
    }

    [Fact]
    public async Task Counter_finds_and_skips_players_and_keeps_partial_points_when_time_runs_out()
    {
        var id = await StartedAsync(QuestionType.Counter);
        var names = await CounterNamesAsync(id);
        Assert.Equal(QuestionRules.CounterPlayers, names.Count);

        var start = await _matches.GetStateAsync(_host, id);
        Assert.Equal("Speler 1 van 10", start.View!.Note);

        var wrong = await _matches.GuessAsync(_host, id, "Niemand");
        Assert.Equal(0, wrong.MyWrongGuesses); // snelheidsronde: geen straf
        Assert.Equal("Speler 1 van 10", wrong.View!.Note);

        await _matches.GuessAsync(_host, id, names[0]);
        await _matches.GuessAsync(_host, id, QuestionRules.CounterSkip);
        var third = await _matches.GuessAsync(_host, id, names[2]);
        Assert.Equal(2, third.View!.Found);
        Assert.Equal("Speler 4 van 10", third.View.Note);
        Assert.False(third.MyDone);

        await Tick(QuestionRules.CounterSeconds + 1);
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(QuestionRules.CounterPoints(2), reveal.MyPoints);
        Assert.Equal(0, (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    [Fact]
    public async Task Counter_is_done_when_every_player_is_found_or_skipped()
    {
        var id = await StartedAsync(QuestionType.Counter);
        var names = await CounterNamesAsync(id);

        await _matches.GuessAsync(_host, id, names[0]);
        MatchState state = null!;
        for (var i = 1; i < names.Count; i++) state = await _matches.GuessAsync(_host, id, QuestionRules.CounterSkip);
        Assert.True(state.MyDone);
        Assert.False(state.MyCorrect);

        // Passen bij een counter houdt de deelpunten.
        await _matches.GuessAsync(_guest, id, names[0]);
        await _matches.GuessAsync(_guest, id, names[1]);
        await _matches.GiveUpAsync(_guest, id);
        await _matches.AdvanceAllAsync();
        Assert.Equal(QuestionRules.CounterPoints(1), (await _matches.GetStateAsync(_host, id)).MyPoints);
        Assert.Equal(QuestionRules.CounterPoints(2), (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    [Fact]
    public async Task Jaar_ignores_invalid_years_and_gives_points_for_being_close()
    {
        var id = await StartedAsync(QuestionType.Jaar);
        var year = QuestionEngine.Parse<JaarData>((await CurrentAsync(id)).QuizItem!.Data).Year;

        var invalid = await _matches.GuessAsync(_host, id, "1800");
        Assert.False(invalid.MyDone);
        Assert.Equal(0, invalid.MyWrongGuesses);

        var close = await _matches.GuessAsync(_host, id, (year + 1).ToString());
        Assert.True(close.MyDone);
        Assert.False(close.MyCorrect);
        var again = await _matches.GuessAsync(_host, id, year.ToString()); // één keuze: daarna telt niets meer
        Assert.False(again.MyCorrect);

        await _matches.GuessAsync(_guest, id, year.ToString());
        await _matches.AdvanceAllAsync();
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(QuestionRules.JaarPoints(year + 1, year), reveal.MyPoints); // geen positiebonus: niet juist
        Assert.Equal(year, reveal.Result!.CorrectYear);
        Assert.Equal(year + 1, reveal.Result.MyYear);
        Assert.Equal(100 + MatchRules.BonusFor(1), (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    [Fact]
    public async Task An_answer_within_the_latency_grace_still_counts_but_not_after()
    {
        var id = await StartedAsync(QuestionType.WieBenIk);
        var answer = (await CurrentAsync(id)).Player!.Name;

        // Niet via Tick: de engine zou de vraag na de speling afsluiten.
        _clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + GameRules.LatencyGraceSeconds - 0.1));
        var inTime = await _matches.GuessAsync(_host, id, answer);
        Assert.True(inTime.MyCorrect);

        _clock.Advance(TimeSpan.FromSeconds(0.2));
        var late = await _matches.GuessAsync(_guest, id, answer);
        Assert.False(late.MyDone);
        Assert.False(late.MyCorrect);

        await _matches.AdvanceAllAsync();
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(GameRules.LastChancePoints + MatchRules.BonusFor(1), reveal.MyPoints);
        Assert.Equal(0, (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    [Fact]
    public async Task Wrong_names_cost_points_and_giving_up_scores_nothing()
    {
        var id = await StartedAsync(QuestionType.WieBenIk);
        var answer = (await CurrentAsync(id)).Player!.Name;

        var wrong = await _matches.GuessAsync(_host, id, "Iemand");
        Assert.Equal(1, wrong.MyWrongGuesses);
        Assert.Equal("Iemand", wrong.MyLastWrongGuess);
        var empty = await _matches.GuessAsync(_host, id, "   ");
        Assert.Equal(1, empty.MyWrongGuesses); // een lege gok telt niet

        var gaveUp = await _matches.GiveUpAsync(_guest, id);
        Assert.True(gaveUp.MyDone);
        var after = await _matches.GuessAsync(_guest, id, answer); // na passen telt niets meer
        Assert.False(after.MyCorrect);

        await _matches.GuessAsync(_host, id, answer);
        await _matches.AdvanceAllAsync();
        Assert.Equal(100 - GameRules.WrongGuessPenalty + MatchRules.BonusFor(1), (await _matches.GetStateAsync(_host, id)).MyPoints);
        Assert.Equal(0, (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    public void Dispose() => _t.Dispose();
}
