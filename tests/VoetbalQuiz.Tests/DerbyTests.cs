using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class DerbyTests : IDisposable
{
    private sealed class NoNotifier : IMatchNotifier
    {
        public Task LobbyChangedAsync() => Task.CompletedTask;
        public Task MatchChangedAsync(int matchId) => Task.CompletedTask;
    }

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly MatchService _matches;
    private readonly string _host, _guest, _third;

    public DerbyTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        for (var i = 1; i <= 20; i++)
        {
            var data = new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], i % 4);
            _db.QuizItems.Add(new QuizItem
            {
                Type = QuestionType.Snelvuur, Key = data.Question, Data = QuestionEngine.Serialize(data), Status = PlayerStatus.Approved,
            });
        }
        _host = AddUser("host");
        _guest = AddUser("gast");
        _third = AddUser("derde");

        var results = new ResultsService(_db, _clock, new MemoryCache(new MemoryCacheOptions()));
        _matches = new MatchService(_db, _clock, results, new QuizService(_db, _clock, results), new NoNotifier(), new Random(7));
    }

    private string AddUser(string name)
    {
        var user = new AppUser { UserName = name };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }

    private async Task Tick(double seconds)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        await _matches.AdvanceAllAsync();
    }

    /// <summary>Een derby met drie spelers, gespeeld tot de start van vraag 2 (vraag 1 is wapenstilstand).</summary>
    private async Task<int> AtQuestionTwoAsync(QuestionType type = QuestionType.Snelvuur)
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [type], Derby: true));
        await _matches.JoinAsync(_guest, id);
        await _matches.JoinAsync(_third, id);
        await _matches.StartAsync(_host, id);
        await Tick(MatchRules.CountdownSeconds);
        await Tick(GameRules.TimeLimitSeconds + 1);
        await Tick(MatchRules.RevealSeconds);
        return id;
    }

    private void Give(int matchId, string userId, DerbyWeapon weapon)
    {
        _db.MatchPlayers.Single(p => p.MatchId == matchId && p.UserId == userId).Weapon = weapon;
        _db.SaveChanges();
    }

    private async Task<(SnelvuurData Data, int ItemId)> CurrentAsync(int matchId)
    {
        var match = await _db.Matches.Include(m => m.Questions).ThenInclude(q => q.QuizItem).SingleAsync(m => m.Id == matchId);
        var q = MatchService.CurrentQuestion(match)!;
        return (QuestionEngine.Parse<SnelvuurData>(q.QuizItem!.Data), q.QuizItemId!.Value);
    }

    [Fact]
    public void Weapons_are_dealt_without_duplicates_up_to_four_players()
    {
        var random = new Random(1);
        Assert.Equal(6, DerbyRules.Deal(6, true, random).Distinct().Count());
        Assert.Equal(3, DerbyRules.Deal(3, true, random).Distinct().Count());
        Assert.Equal(6, DerbyRules.Deal(8, true, random).Distinct().Count());
        Assert.Equal(5, DerbyRules.Deal(5, false, random).Distinct().Count());
        Assert.DoesNotContain(DerbyWeapon.Trump, DerbyRules.Deal(8, false, random));
    }

    [Fact]
    public async Task Every_player_gets_a_secret_weapon_at_kickoff()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, Derby: true));
        await _matches.JoinAsync(_guest, id);
        Assert.True(Assert.Single((await _matches.GetLobbyAsync(_host)).Mine).Derby);
        await _matches.StartAsync(_host, id);

        var state = await _matches.GetStateAsync(_guest, id);
        Assert.NotNull(state.Derby!.MyWeapon);
        Assert.NotEqual(DerbyWeapon.Trump, state.Derby.MyWeapon); // enkel "Wie ben ik?": geen keuzevragen
        Assert.True(_db.MatchPlayers.Where(p => p.MatchId == id).All(p => p.Weapon != null));

        var plain = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4));
        Assert.Null((await _matches.GetStateAsync(_host, plain)).Derby);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_host, plain, null));
    }

    [Fact]
    public async Task No_weapons_on_the_first_question()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.Snelvuur], Derby: true));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        Give(id, _host, DerbyWeapon.BengaalsVuur);
        await Tick(MatchRules.CountdownSeconds);

        var state = await _matches.GetStateAsync(_host, id);
        Assert.True(state.Derby!.Ceasefire);
        Assert.False(state.Derby.CanUseWeapon);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_host, id, null));
    }

    [Fact]
    public async Task Red_card_halves_the_points_of_the_target()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.RodeKaart);
        Give(id, _third, DerbyWeapon.RodeKaart);

        var used = await _matches.UseWeaponAsync(_host, id, "gast");
        Assert.True(used.Derby!.MyWeaponUsed);
        var seen = Assert.Single((await _matches.GetStateAsync(_guest, id)).Derby!.Events);
        Assert.Equal((DerbyWeapon.RodeKaart, "host", true), (seen.Weapon, seen.By, seen.TargetIsMe));

        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_third, id, "gast")); // al rood
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_host, id, "derde"));  // al gebruikt
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_third, id, "derde")); // niet jezelf

        var (q, _) = await CurrentAsync(id);
        await _matches.GuessAsync(_guest, id, q.Correct.ToString()); // 100 + 50, gehalveerd
        await Tick(QuestionRules.SnelvuurSeconds + 1);

        var reveal = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(75, reveal.MyPoints);
        Assert.Equal(75, reveal.Players.Single(p => p.IsMe).Score);
        Assert.Equal(75, reveal.Derby!.Events.Single().Effect);
    }

    [Fact]
    public async Task Trump_makes_his_answer_the_right_one()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Trump);
        var (q, _) = await CurrentAsync(id);
        var fake = (q.Correct + 1) % 4;

        await _matches.UseWeaponAsync(_guest, id, null);
        var others = await _matches.GetStateAsync(_host, id);
        Assert.Equal(DerbyWeapon.Trump, Assert.Single(others.Derby!.Events).Weapon);

        await _matches.GuessAsync(_guest, id, fake.ToString());
        await _matches.GuessAsync(_third, id, fake.ToString());
        await _matches.GuessAsync(_host, id, q.Correct.ToString());
        await Tick(0.1);

        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.Equal(0, reveal.MyPoints);
        Assert.Equal(["gast", "derde"], reveal.Reveal.Select(r => r.UserName));
        Assert.Equal(fake, reveal.Result!.CorrectIndex);
        Assert.Equal(q.Options[fake], reveal.Derby!.TrumpOfficial);
        Assert.Equal(q.Options[q.Correct], reveal.Derby.TrumpActual);
        Assert.Equal(100, reveal.Derby.Events.Single().Effect); // de snelheidspunten die de host verloor
    }

    [Fact]
    public async Task Trump_needs_a_choice_question_and_to_be_used_before_answering()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Trump);
        var (q, _) = await CurrentAsync(id);
        await _matches.GuessAsync(_guest, id, q.Correct.ToString());
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_guest, id, null));

        var who = await AtQuestionTwoAsync(QuestionType.WieBenIk);
        Give(who, _guest, DerbyWeapon.Trump);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_guest, who, null));
    }

    [Fact]
    public async Task Trump_without_an_answer_changes_nothing()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Trump);
        var (q, _) = await CurrentAsync(id);
        await _matches.UseWeaponAsync(_guest, id, null);
        await _matches.GuessAsync(_host, id, q.Correct.ToString());
        await Tick(QuestionRules.SnelvuurSeconds + 1);

        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(150, reveal.MyPoints);
        Assert.Null(reveal.Derby!.TrumpOfficial);
    }

    [Fact]
    public async Task Swap_gives_everyone_the_same_new_question()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Wissel);
        var (old, oldItem) = await CurrentAsync(id);
        await _matches.GuessAsync(_host, id, old.Correct.ToString());

        await _matches.UseWeaponAsync(_guest, id, null);
        var swapped = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Countdown, swapped.Phase);
        Assert.Equal(DerbyWeapon.Wissel, Assert.Single(swapped.Derby!.Events).Weapon);

        await Tick(DerbyRules.SwapCountdownSeconds);
        var fresh = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchPhase.Question, fresh.Phase);
        Assert.Equal(2, fresh.CurrentNumber);
        Assert.False(fresh.MyDone); // het oude antwoord telt niet meer
        var (_, newItem) = await CurrentAsync(id);
        Assert.NotEqual(oldItem, newItem);
        var used = _db.MatchQuestions.Where(q => q.MatchId == id && q.Number != 2).Select(q => q.QuizItemId).ToList();
        Assert.DoesNotContain(newItem, used);

        // Max één wissel per vraag, en niet meer in de tweede helft van de vraagtijd.
        Give(id, _third, DerbyWeapon.Wissel);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_third, id, null));
    }

    [Fact]
    public async Task Gokchinees_takes_all_points_of_the_bribed_player()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.Gokchinees);
        Give(id, _third, DerbyWeapon.Gokchinees);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_host, id, null)); // zonder doelwit
        await _matches.UseWeaponAsync(_host, id, "gast");
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_third, id, "gast")); // al omgekocht

        var seen = Assert.Single((await _matches.GetStateAsync(_third, id)).Derby!.Events);
        Assert.Equal((DerbyWeapon.Gokchinees, "host", "gast"), (seen.Weapon, seen.By, seen.Target));

        var (q, _) = await CurrentAsync(id);
        await _matches.GuessAsync(_guest, id, q.Correct.ToString()); // 100 + 50 bonus
        await Tick(QuestionRules.SnelvuurSeconds + 1);

        var reveal = await _matches.GetStateAsync(_host, id);
        Assert.Equal(0, reveal.Players.Single(p => p.UserName == "gast").Score);
        Assert.Equal(150, reveal.Players.Single(p => p.UserName == "host").Score);
        Assert.Equal(150, reveal.Derby!.Events.Single().Effect);
    }

    [Fact]
    public async Task Verlenging_gives_only_you_ten_seconds_more()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Verlenging);
        var used = await _matches.UseWeaponAsync(_guest, id, null);
        Assert.Equal(QuestionRules.SnelvuurSeconds + DerbyRules.ExtensionSeconds, used.TimeLimit);
        Assert.Equal(QuestionRules.SnelvuurSeconds, (await _matches.GetStateAsync(_host, id)).TimeLimit);
        Assert.Equal(DerbyWeapon.Verlenging, Assert.Single((await _matches.GetStateAsync(_host, id)).Derby!.Events).Weapon);

        await _matches.GiveUpAsync(_host, id);
        await _matches.GiveUpAsync(_third, id);
        await Tick(QuestionRules.SnelvuurSeconds + 3); // de gewone tijd is om, de verlenging loopt nog
        var late = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Question, late.Phase);

        var (q, _) = await CurrentAsync(id);
        var answered = await _matches.GuessAsync(_guest, id, q.Correct.ToString());
        Assert.True(answered.MyCorrect);
        await Tick(0.1);
        var reveal = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Reveal, reveal.Phase);
        Assert.InRange(reveal.MyPoints, 50 + 50, 60 + 50); // telt als op de valreep, plus de bonus van de eerste juiste
    }

    [Fact]
    public async Task Verlenging_runs_out_too()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Verlenging);
        await _matches.UseWeaponAsync(_guest, id, null);
        await Tick(QuestionRules.SnelvuurSeconds + 3);
        Assert.Equal(MatchPhase.Question, (await _matches.GetStateAsync(_host, id)).Phase);
        await Tick(DerbyRules.ExtensionSeconds);
        Assert.Equal(MatchPhase.Reveal, (await _matches.GetStateAsync(_host, id)).Phase);
    }

    [Fact]
    public async Task Swap_is_not_a_second_chance_after_your_own_answer()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Wissel);
        var (q, _) = await CurrentAsync(id);
        await _matches.GuessAsync(_guest, id, ((q.Correct + 1) % 4).ToString()); // fout
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_guest, id, null));
    }

    [Fact]
    public async Task Red_card_survives_a_swap()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.RodeKaart);
        Give(id, _third, DerbyWeapon.Wissel);
        await _matches.UseWeaponAsync(_host, id, "gast");
        await _matches.UseWeaponAsync(_third, id, null);
        await Tick(DerbyRules.SwapCountdownSeconds);

        var (q, _) = await CurrentAsync(id);
        await _matches.GuessAsync(_guest, id, q.Correct.ToString());
        await Tick(QuestionRules.SnelvuurSeconds + 1);
        Assert.Equal(75, (await _matches.GetStateAsync(_guest, id)).MyPoints);
    }

    [Fact]
    public async Task A_weapon_cannot_be_used_twice_even_after_a_reload()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.BengaalsVuur);

        // Een tweede verzoek dat tegelijk binnenkomt (dubbele tik) werkt met een oudere kopie van de wedstrijd.
        using var other = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        var results = new ResultsService(other, _clock, new MemoryCache(new MemoryCacheOptions()));
        var stale = new MatchService(other, _clock, results, new QuizService(other, _clock, results), new NoNotifier());
        Assert.True((await stale.GetStateAsync(_host, id)).Derby!.CanUseWeapon);

        await _matches.UseWeaponAsync(_host, id, null);
        await Assert.ThrowsAsync<QuizException>(() => stale.UseWeaponAsync(_host, id, null));
    }

    [Fact]
    public async Task No_weapons_on_the_last_question()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.BengaalsVuur);
        for (var n = 2; n < MatchRules.Questions; n++)
        {
            await Tick(QuestionRules.SnelvuurSeconds + 1);
            await Tick(MatchRules.RevealSeconds);
        }
        var last = await _matches.GetStateAsync(_host, id);
        Assert.Equal(MatchRules.Questions, last.CurrentNumber);
        Assert.True(last.Derby!.Ceasefire);
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_host, id, null));
    }

    [Fact]
    public async Task Swap_and_flare_only_in_the_first_half_of_the_question()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _guest, DerbyWeapon.Wissel);
        Give(id, _third, DerbyWeapon.BengaalsVuur);
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.SnelvuurSeconds * 0.6));
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_guest, id, null));
        await Assert.ThrowsAsync<QuizException>(() => _matches.UseWeaponAsync(_third, id, null));
    }

    [Fact]
    public async Task Flare_can_be_set_up_during_the_reveal_for_the_next_question()
    {
        var id = await _matches.CreateAsync(_host, "host", new CreateMatchRequest(null, 4, [QuestionType.Snelvuur], Derby: true));
        await _matches.JoinAsync(_guest, id);
        await _matches.StartAsync(_host, id);
        Give(id, _host, DerbyWeapon.BengaalsVuur);
        await Tick(MatchRules.CountdownSeconds);
        await Tick(QuestionRules.SnelvuurSeconds + 1); // tussenstand na vraag 1

        var state = await _matches.UseWeaponAsync(_host, id, null);
        Assert.Equal(2, state.Derby!.WeaponFor);
        Assert.True(Assert.Single(state.Derby.Events).Since < 0);  // klaargezet
        // Iedereen ziet meteen dat er vuur klaarstaat voor de volgende vraag.
        Assert.True(Assert.Single((await _matches.GetStateAsync(_guest, id)).Derby!.Events).Since < 0);

        await Tick(MatchRules.RevealSeconds);
        await Tick(1);
        var guest = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(2, guest.CurrentNumber);
        var flare = Assert.Single(guest.Derby!.Events);
        Assert.Equal(DerbyWeapon.BengaalsVuur, flare.Weapon);
        Assert.InRange(flare.Since, 0.9, 1.1);
        Assert.Equal(QuestionRules.SnelvuurSeconds, guest.TimeLimit); // geen tijd minder
    }

    [Fact]
    public async Task Report_lists_every_weapon_after_the_match()
    {
        var id = await AtQuestionTwoAsync();
        Give(id, _host, DerbyWeapon.RodeKaart);
        await _matches.UseWeaponAsync(_host, id, "gast");
        for (var n = 2; n <= MatchRules.Questions; n++)
        {
            await Tick(QuestionRules.SnelvuurSeconds + 1);
            await Tick(MatchRules.RevealSeconds);
        }

        var final = await _matches.GetStateAsync(_guest, id);
        Assert.Equal(MatchPhase.Finished, final.Phase);
        var report = final.Derby!.Report!;
        Assert.Equal(3, report.Count);
        var card = report.Single(r => r.UserName == "host");
        Assert.Equal((DerbyWeapon.RodeKaart, 2, "gast"), (card.Weapon, card.Question, card.Target));
        Assert.All(report.Where(r => r.UserName != "host"), r => Assert.Null(r.Question)); // niet ingezet
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
