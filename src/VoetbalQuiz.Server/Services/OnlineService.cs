using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Klassementen en statistieken van online wedstrijden, apart van de daily.</summary>
public class OnlineService(QuizDbContext db, ResultsService results)
{
    /// <summary>
    /// Counts: er bleven genoeg spelers over (zie <see cref="MatchRules.StayersForWin"/>).
    /// Won: een echte zege volgens <see cref="MatchRules.IsWin"/> (alleen eerste, geen gelijkspel).
    /// </summary>
    private record PlayerResult(string UserId, string UserName, int Score, int Rank, int FirstCorrect, bool Counts, bool Won);

    private record FinishedMatch(int Id, DateTime FinishedAt, List<PlayerResult> Players);

    private async Task<List<FinishedMatch>> FinishedAsync()
    {
        var rows = await db.MatchPlayers
            .Where(p => p.Match.Status == MatchStatus.Finished)
            .Select(p => new
            {
                p.MatchId,
                FinishedAt = p.Match.FinishedAt!.Value,
                p.UserId,
                UserName = p.User.UserName!,
                p.Score,
                Rank = p.Rank ?? 99,
                Stayers = p.Match.Players.Count(o => o.LeftAt == null),
                Firsts = p.Match.Players.Count(o => o.Rank == 1),
                FirstCorrect = db.MatchAnswers.Count(a => a.MatchQuestion.MatchId == p.MatchId && a.UserId == p.UserId && a.Position == 1),
            })
            .ToListAsync();

        return rows.GroupBy(r => r.MatchId)
            .Select(g => new FinishedMatch(g.Key, g.First().FinishedAt,
                g.Select(r => new PlayerResult(r.UserId, r.UserName, r.Score, r.Rank, r.FirstCorrect,
                    r.Stayers >= MatchRules.StayersForWin, MatchRules.IsWin(r.Rank, r.Stayers, r.Firsts))).ToList()))
            .OrderBy(m => m.FinishedAt)
            .ToList();
    }

    /// <summary>
    /// Elo voor meerdere spelers: elke wedstrijd telt als een duel tussen elk paar spelers in het partijtje.
    /// We rekenen alles opnieuw uit vanaf de eerste wedstrijd, dus de rating is altijd consistent.
    /// </summary>
    private static (Dictionary<string, double> Current, Dictionary<string, double> Best, Dictionary<string, int> LongestWinStreak)
        Ratings(List<FinishedMatch> matches)
    {
        var rating = new Dictionary<string, double>();
        var best = new Dictionary<string, double>();
        var streak = new Dictionary<string, int>();
        var longest = new Dictionary<string, int>();

        foreach (var match in matches)
        {
            var players = match.Players;
            if (players.Count < 2) continue;
            foreach (var p in players) rating.TryAdd(p.UserId, MatchRules.StartRating);

            var k = MatchRules.EloK / (players.Count - 1);
            var deltas = players.ToDictionary(p => p.UserId, p => players.Where(o => o != p).Sum(o =>
            {
                var expected = 1 / (1 + Math.Pow(10, (rating[o.UserId] - rating[p.UserId]) / 400));
                var actual = p.Rank < o.Rank ? 1 : p.Rank == o.Rank ? 0.5 : 0;
                return k * (actual - expected);
            }));

            foreach (var p in players)
            {
                // Als enige overgebleven: geen zege, dus ook geen rating erbij en de reeks blijft staan.
                // Wie vertrok, verliest wel gewoon. Een gelijkspel telt wel voor de rating (een half punt), maar breekt de reeks.
                if (p.Rank == 1 && !p.Counts) continue;
                rating[p.UserId] += deltas[p.UserId];
                best[p.UserId] = Math.Max(best.GetValueOrDefault(p.UserId, MatchRules.StartRating), rating[p.UserId]);
                streak[p.UserId] = p.Won ? streak.GetValueOrDefault(p.UserId) + 1 : 0;
                longest[p.UserId] = Math.Max(longest.GetValueOrDefault(p.UserId), streak[p.UserId]);
            }
        }
        return (rating, best, longest);
    }

    // ---------- Klassementen ----------

