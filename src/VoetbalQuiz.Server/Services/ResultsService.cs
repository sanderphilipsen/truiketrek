using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Dagwinnaars, klassementen en statistieken.</summary>
public class ResultsService(QuizDbContext db, TimeProvider clock, IMemoryCache cache)
{
    private DateOnly Today => QuizClock.Today(clock);

    /// <summary>Eén beantwoorde vraag, plat gemaakt voor berekeningen.</summary>
    private record Row(
        string UserId, string UserName, int ChallengeId, DateOnly Date, string? PlayerName,
        int Points, bool Correct, int WrongGuesses, DateTime StartedAt, DateTime AnsweredAt);

    /// <summary>Totaal van één gebruiker op één dag.</summary>
    private record DayTotal(
        string UserId, string UserName, int ChallengeId, DateOnly Date,
        int Points, int Correct, int Wrong, DateTime FinishedAt);

    /// <summary>Beantwoorde vragen. Specials tellen standaard niet mee: de daily-klassementen blijven apart.</summary>
    private Task<List<Row>> RowsAsync(Expression<Func<Attempt, bool>> filter, bool includeSpecials = false) =>
        db.Attempts
            .Where(a => a.AnsweredAt != null)
            .Where(a => includeSpecials || a.DailyQuestion.DailyChallenge.SpecialEventId == null)
            .Where(filter)
            .Select(a => new Row(
                a.UserId, a.User.UserName!, a.DailyQuestion.DailyChallengeId, a.DailyQuestion.DailyChallenge.Date,
                a.DailyQuestion.Player != null ? a.DailyQuestion.Player.Name : null, a.Points, a.Correct, a.WrongGuesses, a.StartedAt, a.AnsweredAt!.Value))
            .ToListAsync();

    private static IEnumerable<DayTotal> DayTotals(IEnumerable<Row> rows) =>
        rows.GroupBy(r => (r.UserId, r.ChallengeId)).Select(g => new DayTotal(
            g.Key.UserId, g.First().UserName, g.Key.ChallengeId, g.First().Date,
            g.Sum(r => r.Points), g.Count(r => r.Correct), g.Sum(r => r.WrongGuesses), g.Max(r => r.AnsweredAt)));

    /// <summary>
    /// Volgorde van een dag. Bij gelijke punten wint wie meer juist had, dan wie minder fout gokte,
    /// en dan wie het eerst klaar was. Zo is er altijd precies één dagwinnaar.
    /// </summary>
    private static List<DayTotal> RankDay(IEnumerable<DayTotal> totals) =>
        totals.OrderByDescending(t => t.Points)
            .ThenByDescending(t => t.Correct)
            .ThenBy(t => t.Wrong)
            .ThenBy(t => t.FinishedAt)
            .ToList();

    // ---------- Avatar en level ----------

    public record UserCard(string? AvatarId, int Level);

    /// <summary>Wat elke gebruiker gespeeld heeft (daily en online), als basis voor XP.</summary>
    public async Task<Dictionary<string, XpInput>> XpInputsAsync()
    {
        await SettleFinishedDaysAsync();
        var rows = await RowsAsync(a => true, includeSpecials: true); // ook specials leveren XP op
        var wins = await db.DailyChallenges
            .Where(c => c.WinnerUserId != null && c.SpecialEventId == null)
            .GroupBy(c => c.WinnerUserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
        var matches = await db.MatchPlayers
            .Where(p => p.Match.Status == MatchStatus.Finished)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Played = g.Count(), Won = g.Count(p => p.Rank == 1) })
            .ToDictionaryAsync(x => x.UserId);

