using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Het maandklassement: daily, specials en online wedstrijden in één ranking.
/// Zie <see cref="MonthRules"/> voor de regels.
/// </summary>
public class MonthRankingService(QuizDbContext db, ResultsService results)
{
    public async Task<MonthRanking> GetAsync(string userId, int year, int month)
    {
        var entries = await ComputeAsync(year, month);
        var cards = await results.UserCardsAsync();
        var ranked = new List<MonthEntry>();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            // Gelijke totalen = gedeelde plaats.
            var rank = i > 0 && entries[i - 1].Total == e.Total ? ranked[i - 1].Rank : i + 1;
            ranked.Add(e with
            {
                Rank = rank,
                AvatarId = cards.GetValueOrDefault(e.UserName)?.AvatarId,
                Level = cards.GetValueOrDefault(e.UserName)?.Level ?? 1,
                IsMe = e.UserName == userId,
            });
        }

        // UserName bevat tijdens het rekenen de user-id; nu de echte naam invullen.
        var names = await db.Users.ToDictionaryAsync(u => u.Id, u => u.UserName!);
        return new MonthRanking(DutchDates.MonthYear(year, month), year, month, MonthRules.Explanation,
            ranked.Select(e => e with { UserName = names.GetValueOrDefault(e.UserName, "?") }).ToList());
    }

    /// <summary>Ruwe maandtotalen per gebruiker (UserName bevat hier nog de user-id), gesorteerd van hoog naar laag.</summary>
    public async Task<List<MonthEntry>> ComputeAsync(int year, int month)
    {
        var (from, to) = MonthRules.Days(year, month);

        // Daily en specials: de scores zelf. Een special telt in de maand waarin hij start.
        var played = await db.Attempts
            .Where(a => a.AnsweredAt != null
                        && a.DailyQuestion.DailyChallenge.Date >= from && a.DailyQuestion.DailyChallenge.Date < to)
            .Select(a => new
            {
                a.UserId,
                a.DailyQuestion.DailyChallengeId,
                IsSpecial = a.DailyQuestion.DailyChallenge.SpecialEventId != null,
                a.Points,
            })
            .ToListAsync();

        // Podiumbonussen voor afgelopen dailies en specials.
        var bonus = new Dictionary<string, (int Points, int Podiums, int Wins)>();
        foreach (var podium in await results.PodiumsAsync(from, to))
        {
            for (var place = 1; place <= podium.UserIds.Count; place++)
            {
                var id = podium.UserIds[place - 1];
                var b = bonus.GetValueOrDefault(id);
                bonus[id] = (b.Points + MonthRules.Bonus(place), b.Podiums + 1, b.Wins + (place == 1 && !podium.IsSpecial ? 1 : 0));
            }
        }

        // Online: punten per wedstrijd volgens je eindplaats; enkel de beste wedstrijden tellen.
        var fromUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddMonths(1);
        var matchRows = await db.MatchPlayers
            .Where(p => p.Match.Status == MatchStatus.Finished && p.Match.FinishedAt >= fromUtc && p.Match.FinishedAt < toUtc)
            .Select(p => new
            {
                p.MatchId, p.UserId, Rank = p.Rank ?? 99,
                Counts = p.Match.Players.Count(o => o.LeftAt == null) >= MatchRules.StayersForWin,
            })
            .ToListAsync();
        // Als enige overgebleven: geen zege en geen punten (zie MatchRules.StayersForWin).
        var online = matchRows.GroupBy(r => r.MatchId)
            .SelectMany(match => match.Select(p => p.Rank == 1 && !p.Counts
                ? new { p.UserId, Won = false, Points = 0 }
                : new
                {
                    p.UserId,
                    Won = p.Rank == 1,
                    Points = MonthRules.OnlinePoints(match.Count(o => o.UserId != p.UserId && o.Rank > p.Rank)
                                                     + 0.5 * match.Count(o => o.UserId != p.UserId && o.Rank == p.Rank)),
                }))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (
                Points: g.OrderByDescending(x => x.Points).Take(MonthRules.OnlineBestMatches).Sum(x => x.Points),
                Matches: g.Count(),
                Wins: g.Count(x => x.Won)));

        var users = played.Select(p => p.UserId).Concat(online.Keys).Distinct();
        return users.Select(id =>
            {
                var mine = played.Where(p => p.UserId == id).ToList();
                var daily = mine.Where(p => !p.IsSpecial).ToList();
                var specials = mine.Where(p => p.IsSpecial).ToList();
                var b = bonus.GetValueOrDefault(id);
                var o = online.GetValueOrDefault(id);
                var dailyPoints = daily.Sum(p => p.Points);
                var specialPoints = specials.Sum(p => p.Points);
                return new MonthEntry(0, id, null, 1, false,
                    dailyPoints + specialPoints + o.Points + b.Points,
                    dailyPoints, specialPoints, o.Points, b.Points,
                    daily.Select(p => p.DailyChallengeId).Distinct().Count(), b.Podiums, b.Wins,
                    o.Matches, o.Wins, specials.Select(p => p.DailyChallengeId).Distinct().Count());
            })
            .OrderByDescending(e => e.Total).ThenByDescending(e => e.Bonus).ThenBy(e => e.UserName)
            .ToList();
    }

    /// <summary>Top 3 van elke afgelopen maand: wie welke plaats haalde (user-id, plaats, totaal).</summary>
    public async Task<List<(string UserId, string Season, int Place, int Points)>> PodiumsPerMonthAsync(DateOnly today)
    {
        var result = new List<(string, string, int, int)>();
        foreach (var (year, month) in await ActiveMonthsAsync(today))
        {
            var entries = await ComputeAsync(year, month);
            for (var i = 0; i < entries.Count; i++)
            {
                var place = i > 0 && entries[i - 1].Total == entries[i].Total ? result[^1].Item3 : i + 1;
                if (place > 3 || entries[i].Total == 0) break;
                result.Add((entries[i].UserName, DutchDates.MonthYear(year, month), place, entries[i].Total));
            }
        }
        return result;
    }

    /// <summary>Afgelopen maanden waarin iets gespeeld werd (de lopende maand telt nog niet mee).</summary>
    public async Task<List<(int Year, int Month)>> ActiveMonthsAsync(DateOnly today)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var days = await db.DailyChallenges.Where(c => c.Date < thisMonth).Select(c => c.Date).ToListAsync();
        var matches = await db.Matches.Where(m => m.Status == MatchStatus.Finished && m.FinishedAt != null)
            .Select(m => m.FinishedAt!.Value).ToListAsync();
        return days.Select(d => (d.Year, d.Month))
            .Concat(matches.Where(d => DateOnly.FromDateTime(d) < thisMonth).Select(d => (d.Year, d.Month)))
            .Distinct()
            .ToList();
    }
}
