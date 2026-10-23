using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Waakt over de voorraad van de daily en verwittigt de admins: wanneer een type voor het eerst vragen herhaalt, wanneer de
/// ongebruikte goedgekeurde vragen niet meer volstaan voor <see cref="NotificationRules.StockWarnDays"/> dagen weekplanning,
/// en wanneer de daily moest terugvallen op "Wie ben ik?". De spelertypes (Wie ben ik, Kluts, Counter) delen één voorraad.
/// Bewaart zelf niets: alles volgt uit de reeksen die er al waren (dailies en specials tellen allebei mee voor de rotatie).
/// </summary>
public class StockAlertService(QuizDbContext db, QuestionPicker picker, NotificationService notifications, ILogger<StockAlertService> logger)
{
    /// <summary>Na het aanmaken van een daily. Een fout hier mag de daily nooit tegenhouden.</summary>
    public async Task CheckAsync(DailyChallenge daily)
    {
        try
        {
            await CheckCoreAsync(daily);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voorraadcontrole van de daily van {Date} mislukt", daily.Date);
        }
    }

    /// <summary>Eén voorraad: een type uit de vragenbank, of null voor de spelers.</summary>
    private readonly record struct Use(int Challenge, QuestionType? Group, int Key);

    private async Task CheckCoreAsync(DailyChallenge daily)
    {
        var challenges = await db.DailyChallenges.Where(c => c.Date <= daily.Date)
            .Select(c => new { c.Id, c.Date, c.SpecialEventId }).ToListAsync();
        var rows = await db.DailyQuestions.Where(q => q.DailyChallenge.Date <= daily.Date)
            .Select(q => new { q.DailyChallengeId, q.Type, q.PlayerId, q.QuizItemId, q.CounterPlayerIds }).ToListAsync();

        // Volgorde van de reeksen; een vraag is een herhaling als ze al in een eerdere reeks zat.
        var order = challenges.OrderBy(c => c.Date).ThenBy(c => c.Id).Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        IEnumerable<Use> UsesOf(int challenge, QuestionType type, int? playerId, int? itemId, IEnumerable<int> counterPlayers)
        {
            if (!QuestionTypes.Get(type).FromPlayers)
                return itemId is { } item ? [new Use(challenge, type, item)] : [];
            IEnumerable<int> ids = type == QuestionType.Counter ? counterPlayers : playerId is { } p ? [p] : [];
            return ids.Select(id => new Use(challenge, null, id));
        }
        var uses = rows.SelectMany(r => UsesOf(r.DailyChallengeId, r.Type, r.PlayerId, r.QuizItemId, r.CounterPlayerIds)).ToList();
        var first = uses.GroupBy(u => (u.Group, u.Key)).ToDictionary(g => g.Key, g => g.Min(u => order[u.Challenge]));
        bool IsRepeat(Use u) => first[(u.Group, u.Key)] < order[u.Challenge];

        var items = await db.QuizItems.Where(i => i.Status != PlayerStatus.Rejected)
            .Select(i => new { i.Id, i.Type, i.Status }).ToListAsync();
        var players = await db.Players.Where(p => p.Status != PlayerStatus.Rejected)
            .Select(p => new { p.Id, p.Status }).ToListAsync();
        // Ongebruikt = nog in geen enkele reeks; goedgekeurd en te keuren apart.
        (int Approved, int Review) Fresh(QuestionType? group)
        {
            var unused = group is { } t
                ? items.Where(i => i.Type == t && !first.ContainsKey((t, i.Id))).Select(i => i.Status)
                : players.Where(p => !first.ContainsKey((null, p.Id))).Select(p => p.Status);
            var list = unused.ToList();
            return (list.Count(s => s == PlayerStatus.Approved), list.Count(s => s != PlayerStatus.Approved));
        }
        bool Approved(Use u) => u.Group is null
            ? players.Any(p => p.Id == u.Key && p.Status == PlayerStatus.Approved)
            : items.Any(i => i.Id == u.Key && i.Status == PlayerStatus.Approved);

        var need = await NeedAsync(daily.Date);
        var today = uses.Where(u => u.Challenge == daily.Id).ToList();
        foreach (var group in today.Select(u => u.Group).Distinct())
        {
            var mine = today.Where(u => u.Group == group).ToList();
            var repeats = mine.Count(IsRepeat);
            var (approved, review) = Fresh(group);
            var unit = group is null ? "spelers" : "vragen";
            var link = group is null ? "admin/spelers" : "admin/vragen";

            if (repeats > 0)
            {
                // Enkel de eerste keer: de vorige daily met deze voorraad had nog geen herhalingen.
                var previous = uses.Where(u => u.Group == group && order[u.Challenge] < order[daily.Id]
                                               && challenges.Any(c => c.Id == u.Challenge && c.SpecialEventId == null))
                    .GroupBy(u => u.Challenge).MaxBy(g => order[g.Key]);
                if (previous is not null && !previous.Any(IsRepeat))
                    await notifications.AddForAdminsAsync("", NotificationKind.Voorraad,
                        group is { } t ? $"{QuestionTypes.Get(t).Name}: vragen komen terug" : "Spelers komen terug",
                        $"Vandaag {repeats} van de {mine.Count} al eerder gevraagd. Nog {approved} ongebruikte goedgekeurde en {review} te keuren {unit}.",
                        link);
                continue;
            }

            // Vroege waarschuwing: vóór deze daily was er genoeg voor de komende weken, nu niet meer.
            var needed = need.GetValueOrDefault(group ?? QuestionType.WieBenIk);
            var before = approved + mine.Count(Approved);
            if (needed > 0 && approved < needed && before >= needed)
                await notifications.AddForAdminsAsync("", NotificationKind.Voorraad,
                    $"{(group is { } t ? QuestionTypes.Get(t).Name : "Spelers")}: voorraad raakt op",
                    $"Nog {approved} ongebruikte goedgekeurde {unit}, de komende {NotificationRules.StockWarnDays} dagen zijn er {needed} nodig. {review} te keuren.",
                    link);
        }

        // De daily moest terugvallen op "Wie ben ik?" omdat een gepland type te weinig vragen had.
        var planned = QuestionTypes.Mix(await picker.TypesForAsync(daily.Date));
        if (rows.Where(r => r.DailyChallengeId == daily.Id).All(r => r.Type == QuestionType.WieBenIk)
            && planned.Keys.Any(t => t != QuestionType.WieBenIk))
        {
            var shortages = planned
                .Select(m => (Type: m.Key, Needed: m.Key == QuestionType.Counter ? m.Value * QuestionRules.CounterPlayers : m.Value,
                    Available: QuestionTypes.Get(m.Key).FromPlayers ? players.Count : items.Count(i => i.Type == m.Key)))
                .Where(x => x.Available < x.Needed)
                .Select(x => $"{QuestionTypes.Get(x.Type).Name} ({x.Available} beschikbaar, {x.Needed} nodig)")
                .ToList();
            await notifications.AddForAdminsAsync("", NotificationKind.Voorraad, "Daily viel terug op Wie ben ik?",
                shortages.Count > 0
                    ? $"Te weinig vragen voor {string.Join(", ", shortages)}."
                    : "Een gepland type had niet genoeg geschikte vragen.",
                "admin/vragen");
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Hoeveel vragen per voorraad de daily de komende <see cref="NotificationRules.StockWarnDays"/> dagen nodig heeft volgens de
    /// weekplanning. De spelers staan onder <see cref="QuestionType.WieBenIk"/>.
    /// </summary>
    private async Task<Dictionary<QuestionType, int>> NeedAsync(DateOnly from)
    {
        var need = new Dictionary<QuestionType, int>();
        for (var d = 1; d <= NotificationRules.StockWarnDays; d++)
            foreach (var (type, count) in QuestionTypes.Mix(await picker.TypesForAsync(from.AddDays(d))))
            {
                var key = QuestionTypes.Get(type).FromPlayers ? QuestionType.WieBenIk : type;
                need[key] = need.GetValueOrDefault(key) + QuestionTypes.ItemsFor(type, count); // counter: 10 spelers, kaartdag: één kaart
            }
        return need;
    }
}
