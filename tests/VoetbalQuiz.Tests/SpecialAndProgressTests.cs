using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public sealed class SpecialAndProgressTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly ResultsService _results;
    private readonly QuizService _quiz;
    private readonly AdminService _admin;
    private readonly ProgressService _progress;
    private readonly MonthRankingService _ranking;
    private readonly string _me, _friend;

    public SpecialAndProgressTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        for (var i = 1; i <= 30; i++)
        {
            _db.Players.Add(new Player
            {
                Name = $"Speler {i}",
                Status = PlayerStatus.Approved,
                Tags = i <= 12 ? ["halloween"] : [],
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")],
                Sources = ["https://example.org"],
            });
        }
        _me = AddUser("sander");
        _friend = AddUser("tom");

        _results = _t.Results;
        _quiz = _t.Quiz;
        _admin = _t.Admin;
        _ranking = _t.Ranking;
        _progress = _t.Progress;
    }

    private string AddUser(string name) => _t.AddUser(name);

    private async Task PlayAllAsync(string userId, int? specialId, bool correct)
    {
        var challenge = specialId is { } id ? await _quiz.GetOrCreateSpecialChallengeAsync(id) : await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        foreach (var q in challenge.Questions.OrderBy(q => q.Number))
        {
            await _quiz.StartAsync(userId, q.Number, specialId);
            if (correct) await _quiz.GuessAsync(userId, q.Number, q.Player!.Name, specialId);
            else await _quiz.GiveUpAsync(userId, q.Number, specialId);
        }
    }

    private Task<int> HalloweenAsync() => _admin.SaveSpecialAsync(
        new SpecialEdit(0, "halloween", "Halloween Special", "🎃", "", _quiz.Today, _quiz.Today, "halloween", []));

    [Fact]
    public async Task Special_uses_its_themed_pool_and_has_its_own_leaderboard()
    {
        var id = await HalloweenAsync();
        await PlayAllAsync(_me, id, correct: true);

        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        Assert.All(challenge.Questions, q => Assert.Contains("halloween", q.Player!.Tags));

        var month = Assert.Single((await _ranking.GetAsync(_me, 2026, 10)).Entries);
        Assert.Equal(1000, month.Specials);
        Assert.Equal(0, month.Daily);
        Assert.Equal(1, month.SpecialsPlayed);

        // Het dagklassement is enkel de daily: de special telt daar niet mee.
        Assert.Empty((await _results.GetDailyLeaderboardAsync(_me)).Entries);
    }

    [Fact]
    public async Task Special_leaderboard_ranks_only_that_special()
    {
        var id = await HalloweenAsync();
        await PlayAllAsync(_friend, id, correct: false);
        await PlayAllAsync(_me, id, correct: true);
        await PlayAllAsync(_friend, null, correct: true); // de daily telt niet mee

        var board = await _results.GetSpecialLeaderboardAsync(_me, id);
        Assert.Collection(board.Entries,
            e => { Assert.Equal((1, "sander", 1000, true), (e.Rank, e.UserName, e.Points, e.IsMe)); },
            e => { Assert.Equal((2, "tom", 0, false), (e.Rank, e.UserName, e.Points, e.IsMe)); });

        Assert.Empty((await _results.GetSpecialLeaderboardAsync(_me, id + 1)).Entries);
    }

    [Fact]
    public async Task Daily_and_active_special_show_their_average_difficulty()
    {
        foreach (var p in _db.Players) p.Difficulty = p.Tags.Contains("halloween") ? 5 : 1;
        await _db.SaveChangesAsync();
        var id = await HalloweenAsync();

        var special = Assert.Single(await _quiz.GetSpecialsAsync(_me), s => s.Id == id);
        Assert.Equal(5, special.Difficulty);
        Assert.Equal(5, (await _quiz.GetOverviewAsync(_me, id)).Difficulty);

        var daily = await _quiz.GetOverviewAsync(_me);
        Assert.InRange(daily.Difficulty, 1, 5);
    }

    [Fact]
    public async Task Special_needs_enough_players_and_is_only_playable_in_its_window()
    {
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveSpecialAsync(
            new SpecialEdit(0, "kerst", "Kerst", "🎄", "", _quiz.Today, _quiz.Today, "kerst", [])));

        var id = await _admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Later", "🎃", "", _quiz.Today.AddDays(3), _quiz.Today.AddDays(3), "halloween", []));
        await Assert.ThrowsAsync<QuizException>(() => _quiz.StartAsync(_me, 1, id));
    }

    [Fact]
    public async Task Approving_a_player_requires_a_source()
    {
        var edit = new PlayerEdit(0, "Nieuwe Speler", [], "België", "Jupiler Pro League", "modern", 2,
            ["Ik ben een.", "Ik speelde.", "Ik won.", "Ik scoorde.", "Ik was."], [], [], PlayerStatus.Approved, null, null, null, 0);
        await Assert.ThrowsAsync<QuizException>(() => _admin.SaveAsync(edit, "sander"));

        var id = await _admin.SaveAsync(edit with { Sources = ["https://nl.wikipedia.org/wiki/Test"] }, "sander");
        var saved = await _db.Players.FindAsync(id);
        Assert.Equal(PlayerStatus.Approved, saved!.Status);
        Assert.True(saved.EditedInAdmin);
    }

    [Fact]
    public void Hints_may_not_reveal_the_name()
    {
        var edit = new PlayerEdit(0, "Kevin De Bruyne", [], "", "", "modern", 1,
            ["Ik ben Kevin.", "b", "c", "d", "e"], [], [], PlayerStatus.Review, null, null, null, 0);
        Assert.Contains(PlayerValidation.Problems(edit, false), p => p.Contains("verklapt"));
    }

    [Fact]
    public async Task Rejected_players_are_never_asked()
    {
        foreach (var p in _db.Players.Where(p => p.Tags.Count == 0)) p.Status = PlayerStatus.Rejected;
        await _db.SaveChangesAsync();
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.All(challenge.Questions, q => Assert.NotEqual(PlayerStatus.Rejected, q.Player!.Status));
    }

    [Fact]
    public async Task Profile_shows_badges_trophies_and_head_to_head()
    {
        // Oktober: ik win elke dag van Tom.
        for (var day = 0; day < 3; day++)
        {
            await PlayAllAsync(_me, null, correct: true);
            await PlayAllAsync(_friend, null, correct: false);
            _clock.Advance(TimeSpan.FromDays(1));
        }
        var halloween = await HalloweenAsync();
        await PlayAllAsync(_me, halloween, correct: true);

        _clock.Advance(TimeSpan.FromDays(20)); // november: oktober is een afgesloten seizoen

        var profile = await _progress.GetProfileAsync(_friend, "sander");
        Assert.Contains(profile.Badges, b => b.Id == "perfecte-dag" && b.Earned);
        Assert.Contains(profile.Badges, b => b.Id == "dagwinnaar" && b.Earned);
        Assert.Contains(profile.Badges, b => b.Id == "feestganger" && b.Earned);
        Assert.Contains(profile.Badges, b => b.Id == "specialist" && b.Earned);
        Assert.Contains(profile.Trophies, t => t.Kind == TrophyKind.Month && t.Place == 1);
        Assert.Contains(profile.Trophies, t => t.Kind == TrophyKind.Special);

        var h2h = profile.VersusMe!; // gezien door Tom
        Assert.Equal(3, h2h.DailyDaysTogether);
        Assert.Equal(3, h2h.TheirDailyWins);
        Assert.Equal(0, h2h.MyDailyWins);
    }

    [Fact]
    public async Task Reward_shows_new_xp_and_badges_once()
    {
        var user = await _db.Users.SingleAsync(u => u.Id == _me);
        // Zoals een nieuwe registratie: nul XP per onderdeel en nog geen badges gezien.
        (user.SeenXpParts, user.SeenBadges) = ([.. Levels.Breakdown(new XpInput(0, 0, 0, 0, 0)).Select(p => p.Xp)], []);
        await _db.SaveChangesAsync();

        await PlayAllAsync(_me, null, correct: true);
        var reward = await _progress.ClaimRewardAsync(_me);

        Assert.True(reward.HasNews);
        Assert.Equal(1, reward.Before.Level);
        Assert.Equal(reward.After.Xp, reward.GainedXp);
        Assert.Contains(reward.Gained, p => p.Label == "Perfecte dagen" && p.Xp == Levels.XpPerPerfectDay);
        Assert.Contains(reward.NewBadges, b => b.Id == "aftrap");
        Assert.Contains(reward.NewBadges, b => b.Id == "perfecte-dag");

        // Al gezien: een tweede keer is er niets nieuws.
        Assert.False((await _progress.ClaimRewardAsync(_me)).HasNews);
    }

    [Fact]
    public async Task Reward_starts_silently_for_existing_players()
    {
        await PlayAllAsync(_me, null, correct: true); // gespeeld vóór er een popup bestond

        Assert.False((await _progress.ClaimRewardAsync(_me)).HasNews);
        Assert.False((await _progress.ClaimRewardAsync(_me)).HasNews);
    }

    [Fact]
    public async Task Full_week_and_theme_badges_are_earned()
    {
        foreach (var p in _db.Players) p.Tags = [.. p.Tags, "rode-duivels"];
        await _db.SaveChangesAsync();

        _clock.Advance(TimeSpan.FromDays(2)); // zaterdag 17/10 → maandag 19/10
        for (var day = 0; day < 6; day++)
        {
            await PlayAllAsync(_me, null, correct: true);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        var notYet = await _progress.GetProfileAsync(_me, "sander");
        Assert.Contains(notYet.Badges, b => b.Id == "volle-week" && !b.Earned);
        Assert.Contains(notYet.Badges, b => b.Id == "rode-duivels-fan" && b.Earned); // 60 juiste antwoorden

        await PlayAllAsync(_me, null, correct: true); // zondag
        var profile = await _progress.GetProfileAsync(_me, "sander");
        Assert.Contains(profile.Badges, b => b.Id == "volle-week" && b.Earned);
        Assert.Contains(profile.Badges, b => b.Id == "allrounder" && !b.Earned && b.Progress == $"1/{QuestionTypes.All.Count}");
    }

    public void Dispose() => _t.Dispose();
}