    public async Task<OnlineLeaderboard> GetRatingLeaderboardAsync(string userId)
    {
        var matches = await FinishedAsync();
        var (rating, _, _) = Ratings(matches);
        var cards = await results.UserCardsAsync();
        var all = matches.SelectMany(m => m.Players).GroupBy(p => p.UserId).ToList();

        var entries = all
            .Select(g => (Id: g.Key, Name: g.First().UserName, Rating: (int)Math.Round(rating.GetValueOrDefault(g.Key, MatchRules.StartRating)),
                Played: g.Count(), Wins: g.Count(p => p.Won)))
            .OrderByDescending(x => x.Rating).ThenByDescending(x => x.Wins)
            .Select((x, i) => new RatingEntry(i + 1, x.Name, cards.GetValueOrDefault(x.Id)?.AvatarId, cards.GetValueOrDefault(x.Id)?.Level ?? 1,
                x.Rating, x.Played, x.Wins, Percent(x.Wins, x.Played), x.Id == userId))
            .ToList();

        return new OnlineLeaderboard("Rating", "rating",
            $"Iedereen start op {MatchRules.StartRating}. Win van sterkere spelers en je stijgt sneller.", entries);
    }

    // ---------- Statistieken ----------

    public async Task<OnlineStats> GetStatsAsync(string userId)
    {
        var matches = await FinishedAsync();
        var (rating, best, longestStreak) = Ratings(matches);
        var cards = await results.UserCardsAsync();
        var all = matches.SelectMany(m => m.Players).ToList();
        var byUser = all.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var names = all.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.First().UserName);

        var hall = new List<StatCard>();
        void Add(string icon, string title, string caption, string user, string value) =>
            hall.Add(new StatCard(icon, title, caption, names[user], cards.GetValueOrDefault(user)?.AvatarId, value, user == userId));

        if (byUser.Count > 0)
        {
            var wins = byUser.Select(kv => (User: kv.Key, Wins: kv.Value.Count(p => p.Won))).MaxBy(x => x.Wins);
            if (wins.Wins > 0) Add("🏅", "Meeste wedstrijdzeges", "Vaakst eerste in een partijtje", wins.User, $"{wins.Wins}×");

            var top = rating.MaxBy(kv => kv.Value);
            Add("📊", "Hoogste rating", "Sterkste online speler", top.Key, $"{Math.Round(top.Value)}");

            var sniper = byUser.Select(kv => (User: kv.Key, Count: kv.Value.Sum(p => p.FirstCorrect))).MaxBy(x => x.Count);
            if (sniper.Count > 0) Add("🎯", "Snelste schutter", "Vaakst als eerste het juiste antwoord", sniper.User, $"{sniper.Count}×");

            var bestScore = all.MaxBy(p => p.Score)!;
            if (bestScore.Score > 0) Add("💥", "Hoogste wedstrijdscore", "Meeste punten in één wedstrijd", bestScore.UserId, $"{bestScore.Score}");

            var unbeaten = longestStreak.MaxBy(kv => kv.Value);
            if (unbeaten.Value >= 2) Add("🔥", "Onklopbaar", "Langste reeks gewonnen wedstrijden", unbeaten.Key, $"{unbeaten.Value} op rij");

            var winRate = byUser.Where(kv => kv.Value.Count >= 5)
                .Select(kv => (User: kv.Key, Pct: Percent(kv.Value.Count(p => p.Won), kv.Value.Count)))
                .OrderByDescending(x => x.Pct).FirstOrDefault();
            if (winRate.User is not null) Add("📈", "Winnaarsmentaliteit", "Hoogste winstpercentage (min. 5 wedstrijden)", winRate.User, $"{winRate.Pct}%");

            var busiest = byUser.MaxBy(kv => kv.Value.Count);
            Add("🎮", "Vaste waarde", "Meeste online wedstrijden gespeeld", busiest.Key, $"{busiest.Value.Count}");
        }

        var mine = byUser.GetValueOrDefault(userId) ?? [];
        var me = new OnlineMyStats(
            mine.Count,
            mine.Count(p => p.Won),
            (int)Math.Round(rating.GetValueOrDefault(userId, MatchRules.StartRating)),
            (int)Math.Round(best.GetValueOrDefault(userId, MatchRules.StartRating)),
            mine.Sum(p => p.FirstCorrect),
            mine.Count == 0 ? 0 : mine.Max(p => p.Score));

        return new OnlineStats(me, hall);
    }

    private static int Percent(int part, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * part / total);
}
