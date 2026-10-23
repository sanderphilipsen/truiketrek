using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Kent de Gouden Bal toe aan de nummer 1 van elke afgelopen maand (zie <see cref="GoldenBallRules"/>).
/// Zoals de dagwinnaars gebeurt dat bij het eerste verzoek na het einde van de maand.
/// </summary>
public class GoldenBallService(QuizDbContext db, TimeProvider clock, MonthRankingService ranking, ResultsService results)
{
    /// <summary>Legt de Gouden Bal vast voor elke afgelopen maand die nog niet afgesloten werd en waarvan alle punten vastliggen.</summary>
    public async Task SettleAsync()
    {
        var today = QuizClock.Today(clock);
        var now = clock.GetUtcNow().UtcDateTime;
        var settled = (await db.GoldenBallMonths.Select(m => new { m.Year, m.Month }).ToListAsync())
            .Select(m => (m.Year, m.Month)).ToHashSet();
        // Een special telt in de maand waarin hij start, ook als hij pas na de maandwissel eindigt.
        var runningSpecials = await db.SpecialEvents.Where(s => s.EndDate >= today).Select(s => s.StartDate).ToListAsync();
        var open = (await ranking.ActiveMonthsAsync(today))
            .Where(m => !settled.Contains(m)
                        && GoldenBallRules.CanSettle(m.Year, m.Month, now)
                        && !runningSpecials.Any(start => start.Year == m.Year && start.Month == m.Month))
            .Order().ToList();
        if (open.Count == 0) return;

        var added = new List<object>();
        foreach (var (year, month) in open)
        {
            var entries = await ranking.ComputeAsync(year, month);
            var best = entries.FirstOrDefault()?.Total ?? 0;
            // Gelijke stand bovenaan = gedeelde Gouden Bal, net als de gedeelde plaats in het klassement.
            if (best > 0)
                foreach (var e in entries.TakeWhile(e => e.Total == best))
                    added.Add(db.GoldenBalls.Add(new GoldenBall { Year = year, Month = month, UserId = e.UserName, Points = e.Total, AwardedAt = now }).Entity);
            added.Add(db.GoldenBallMonths.Add(new GoldenBallMonth { Year = year, Month = month, SettledAt = now }).Entity);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Enkel onze eigen rijen loslaten: de context is gedeeld met de rest van het verzoek.
            foreach (var entity in added) db.Entry(entity).State = EntityState.Detached;
            // Legde een gelijktijdig verzoek dezelfde maanden al vast, dan is er niets aan de hand; anders is het een echte fout.
            var done = await db.GoldenBallMonths.Select(m => new { m.Year, m.Month }).ToListAsync();
            if (!open.All(m => done.Any(d => d.Year == m.Year && d.Month == m.Month))) throw;
        }
    }

    public async Task<GoldenBallOverview> GetAsync(string userId)
    {
        await SettleAsync();
        var today = QuizClock.Today(clock);
        var (holderYear, holderMonth) = GoldenBallRules.HolderMonth(today);
        var cards = await results.UserCardsAsync();
        var settledAt = await db.GoldenBallMonths.Where(m => m.Year == holderYear && m.Month == holderMonth)
            .Select(m => (DateTime?)m.SettledAt).FirstOrDefaultAsync();

        var history = (await db.GoldenBalls
                .OrderByDescending(g => g.Year).ThenByDescending(g => g.Month).ThenBy(g => g.User.UserName)
                .Select(g => new { g.Year, g.Month, g.UserId, g.User.UserName, g.Points })
                .ToListAsync())
            .Select(g => new GoldenBallWinner(g.Year, g.Month, g.UserName!, cards.GetValueOrDefault(g.UserId)?.AvatarId, g.Points, g.UserId == userId))
            .ToList();

        var holders = history.Where(g => g.Year == holderYear && g.Month == holderMonth).ToList();
        return new GoldenBallOverview(
            holders,
            GoldenBallRules.IsAnnounceDay(today),
            history.GroupBy(g => g.UserName).ToDictionary(g => g.Key, g => g.Count()),
            history,
            GoldenBallRules.Explanation,
            settledAt is { } awarded && holders.Count > 0 ? await CeremonyAsync(userId, holderYear, holderMonth, awarded, holders) : null);
    }

    /// <summary>De uitreiking, als de speler ze nog niet zag en al een account had toen de Gouden Bal toegekend werd.</summary>
    private async Task<GoldenBallCeremony?> CeremonyAsync(string userId, int year, int month, DateTime awardedAt, List<GoldenBallWinner> winners)
    {
        var user = await db.Users.Where(u => u.Id == userId).Select(u => new { u.CreatedAt, u.GoldenBallSeenMonth }).SingleAsync();
        var first = new DateOnly(year, month, 1);
        if (user.CreatedAt >= awardedAt || user.GoldenBallSeenMonth >= first) return null;

        // De nummers 2 en 3 uit het maandklassement; de winnaars zelf staan al vast in GoldenBalls.
        var standing = await ranking.GetAsync(userId, year, month);
        var runnersUp = standing.Entries
            .Where(e => e.Rank > 1 && e.Rank <= MonthRules.PrizeZone && !winners.Any(w => w.UserName == e.UserName))
            .Select(e => new GoldenBallRunnerUp(e.Rank, e.UserName, e.AvatarId, e.Total, e.IsMe))
            .ToList();
        return new GoldenBallCeremony(year, month, winners, runnersUp);
    }

    /// <summary>De speler zag de uitreiking van vorige maand: niet meer tonen, ook niet op een ander toestel.</summary>
    public async Task MarkCeremonySeenAsync(string userId)
    {
        var (year, month) = GoldenBallRules.HolderMonth(QuizClock.Today(clock));
        var first = new DateOnly(year, month, 1);
        await db.Users.Where(u => u.Id == userId && (u.GoldenBallSeenMonth == null || u.GoldenBallSeenMonth < first))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.GoldenBallSeenMonth, first));
    }

    /// <summary>De Gouden Ballen van één speler (jaar, maand), oudste eerst. Voor de badges.</summary>
    public async Task<List<(int Year, int Month)>> WonByAsync(string userId)
    {
        await SettleAsync();
        return (await db.GoldenBalls.Where(g => g.UserId == userId)
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .Select(g => new { g.Year, g.Month }).ToListAsync())
            .Select(g => (g.Year, g.Month)).ToList();
    }
}
