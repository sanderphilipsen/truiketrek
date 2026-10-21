using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Gelijktijdige schrijvers op één wedstrijd (bevinding B5, stap 3.3): de engine en verzoeken werken elk op hun eigen
/// context. Een interceptor laat de andere schrijver net vóór het bewaren tussenkomen; <see cref="Match.Version"/>
/// moet dat opvangen en het verzoek of de engine opnieuw laten beginnen vanaf een verse toestand.
/// </summary>
public sealed class MatchConcurrencyTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly MatchService _matches;
    private readonly string _host, _guest;

    public MatchConcurrencyTests()
    {
        for (var i = 1; i <= 10; i++)
            _t.Db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        _host = _t.AddUser("host");
        _guest = _t.AddUser("gast");
        _matches = _t.Matches();
    }

    /// <summary>Voert <paramref name="action"/> één keer uit, net voor de eerste SaveChanges van de context.</summary>
    private sealed class BeforeFirstSave(Func<Task> action) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await action();
            }
            return result;
        }
    }

    private async Task<(int Id, string Answer)> StartedAsync()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.WieBenIk], Questions: 3));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        _t.Clock.Advance(TimeSpan.FromSeconds(MatchRules.CountdownSeconds));
        await _matches.AdvanceAllAsync();
        var question = await _t.Db.MatchQuestions.Include(q => q.Player).SingleAsync(q => q.MatchId == id && q.Number == 1);
        return (id, question.Player!.Name);
    }

    private async Task<Match> FreshAsync(int id)
    {
        await using var db = _t.NewContext();
        return await db.Matches.Include(m => m.Players).Include(m => m.Questions).ThenInclude(q => q.Answers)
            .AsSplitQuery().SingleAsync(m => m.Id == id);
    }

    [Fact]
    public async Task A_guess_loaded_before_the_engine_ends_the_question_does_not_count_after_it()
    {
        var (id, answer) = await StartedAsync();
        _t.Clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + GameRules.LatencyGraceSeconds - 0.1));

        // Het verzoek laadt de wedstrijd terwijl de vraag nog loopt; net voor het bewaren sluit de engine ze af.
        await using var requestDb = _t.NewContext(new BeforeFirstSave(async () =>
        {
            _t.Clock.Advance(TimeSpan.FromSeconds(0.2));
            Assert.Equal(1, await _matches.AdvanceAllAsync());
        }));
        var state = await _t.NewMatches(requestDb).GuessAsync(_guest, id, answer);

        Assert.False(state.MyCorrect);
        var match = await FreshAsync(id);
        var question = match.Questions.Single(q => q.Number == 1);
        Assert.NotNull(question.EndedAt);
        Assert.DoesNotContain(question.Answers, a => a.UserId == _guest && a.Correct);
        Assert.Equal(0, match.Players.Single(p => p.UserId == _guest).Score);
    }

    [Fact]
    public async Task The_engine_starts_over_when_a_player_leaves_while_it_settles_a_question()
    {
        var (id, answer) = await StartedAsync();
        await _matches.GuessAsync(_host, id, answer);
        await _matches.GuessAsync(_guest, id, answer);

        // De engine laadt de wedstrijd met beide antwoorden; net voor het bewaren vertrekt de gast.
        await using var engineDb = _t.NewContext(new BeforeFirstSave(() => _matches.LeaveAsync(_guest, id)));
        Assert.Equal(1, await _t.NewMatches(engineDb).AdvanceAllAsync());

        var match = await FreshAsync(id);
        var guest = match.Players.Single(p => p.UserId == _guest);
        var host = match.Players.Single(p => p.UserId == _host);
        var answers = match.Questions.Single(q => q.Number == 1).Answers;
        Assert.NotNull(guest.LeftAt);
        Assert.Equal(0, guest.Score); // zonder herladen hield de vertrekker de punten van de engine
        Assert.Null(answers.Single(a => a.UserId == _guest).Position);
        Assert.Equal(1, answers.Single(a => a.UserId == _host).Position);
        Assert.Equal(answers.Single(a => a.UserId == _host).Points + MatchRules.BonusFor(1), host.Score);
    }

    [Fact]
    public async Task Of_two_guesses_at_the_same_time_from_one_player_only_one_counts()
    {
        var (id, _) = await StartedAsync();

        await using var requestDb = _t.NewContext(new BeforeFirstSave(() => _matches.GuessAsync(_guest, id, "Eerste tik")));
        await _t.NewMatches(requestDb).GuessAsync(_guest, id, "Tweede tik");

        var mine = (await FreshAsync(id)).Questions.Single(q => q.Number == 1).Answers.Single(a => a.UserId == _guest);
        Assert.Equal(1, mine.WrongGuesses);
        Assert.Equal(1, mine.Guesses);
        Assert.Equal("Eerste tik", mine.LastGuess);
    }

    /// <summary>Laat de eerste SaveChanges falen zoals PostgreSQL een deadlock afbreekt (40P01).</summary>
    private sealed class DeadlockOnFirstSave : SaveChangesInterceptor
    {
        private bool _done;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (_done) return ValueTask.FromResult(result);
            _done = true;
            throw new DbUpdateException("deadlock", new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected));
        }
    }

    [Fact]
    public async Task A_weapon_rolls_back_its_claim_and_succeeds_when_postgres_breaks_a_deadlock()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.WieBenIk], Derby: true, Questions: 3));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        _t.Clock.Advance(TimeSpan.FromSeconds(MatchRules.CountdownSeconds));
        await _matches.AdvanceAllAsync();
        _t.Clock.Advance(TimeSpan.FromSeconds(GameRules.TimeLimitSeconds + GameRules.LatencyGraceSeconds + 1));
        await _matches.AdvanceAllAsync();
        _t.Clock.Advance(TimeSpan.FromSeconds(MatchRules.RevealSeconds));
        await _matches.AdvanceAllAsync(); // vraag 2: wapens mogen
        _t.Db.MatchPlayers.Single(p => p.MatchId == id && p.UserId == _guest).Weapon = DerbyWeapon.Gokchinees;
        await _t.Db.SaveChangesAsync();

        // De claim (ExecuteUpdate) lukt, het bewaren erna niet: zonder terugdraaien zou de herpoging "al gebruikt" zeggen.
        await using var requestDb = _t.NewContext(new DeadlockOnFirstSave());
        await _t.NewMatches(requestDb).UseWeaponAsync(_guest, id, null);

        var match = await FreshAsync(id);
        Assert.NotNull(match.Players.Single(p => p.UserId == _guest).WeaponUsedAt);
        Assert.True(match.Questions.Single(q => q.Number == 2).Answers.Single(a => a.UserId == _guest).Gamble);
    }

    public void Dispose() => _t.Dispose();
}
