namespace VoetbalQuiz.Shared;

/// <summary>Een kamer van het Spookhuis: de tag die een vraag in de kamer zet, de naam en een emoji.</summary>
public record Kamer(string Tag, string Name, string Emoji);

/// <summary>
/// De Halloween Special als "Het Spookhuis": elke vraag speelt in een kamer. De kamer volgt uit de tags van de vraag
/// (een vraag met tag halloween krijgt er één kamertag bij); de spelersrondes zijn de kamer van wie op Halloween jarig is.
/// Puur aankleding: de vragen, de volgorde en de punten blijven die van een gewone special.
/// </summary>
public static class Spookhuis
{
    /// <summary>Het thema van de special (zie <see cref="SpecialTemplates"/>).</summary>
    public const string Theme = "halloween";

    public static readonly Kamer Jarig = new(Theme, "Jarig op Halloween", "🎂");

    /// <summary>Een vraag zonder kamertag (bv. een gewone vraag die een te kleine voorraad aanvult).</summary>
    public static readonly Kamer Zolder = new("zolder", "Op zolder", "🕸️");

    public static readonly List<Kamer> Kamers =
    [
        new("spookgoal", "Spookgoals", "👻"),
        new("vloek", "De vloek", "🧿"),
        new("duivels", "Duivels", "😈"),
        new("monster", "Monsters", "🧛"),
        new("remontade", "Uit de dood opgestaan", "🧟"),
    ];

    /// <summary>
    /// De kamer van een vraag: de spelersrondes (<paramref name="fromPlayers"/>) en een themavraag zonder kamertag zijn
    /// "Jarig op Halloween", een vraag met een kamertag die kamer, de rest de zolder.
    /// </summary>
    public static Kamer KamerVan(IEnumerable<string> tags, bool fromPlayers)
    {
        if (fromPlayers) return Jarig;
        var list = tags.ToList();
        return Kamers.FirstOrDefault(k => list.Contains(k.Tag)) ?? (list.Contains(Theme) ? Jarig : Zolder);
    }
}
