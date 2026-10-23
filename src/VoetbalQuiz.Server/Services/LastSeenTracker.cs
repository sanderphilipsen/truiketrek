using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Houdt bij wanneer een speler het laatst online was (<see cref="AppUser.LastSeenAt"/>), voor het admin-paneel.
/// Niet bij elk verzoek naar de database: per speler hooguit eens per <see cref="Interval"/>. Het geheugen is per
/// serverproces; na een herstart schrijft de eerste aanvraag dus meteen weer. Singleton (zie Program.cs).
/// </summary>
public class LastSeenTracker(TimeProvider clock)
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, DateTime> _written = new();

    /// <summary>True als het tijd is om deze speler weg te schrijven; onthoudt dat meteen, zodat de volgende verzoeken wachten.</summary>
    public bool ShouldRecord(string userId, DateTime nowUtc)
    {
        if (_written.TryGetValue(userId, out var last) && nowUtc - last < Interval) return false;
        _written[userId] = nowUtc;
        return true;
    }

    /// <summary>Zet LastSeenAt van de speler op nu, als dat lang genoeg geleden is.</summary>
    public async Task TouchAsync(string userId, QuizDbContext db)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!ShouldRecord(userId, now)) return;
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now));
    }
}
