namespace VoetbalQuiz.Shared;

/// <summary>
/// De Gouden Bal: de nummer 1 van het maandklassement. Elke Gouden Bal is voor altijd een ster op je shirt;
/// de hele volgende maand draagt de winnaar ook een gouden rand rond de avatar.
/// </summary>
public static class GoldenBallRules
{
    /// <summary>Zoveel sterren tekenen we op het shirt (3 per rij); daarboven blijft het bij dit aantal.</summary>
    public const int MaxStarsShown = 9;

    /// <summary>Speling na de maandwissel, zodat een vraag die net voor middernacht startte nog afgewerkt is.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Een maand wordt pas afgesloten als ook in UTC de volgende maand begonnen is (online wedstrijden tellen per UTC-maand),
    /// plus <see cref="SettleDelay"/>. In België is dat tussen 1 en 2 uur 's nachts op de eerste.
    /// Specials die in de maand startten, moeten bovendien afgelopen zijn (dat controleert de server).
    /// </summary>
    public static bool CanSettle(int year, int month, DateTime utcNow) =>
        utcNow >= new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1) + SettleDelay;

    /// <summary>De maand waarvan de winnaar vandaag de gouden rand draagt: de vorige.</summary>
    public static (int Year, int Month) HolderMonth(DateOnly today)
    {
        var last = today.AddMonths(-1);
        return (last.Year, last.Month);
    }

    /// <summary>De nieuwe Gouden Bal komt groot op het dashboard op de eerste van de maand, of op de dag dat hij later toegekend werd.</summary>
    public static bool IsAnnounceDay(DateOnly today, DateOnly? awardedOn) => today.Day == 1 || awardedOn == today;

    public static string Title(int month) => $"Gouden Bal {DutchDates.Month(month)}";

    public static string Explanation =>
        "Wie op het einde van de maand bovenaan het maandklassement staat, wint de Gouden Bal: " +
        "een ster op het shirt (voor altijd, één per Gouden Bal) en de hele volgende maand een gouden rand rond de avatar. " +
        "Bij een gedeelde eerste plaats krijgen ze hem allebei.";
}

public record GoldenBallWinner(int Year, int Month, string UserName, string? AvatarId, int Points, bool IsMe)
{
    public string Title => GoldenBallRules.Title(Month);
}

/// <param name="Holders">Winnaar(s) van vorige maand: zij dragen deze maand de gouden rand.</param>
/// <param name="Announce">Eerste dag van de maand: de nieuwe Gouden Bal komt groot op het dashboard.</param>
/// <param name="Stars">Gebruikersnaam → aantal gewonnen Gouden Ballen.</param>
/// <param name="History">Alle Gouden Ballen, nieuwste eerst.</param>
public record GoldenBallOverview(
    List<GoldenBallWinner> Holders, bool Announce, Dictionary<string, int> Stars, List<GoldenBallWinner> History, string Explanation);
