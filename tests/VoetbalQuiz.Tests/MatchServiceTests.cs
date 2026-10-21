using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class MatchServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly ResultsService _results;
    private readonly MatchService _matches;
    private readonly OnlineService _online;
    private readonly string _host, _guest, _third;

    public MatchServiceTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        _host = AddUser("host");
        _guest = AddUser("gast");
        _third = AddUser("derde");

        _results = _t.Results;
        _matches = _t.Matches();
        _online = _t.Online;
    }

    private string AddUser(string name) => _t.AddUser(name);

    private async Task<string> CurrentAnswerAsync(int matchId)
    {
        var match = await _db.Matches.Include(m => m.Questions).ThenInclude(q => q.Player).SingleAsync(m => m.Id == matchId);
        return MatchService.CurrentQuestion(match)!.Player!.Name;
    }

    private async Task Tick(double seconds)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        await _matches.AdvanceAllAsync();
    }

    private async Task<int> StartedMatchAsync(params string[] guests)
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4));
        foreach (var g in guests) await _matches.JoinAsync(g, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);
        return id;
    }

    [Fact]
    public async Task Lobby_shows_open_tables_and_respects_seats()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest("Café", 2));

        var lobby = await _matches.GetLobbyAsync(_guest);
        Assert.Equal("Café", Assert.Single(lobby.Open).Name);

        await _matches.JoinAsync(_guest, id);
        await Assert.ThrowsAsync<QuizException>(() => _matches.JoinAsync(_third, id)); // vol
        Assert.Empty((await _matches.GetLobbyAsync(_third)).Open);
    }

    [Fact]
    public async Task Only_host_can_start_with_at_least_two_players()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4));
        await Assert.ThrowsAsync<QuizException>(() => _matches.StartAsync(_host, id));

        await _matches.JoinAsync(_guest, id);
        await Assert.ThrowsAsync<QuizException>(() => _matches.StartAsync(_guest, id));

        await _matches.StartAsync(_host, id);
        var state = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Countdown, state.Phase);
        Assert.Empty(state.Hints);
    }

    [Fact]
    public async Task First_correct_answer_gets_the_biggest_bonus()
    {
        var id = await StartedMatchAsync(_guest);
        var answer = await CurrentAnswerAsync(id);

        await _matches.GuessAsync(_guest, id, answer);     // eerst: 100 + 50
        _clock.Advance(TimeSpan.FromSeconds(6));
        await _matches.GuessAsync(_host, id, "fout");
        await _matches.GuessAsync(_host, id, answer);      // tweede: 90 - 5 + 30
        await _matches.AdvanceAllAsync();                  // iedereen klaar: vraag stopt meteen

        var state = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, state.Phase);
        Assert.Equal(answer, state.RevealedPlayer);
        Assert.Equal(["gast", "host"], state.Reveal.Select(r => r.UserName));
        Assert.Equal(150, state.Reveal[0].Points);
        Assert.Equal(115, state.Reveal[1].Points);
    }

    [Fact]
    public async Task Question_ends_after_time_limit_and_next_one_follows_reveal()
    {
        var id = await StartedMatchAsync(_guest);
        await Tick(GameRules.TimeLimitSeconds + 1);
        Assert.Equal(MatchPhase.Reveal, (await _matches.GetStateAsync(_host, id)).Phase);

        // Te laat antwoorden telt niet.
        var late = await _matches.GuessAsync(_guest, id, await CurrentAnswerAsync(id));
        Assert.Empty(late.Reveal);

        await Tick(MatchRules.RevealSeconds);
        var next = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Question, next.Phase);
        Assert.Equal(2, next.CurrentNumber);
        Assert.Single(next.Hints);
    }

    [Fact]
    public async Task Full_match_ends_with_ranking_rating_and_xp()
    {
        var id = await StartedMatchAsync(_guest, _third);
        for (var n = 1; n <= MatchRules.DefaultQuestions; n++)
        {
            var answer = await CurrentAnswerAsync(id);
            await _matches.GuessAsync(_host, id, answer);
            await _matches.GiveUpAsync(_guest, id);
            await _matches.GiveUpAsync(_third, id);
            await Tick(0.1);                       // vraag sluit af
            await Tick(MatchRules.RevealSeconds);  // volgende vraag of einde
        }

        var final = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Finished, final.Phase);
        var winner = final.Players.Single(p => p.Rank == 1);
        Assert.Equal("host", winner.UserName);
        Assert.Equal(10 * 150, winner.Score);
        Assert.All(final.Players.Where(p => p.UserName != "host"), p => Assert.Equal(2, p.Rank)); // gedeelde plaats

        var board = await _online.GetRatingLeaderboardAsync(_host);
        Assert.Equal("host", board.Entries[0].UserName);
        Assert.True(board.Entries[0].Value > MatchRules.StartRating);
        Assert.True(board.Entries[^1].Value < MatchRules.StartRating);

        var stats = await _online.GetStatsAsync(_host);
        Assert.Equal(1, stats.Me.Wins);
        Assert.Equal(10, stats.Me.FirstCorrect);
        Assert.Contains(stats.HallOfFame, c => c.Title == "Snelste schutter" && c.IsMe);

        var level = await _results.LevelOfAsync(_host);
        Assert.Equal(Levels.XpPerMatch + Levels.XpPerMatchWon, level.Xp);
    }

    [Fact]
    public async Task Leaving_players_do_not_block_the_question()
    {
        var id = await StartedMatchAsync(_guest);
        await _matches.LeaveAsync(_guest, id);
        await _matches.GuessAsync(_host, id, await CurrentAnswerAsync(id));
        await Tick(0.1);
        Assert.Equal(MatchPhase.Reveal, (await _matches.GetStateAsync(_host, id)).Phase);
    }

    /// <summary>Speelt de vragen af: de host antwoordt juist (als hij er nog is), de rest laat de tijd lopen.</summary>
    private async Task PlayOutAsync(int id)
    {
        for (var guard = 0; guard < 100 && (await _matches.GetStateAsync(_host, id)).Phase != MatchPhase.Finished; guard++)
        {
            var state = await _matches.GetStateAsync(_host, id);
            if (state.Phase == MatchPhase.Question && state.IAmIn && !state.MyDone)
                await _matches.GuessAsync(_host, id, await CurrentAnswerAsync(id));
            await Tick(GameRules.TimeLimitSeconds + 1);
            await Tick(MatchRules.RevealSeconds);
        }
    }

    [Fact]
    public async Task Host_chooses_the_number_of_questions()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, Questions: 4));
        Assert.Equal(4, Assert.Single((await _matches.GetLobbyAsync(_guest)).Open).Questions);
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        Assert.Equal(4, (await _matches.GetStateAsync(_host, id)).TotalQuestions);

        // Buiten de grenzen wordt het afgerond naar het minimum of maximum.
        var few = await _matches.CreateAsync(_guest, "gast", new CreateMatchRequest(null, 4, Questions: 1));
        Assert.Equal(MatchRules.MinQuestions, (await _matches.GetStateAsync(_guest, few)).TotalQuestions);
    }

    [Fact]
    public void Mix_with_a_count_spreads_the_questions_over_the_types()
    {
        var mix = QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Snelvuur, QuestionType.Opstelling], 7);
        Assert.Equal(7, mix.Values.Sum());
        Assert.Equal([3, 2, 2], mix.Values);

        // Minstens één vraag per gekozen type.
        Assert.Equal(5, MatchRules.ClampQuestions(3, types: 5));
    }

    [Fact]
    public async Task Leaving_a_running_match_costs_all_points_and_ranks_you_last()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, Questions: 3));
        await _matches.JoinAsync(_guest, id);
        await _matches.JoinAsync(_third, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);

        await _matches.GuessAsync(_host, id, await CurrentAnswerAsync(id));
        await Tick(GameRules.TimeLimitSeconds + 1);
        Assert.True((await _matches.GetStateAsync(_host, id)).Players.Single(p => p.IsMe).Score > 0);

        await _matches.LeaveAsync(_host, id);
        Assert.Equal(0, (await _matches.GetStateAsync(_guest, id)).Players.Single(p => p.UserName == "host").Score);

        await PlayOutAsync(id);
        var final = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Finished, final.Phase);
        Assert.Equal(3, final.Players.Single(p => p.UserName == "host").Rank);
        Assert.All(final.Players.Where(p => p.UserName != "host"), p => Assert.Equal(1, p.Rank));
    }

    [Fact]
    public async Task Last_player_left_wins_and_leavers_rank_by_who_stayed_longest()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, Questions: 3));
        await _matches.JoinAsync(_guest, id);
        await _matches.JoinAsync(_third, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);

        // Gast en derde scoren op vraag 1; de host niet.
        var answer = await CurrentAnswerAsync(id);
        await _matches.GuessAsync(_guest, id, answer);
        await _matches.GuessAsync(_third, id, answer);
        await Tick(GameRules.TimeLimitSeconds + 1);

        await _matches.LeaveAsync(_guest, id);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await _matches.LeaveAsync(_third, id);

        // De host speelt alleen verder en wint zowiezo, ook al had hij minder punten.
        Assert.Equal(MatchStatus.Running, (await _matches.GetStateAsync(_host, id)).Status);
        await PlayOutAsync(id);
        var final = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Finished, final.Phase);
        Assert.Equal(["host", "derde", "gast"], final.Players.OrderBy(p => p.Rank).Select(p => p.UserName));
        Assert.Equal([1, 2, 3], final.Players.OrderBy(p => p.Rank).Select(p => p.Rank!.Value));

        // Maar als enige overgebleven telt het niet als zege: geen rating erbij, geen zege, geen winst-XP.
        // Wie vertrok, verliest wel rating.
        var board = await _online.GetRatingLeaderboardAsync(_host);
        var host = board.Entries.Single(e => e.UserName == "host");
        Assert.Equal((MatchRules.StartRating, 0), (host.Value, host.Wins));
        Assert.True(board.Entries.Single(e => e.UserName == "gast").Value < MatchRules.StartRating);
        Assert.True(board.Entries.Single(e => e.UserName == "derde").Value <= MatchRules.StartRating); // verliest van host, wint van gast
        Assert.Equal(0, (await _online.GetStatsAsync(_host)).Me.Wins);
        Assert.Equal(Levels.XpPerMatch, (await _results.LevelOfAsync(_host)).Xp);
    }

    [Fact]
    public async Task Host_leaving_lobby_cancels_the_table()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4));
        await _matches.JoinAsync(_guest, id);
        await _matches.LeaveAsync(_host, id);
        Assert.Equal(MatchPhase.Cancelled, (await _matches.GetStateAsync(_guest, id)).Phase);
    }

    private void AddSnelvuur(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var data = new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], i % 4);
            _db.QuizItems.Add(new QuizItem
            {
                Type = QuestionType.Snelvuur, Key = data.Question, Data = QuestionEngine.Serialize(data), Status = PlayerStatus.Approved,
            });
        }
        _db.SaveChanges();
    }

    private async Task<SnelvuurData> CurrentSnelvuurAsync(int matchId)
    {
        var match = await _db.Matches.Include(m => m.Questions).ThenInclude(q => q.QuizItem).SingleAsync(m => m.Id == matchId);
        return QuestionEngine.Parse<SnelvuurData>(MatchService.CurrentQuestion(match)!.QuizItem!.Data);
    }

    [Fact]
    public async Task Table_plays_the_chosen_question_type()
    {
        AddSnelvuur(12);
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.Snelvuur]));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);

        var state = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(QuestionType.Snelvuur, state.Type);
        Assert.Equal(QuestionRules.SnelvuurSeconds, state.TimeLimit);
        Assert.Equal(4, state.View!.Options!.Count);
        Assert.Equal([QuestionType.Snelvuur], state.Types);

        var q = await CurrentSnelvuurAsync(id);
        await _matches.GuessAsync(_guest, id, q.Correct.ToString());              // juist: 100 + 50
        var wrong = await _matches.GuessAsync(_host, id, ((q.Correct + 1) % 4).ToString()); // fout gekozen: meteen klaar
        Assert.True(wrong.MyDone);
        Assert.False(wrong.MyCorrect);

        await _matches.AdvanceAllAsync(); // iedereen klaar: vraag stopt meteen
        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(q.Options[q.Correct], reveal.RevealedPlayer);
        Assert.Equal(q.Correct, reveal.Result!.CorrectIndex);
        Assert.Equal(150, Assert.Single(reveal.Reveal).Points);
    }

    [Fact]
    public async Task Only_active_types_with_enough_questions_can_be_chosen()
    {
        AddSnelvuur(12);
        _db.DisabledQuestionTypes.Add(new DisabledQuestionType { Type = QuestionType.Snelvuur });
        _db.SaveChanges();

        await Assert.ThrowsAsync<QuizException>(() =>
            _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.Snelvuur])));
        // Te weinig vragen in de bank.
        await Assert.ThrowsAsync<QuizException>(() =>
            _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.Emoji])));

        Assert.DoesNotContain(QuestionType.Snelvuur, await _t.Picker.ActiveTypesAsync());
    }

    [Fact]
    public async Task A_mix_alternates_between_the_chosen_types()
    {
        AddSnelvuur(12);
        var id = await _matches.CreateAsync(_host, "host",
            new CreateMatchRequest(null, 4, [QuestionType.Snelvuur, QuestionType.WieBenIk]));
        Assert.Equal(10, (await _matches.GetStateAsync(_host, id)).TotalQuestions);

        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        var types = await _db.MatchQuestions.Where(q => q.MatchId == id).OrderBy(q => q.Number).Select(q => q.Type).ToListAsync();
        Assert.Equal(5, types.Count(t => t == QuestionType.Snelvuur));
        Assert.Equal(5, types.Count(t => t == QuestionType.WieBenIk));
        Assert.All(types.Zip(types.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));

        var lobby = await _matches.GetLobbyAsync(_host);
        Assert.Equal("🎲 Mix 🕵️ ⚡", QuestionTypes.Label(Assert.Single(lobby.Mine).Types));
    }

    public void Dispose() => _t.Dispose();
}