        var sizes = await ChallengeSizesAsync();
        var daily = rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.ToList());
        return daily.Keys.Union(matches.Keys).ToDictionary(id => id, id =>
        {
            var mine = daily.GetValueOrDefault(id) ?? [];
            var days = mine.GroupBy(r => r.ChallengeId).ToList();
            var online = matches.GetValueOrDefault(id);
            return new XpInput(
                mine.Count,
                mine.Where(r => r.Correct).Sum(r => r.Points),
                days.Count(d => d.Count() == sizes.GetValueOrDefault(d.Key)),
                days.Count(d => d.Count(r => r.Correct) == sizes.GetValueOrDefault(d.Key)),
                wins.GetValueOrDefault(id),
                online?.Played ?? 0,
                online?.Won ?? 0);
        });
    }

    /// <summary>Hoeveel vragen elke reeks heeft (een opstellingsdag of mix heeft er minder dan 10).</summary>
    public Task<Dictionary<int, int>> ChallengeSizesAsync() =>
        db.DailyQuestions.GroupBy(q => q.DailyChallengeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public async Task<LevelInfo> LevelOfAsync(string userId)
    {
        var inputs = await XpInputsAsync();
        return Levels.For(inputs.TryGetValue(userId, out var input) ? Levels.Xp(input) : 0);
    }

    /// <summary>
    /// Avatar en level van elke gebruiker, voor klassementen, statistieken en wedstrijden.
    /// Kort gecachet omdat een lopende wedstrijd dit bij elke actie opvraagt.
    /// </summary>
    public async Task<Dictionary<string, UserCard>> UserCardsAsync()
    {
        if (cache.TryGetValue("user-cards", out Dictionary<string, UserCard>? cached)) return cached!;

        var inputs = await XpInputsAsync();
        var users = await db.Users.Select(u => new { u.Id, u.AvatarId }).ToListAsync();
        var cards = users.ToDictionary(u => u.Id, u => new UserCard(
            u.AvatarId, Levels.For(inputs.TryGetValue(u.Id, out var i) ? Levels.Xp(i) : 0).Level));
        cache.Set("user-cards", cards, TimeSpan.FromSeconds(10));
        return cards;
    }

    /// <summary>Na een avatarwissel meteen de nieuwe tonen.</summary>
    public void ForgetUserCards() => cache.Remove("user-cards");

    // ---------- Podiums (voor het maandklassement) ----------

    public record Podium(int ChallengeId, bool IsSpecial, List<string> UserIds);

    /// <summary>
    /// Top 3 van elke afgelopen daily en special waarvan de datum in de periode valt,
    /// met dezelfde volgorde-regels als de dagwinnaar.
    /// </summary>
    public async Task<List<Podium>> PodiumsAsync(DateOnly from, DateOnly to)
    {
        await SettleFinishedDaysAsync();
        var settled = await db.DailyChallenges
            .Where(c => c.SettledAt != null && c.Date >= from && c.Date < to)
            .Select(c => new { c.Id, IsSpecial = c.SpecialEventId != null })
            .ToListAsync();
        var ids = settled.Select(c => c.Id).ToList();
        var totals = DayTotals(await RowsAsync(a => ids.Contains(a.DailyQuestion.DailyChallengeId), includeSpecials: true))
            .ToLookup(t => t.ChallengeId);

        return settled.Select(c => new Podium(c.Id, c.IsSpecial,
            RankDay(totals[c.Id]).Where(t => t.Points > 0).Take(MonthRules.PodiumBonus.Length).Select(t => t.UserId).ToList()))
            .ToList();
    }

    // ---------- Specials ----------

    /// <summary>Lopende, komende (30 dagen) en recente (30 dagen) specials, met jouw score.</summary>
    public async Task<List<SpecialSummary>> GetSpecialsAsync(string userId)
    {
        await SettleFinishedDaysAsync();
        var today = Today;
        var specials = await db.SpecialEvents
            .Where(s => s.StartDate <= today.AddDays(30) && s.EndDate >= today.AddDays(-30))
            .OrderBy(s => s.StartDate)
            .ToListAsync();
        var ids = specials.Select(s => s.Id).ToList();

        var challenges = await db.DailyChallenges
            .Where(c => c.SpecialEventId != null && ids.Contains(c.SpecialEventId.Value))
            .Select(c => new { SpecialId = c.SpecialEventId!.Value, Winner = c.Winner!.UserName })
            .ToDictionaryAsync(c => c.SpecialId, c => c.Winner);
        var mine = (await RowsAsync(a => a.UserId == userId && a.DailyQuestion.DailyChallenge.SpecialEventId != null, includeSpecials: true))
            .GroupBy(r => r.ChallengeId).ToList();
        var mineBySpecial = await db.DailyChallenges
            .Where(c => c.SpecialEventId != null && ids.Contains(c.SpecialEventId.Value))
            .Select(c => new { c.Id, SpecialId = c.SpecialEventId!.Value })
            .ToDictionaryAsync(c => c.SpecialId, c => c.Id);

        return specials.Select(s =>
        {
            var played = mineBySpecial.TryGetValue(s.Id, out var cid) ? mine.FirstOrDefault(g => g.Key == cid)?.ToList() : null;
            return new SpecialSummary(s.Id, s.Name, s.Icon, s.Description, s.StartDate, s.EndDate,
                s.StartDate <= today && today <= s.EndDate, s.StartDate > today,
                played?.Sum(r => r.Points), played?.Count ?? 0, challenges.GetValueOrDefault(s.Id), s.Theme);
        }).ToList();
    }

    // ---------- Dagwinnaars ----------



    /// <summary>Legt de winnaar vast van elke dag die voorbij is en nog niet afgesloten werd.</summary>
    public async Task SettleFinishedDaysAsync()
    {
        var today = Today;
        // Een daily is voorbij na zijn dag, een special na zijn einddatum.
        var open = await db.DailyChallenges
            .Where(c => c.SettledAt == null
                        && ((c.SpecialEventId == null && c.Date < today) || (c.SpecialEventId != null && c.SpecialEvent!.EndDate < today)))
            .ToListAsync();
        if (open.Count == 0) return;

        var ids = open.Select(c => c.Id).ToList();
        var totals = DayTotals(await RowsAsync(a => ids.Contains(a.DailyQuestion.DailyChallengeId), includeSpecials: true))
            .ToLookup(t => t.ChallengeId);

        foreach (var challenge in open)
        {
            var winner = RankDay(totals[challenge.Id]).FirstOrDefault();
            if (winner is { Points: > 0 })
            {
                challenge.WinnerUserId = winner.UserId;
                challenge.WinnerPoints = winner.Points;
            }
            challenge.SettledAt = clock.GetUtcNow().UtcDateTime;
        }
        await db.SaveChangesAsync();
    }

    public async Task<DayWinner?> GetLastWinnerAsync(string userId)
    {
        await SettleFinishedDaysAsync();
        var last = await db.DailyChallenges
            .Where(c => c.WinnerUserId != null && c.SpecialEventId == null)
            .OrderByDescending(c => c.Date)
            .Select(c => new { c.Date, c.Winner!.UserName, c.Winner.AvatarId, c.WinnerPoints, c.WinnerUserId })
            .FirstOrDefaultAsync();
        return last is null ? null : new DayWinner(last.Date, last.UserName!, last.AvatarId, last.WinnerPoints ?? 0, last.WinnerUserId == userId);
    }

    // ---------- Klassementen ----------

    public async Task<Leaderboard> GetDailyLeaderboardAsync(string userId)
    {
        var today = Today;
        var ranked = RankDay(DayTotals(await RowsAsync(a => a.DailyQuestion.DailyChallenge.Date == today)));
        var cards = await UserCardsAsync();
        var entries = ranked.Select((t, i) => new LeaderboardEntry(
            i + 1, t.UserName, cards[t.UserId].AvatarId, cards[t.UserId].Level, t.Points, 1, t.Correct, 0, t.UserId == userId)).ToList();
        return new Leaderboard("Vandaag", $"Top 3 van de dag krijgt +{MonthRules.PodiumBonus[0]} / +{MonthRules.PodiumBonus[1]} / +{MonthRules.PodiumBonus[2]} in het maandklassement.", entries);
    }

    // ---------- Statistieken ----------

    public async Task<StatsOverview> GetStatsAsync(string userId)
    {
        await SettleFinishedDaysAsync();
        var today = Today;

        var rows = await RowsAsync(a => true);
        var days = DayTotals(rows).ToList();
        var names = rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First().UserName);
        var winners = await db.DailyChallenges
            .Where(c => c.WinnerUserId != null && c.SpecialEventId == null)
            .Select(c => new { c.Id, c.Date, UserId = c.WinnerUserId! })
            .ToListAsync();

        var users = await UserCardsAsync();
        var cards = new List<StatCard>();
        void Add(string icon, string title, string caption, string? user, string value)
        {
            if (user is not null) cards.Add(new StatCard(icon, title, caption, names[user], users[user].AvatarId, value, user == userId));
        }

        // Meeste dagzeges
        var winCounts = winners.GroupBy(w => w.UserId).Select(g => (User: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count).ToList();
        if (winCounts.Count > 0)
            Add("👑", "Meeste dagzeges", "Vaakst de beste van de dag", winCounts[0].User, $"{winCounts[0].Count}×");

        // Hoogste dagscore
        var bestDay = days.OrderByDescending(d => d.Points).ThenBy(d => d.FinishedAt).FirstOrDefault();
        if (bestDay is { Points: > 0 })
            Add("🔥", "Hoogste dagscore", $"Op {DutchDates.DayMonth(bestDay.Date)}", bestDay.UserId, $"{bestDay.Points}");

        // Eeuwige tweede
        var settledIds = winners.Select(w => w.Id).ToHashSet();
        var seconds = days.Where(d => settledIds.Contains(d.ChallengeId))
            .GroupBy(d => d.ChallengeId)
            .Select(g => RankDay(g).Skip(1).FirstOrDefault())
            .Where(d => d is not null)
            .GroupBy(d => d!.UserId).Select(g => (User: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count).FirstOrDefault();
        if (seconds.User is not null)
            Add("🥈", "Eeuwige tweede", "Vaakst net naast de dagzege", seconds.User, $"{seconds.Count}×");

        // Snelste vinger: juist bij de eerste tip zonder foute poging
        var fastest = rows.Where(r => r.Points == 100).GroupBy(r => r.UserId)
            .Select(g => (User: g.Key, Count: g.Count())).OrderByDescending(x => x.Count).FirstOrDefault();
        if (fastest.User is not null)
            Add("⚡", "Snelste vinger", "Meeste antwoorden bij de eerste tip", fastest.User, $"{fastest.Count}×");

        // Perfecte dagen: 10 op 10
        var sizes = await ChallengeSizesAsync();
        var perfect = days.Where(d => d.Correct == sizes.GetValueOrDefault(d.ChallengeId)).GroupBy(d => d.UserId)
            .Select(g => (User: g.Key, Count: g.Count())).OrderByDescending(x => x.Count).FirstOrDefault();
        if (perfect.User is not null)
            Add("💯", "Perfecte dagen", "Alle vragen van de dag juist", perfect.User, $"{perfect.Count}×");

        // Scherpschutter: hoogste raakpercentage
        var sharp = rows.GroupBy(r => r.UserId).Where(g => g.Count() >= 20)
            .Select(g => (User: g.Key, Pct: Percent(g.Count(r => r.Correct), g.Count())))
            .OrderByDescending(x => x.Pct).FirstOrDefault();
        if (sharp.User is not null)
            Add("🎯", "Scherpschutter", "Hoogste % juist (min. 20 vragen)", sharp.User, $"{sharp.Pct}%");

        // Hoogste gemiddelde dagscore
        var average = days.GroupBy(d => d.UserId).Where(g => g.Count() >= 3)
            .Select(g => (User: g.Key, Avg: (int)Math.Round(g.Average(d => d.Points))))
            .OrderByDescending(x => x.Avg).FirstOrDefault();
        if (average.User is not null)
            Add("📈", "Constantste speler", "Hoogste gemiddelde per dag (min. 3 dagen)", average.User, $"{average.Avg}");

        // Snelste gemiddelde reactietijd
        var quick = rows.Where(r => r.Correct).GroupBy(r => r.UserId).Where(g => g.Count() >= 10)
            .Select(g => (User: g.Key, Sec: g.Average(r => (r.AnsweredAt - r.StartedAt).TotalSeconds)))
            .OrderBy(x => x.Sec).FirstOrDefault();
        if (quick.User is not null)
            Add("⏱️", "Bliksemsnel", "Snelste gemiddelde tijd per juist antwoord", quick.User, $"{quick.Sec:0.0} s");

        // Langste reeks
        var streak = days.GroupBy(d => d.UserId)
            .Select(g => (User: g.Key, Streak: Streaks(g.Select(d => d.Date), today).Longest))
            .OrderByDescending(x => x.Streak).FirstOrDefault();
        if (streak is { User: not null, Streak: >= 2 })
            Add("📅", "Trouwste speler", "Langste reeks dagen na elkaar gespeeld", streak.User, $"{streak.Streak} dagen");

        // Gokker van dienst
        var gambler = rows.GroupBy(r => r.UserId)
            .Select(g => (User: g.Key, Wrong: g.Sum(r => r.WrongGuesses)))
            .OrderByDescending(x => x.Wrong).FirstOrDefault();
        if (gambler is { User: not null, Wrong: > 0 })
            Add("🎲", "Gokker van dienst", "Meeste foute pogingen", gambler.User, $"{gambler.Wrong}");

        return new StatsOverview(MyStatsFor(userId, rows, days, winners.Count(w => w.UserId == userId), today),
            cards, [], Hardest(rows, today)); // maandkampioenen komen uit het maandklassement (MonthRankingService)
    }

    private static MyStats MyStatsFor(string userId, List<Row> rows, List<DayTotal> days, int wins, DateOnly today)
    {
        var mine = rows.Where(r => r.UserId == userId).ToList();
        var myDays = days.Where(d => d.UserId == userId).ToList();
        var (current, longest) = Streaks(myDays.Select(d => d.Date), today);
        return new MyStats(
            myDays.Count,
            wins,
            myDays.Count == 0 ? 0 : myDays.Max(d => d.Points),
            myDays.Count == 0 ? 0 : (int)Math.Round(myDays.Average(d => d.Points)),
            Percent(mine.Count(r => r.Correct), mine.Count),
            mine.Count(r => r.Points == 100),
            current,
            longest);
    }

    /// <summary>Spelers die het minst geraden werden. Enkel afgelopen dagen, zodat niets van vandaag verklapt wordt.</summary>
    private static List<HardQuestion> Hardest(List<Row> rows, DateOnly today) =>
        rows.Where(r => r.Date < today && r.PlayerName != null)
            .GroupBy(r => r.PlayerName!)
            .Where(g => g.Count() >= 2)
            .Select(g => new HardQuestion(g.Key, Percent(g.Count(r => r.Correct), g.Count()), g.Count()))
            .OrderBy(h => h.CorrectPercent).ThenByDescending(h => h.Attempts)
            .Take(5)
            .ToList();

    private static int Percent(int part, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * part / total);

    /// <summary>Huidige reeks (tot vandaag of gisteren) en langste reeks opeenvolgende speeldagen.</summary>
    private static (int Current, int Longest) Streaks(IEnumerable<DateOnly> dates, DateOnly today)
    {
        var sorted = dates.Distinct().Order().ToList();
        int longest = 0, run = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            run = i > 0 && sorted[i - 1].AddDays(1) == sorted[i] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }
        var current = sorted.Count > 0 && sorted[^1] >= today.AddDays(-1) ? run : 0;
        return (current, longest);
    }
}
