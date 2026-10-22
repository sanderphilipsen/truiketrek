using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Het maandklassement: dailies en specials in één ranking (tot en met september 2026 ook online wedstrijden).
/// Zie <see cref="MonthRules"/> voor de regels.
/// </summary>
public class MonthRankingService(QuizDbContext db, TimeProvider clock, ResultsService results)
{
    private DateOnly Today => QuizClock.Today(clock);

    public async Task<MonthRanking> GetAsync(string userId, int year, int month)
    {
        var today = Today;
        var (from, to) = MonthRules.Days(year, month);
        var current = from <= today && today < to;

        var ranked = Rank(await ComputeAsync(year, month));

        // In de lopende maand: de stand op het einde van gisteren, voor de pijltjes. Een afgelopen maand is definitief.
        var yesterday = current
            ? Rank(await ComputeAsync(year, month, until: today)).ToDictionary(e => e.UserName, e => e.Rank)
            : [];

        var cards = await results.UserCardsAsync();
        // UserName bevat tijdens het rekenen de user-id; nu de echte naam invullen.
        var names = await db.Users.ToDictionaryAsync(u => u.Id, u => u.UserName!);
        var entries = ranked.Select(e => e with
        {
            AvatarId = cards.GetValueOrDefault(e.UserName)?.AvatarId,
            Level = cards.GetValueOrDefault(e.UserName)?.Level ?? 1,
            IsMe = e.UserName == userId,
            PreviousRank = yesterday.TryGetValue(e.UserName, out var previous) ? previous : null,
            UserName = names.GetValueOrDefault(e.UserName, "?"),
        }).ToList();

        return new MonthRanking(DutchDates.MonthYear(year, month), year, month, MonthRules.Explanation(year, month), entries, current);
    }

    /// <summary>Plaatsen toekennen aan de gesorteerde totalen: gelijke totalen = gedeelde plaats.</summary>
    private static List<MonthEntry> Rank(List<MonthEntry> entries)
    {
        var ranked = new List<MonthEntry>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var rank = i > 0 && entries[i - 1].Total == entries[i].Total ? ranked[i - 1].Rank : i + 1;
            ranked.Add(entries[i] with { Rank = rank });
        }
        return ranked;
    }

    /// <summary>
    /// Ruwe maandtotalen per gebruiker (UserName bevat hier nog de user-id), gesorteerd van hoog naar laag.
    /// Met <paramref name="until"/> krijg je de stand zoals ze was aan het begin van die dag (Belgische tijd):
    /// enkel reeksen van vroegere dagen, en antwoorden en wedstrijden van vóór middernacht.
    /// </summary>
    public async Task<List<MonthEntry>> ComputeAsync(int year, int month, DateOnly? until = null)
    {
        var (from, to) = MonthRules.Days(year, month);
        if (until is { } u && u < to) to = u;
        // Enkel voor "de stand van gisteren": antwoorden en wedstrijden van na middernacht tellen nog niet.
        var untilUtc = until is null ? DateTime.MaxValue : QuizDay.StartOfDayUtc(to);

        // Daily en specials: de scores zelf. Een special telt in de maand waarin hij start.
        var played = await db.Attempts
            .Where(a => a.AnsweredAt != null && a.AnsweredAt < untilUtc
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

        var online = MonthRules.OnlineCounts(year, month)
            ? await OnlineAsync(year, month, untilUtc)
            : new Dictionary<string, (int Points, int Matches, int Wins)>();

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

    /// <summary>
    /// Oude telling (tot en met september 2026): punten per online wedstrijd volgens je eindplaats; enkel de beste
    /// wedstrijden tellen. De maand loopt hier in UTC, zoals toen; wedstrijden vanaf <paramref name="untilUtc"/> tellen niet.
    /// </summary>
    private async Task<Dictionary<string, (int Points, int Matches, int Wins)>> OnlineAsync(int year, int month, DateTime untilUtc)
    {
        var fromUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddMonths(1);
        if (untilUtc < toUtc) toUtc = untilUtc;
        var matchRows = await db.MatchPlayers
            .Where(p => p.Match.Status == MatchStatus.Finished && p.Match.FinishedAt >= fromUtc && p.Match.FinishedAt < toUtc)
            .Select(p => new
            {
                p.MatchId, p.UserId, Rank = p.Rank ?? 99,
                Stayers = p.Match.Players.Count(o => o.LeftAt == null),
                Firsts = p.Match.Players.Count(o => o.Rank == 1),
            })
            .ToListAsync();
        // Als enige overgebleven: geen zege en geen punten (zie MatchRules.StayersForWin). Een gelijkspel is geen zege.
        return matchRows.GroupBy(r => r.MatchId)
            .SelectMany(match => match.Select(p => p.Rank == 1 && p.Stayers < MatchRules.StayersForWin
                ? new { p.UserId, Won = false, Points = 0 }
                : new
                {
                    p.UserId,
                    Won = MatchRules.IsWin(p.Rank, p.Stayers, p.Firsts),
                    Points = MonthRules.OnlinePoints(match.Count(o => o.UserId != p.UserId && o.Rank > p.Rank)
                                                     + 0.5 * match.Count(o => o.UserId != p.UserId && o.Rank == p.Rank)),
                }))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (
                Points: g.OrderByDescending(x => x.Points).Take(MonthRules.OnlineBestMatches).Sum(x => x.Points),
                Matches: g.Count(),
                Wins: g.Count(x => x.Won)));
    }

    /// <summary>Top 3 van elke afgelopen maand: wie welke plaats haalde (user-id, plaats, totaal).</summary>
    public async Task<List<(string UserId, string Season, int Place, int Points)>> PodiumsPerMonthAsync(DateOnly today)
    {
        var result = new List<(string, string, int, int)>();
        foreach (var (year, month) in await ActiveMonthsAsync(today))
        {
            foreach (var e in Rank(await ComputeAsync(year, month)))
            {
                if (e.Rank > 3 || e.Total == 0) break;
                result.Add((e.UserName, DutchDates.MonthYear(year, month), e.Rank, e.Total));
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
            .Concat(matches.Where(d => DateOnly.FromDateTime(d) < thisMonth)
                .Select(d => (d.Year, d.Month))
                .Where(m => MonthRules.OnlineCounts(m.Year, m.Month)))
            .Distinct()
            .ToList();
    }
}
