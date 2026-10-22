using System.Text.Json.Serialization;

namespace VoetbalQuiz.Shared;

/// <summary>
/// Het maandklassement: de dailies en de specials van de maand.
/// Maandpunten = daily-scores + special-scores + podiumbonussen.
/// Tot en met september 2026 telden ook online wedstrijden mee; sinds oktober 2026 niet meer (zie <see cref="OnlineCountsUntil"/>).
/// </summary>
public static class MonthRules
{
    /// <summary>Bonus voor de top 3 van elke daily en elke special (pas als die dag/special afgelopen is).</summary>
    public static readonly int[] PodiumBonus = [100, 50, 25];

    /// <summary>
    /// Vanaf deze maand tellen online wedstrijden niet meer mee in het maandklassement. Oudere maanden houden hun
    /// oude telling, zodat een afgelopen stand (en de Gouden Bal van die maand) niet achteraf verschuift.
    /// </summary>
    public static readonly DateOnly OnlineCountsUntil = new(2026, 10, 1);

    /// <summary>Telt online mee in deze maand? Enkel voor maanden vóór <see cref="OnlineCountsUntil"/>.</summary>
    public static bool OnlineCounts(int year, int month) => new DateOnly(year, month, 1) < OnlineCountsUntil;

    /// <summary>Oude telling (vóór oktober 2026): punten voor elke uitgespeelde wedstrijd...</summary>
    public const int OnlineBase = 100;

    /// <summary>...plus zoveel per tegenstander die je achter je liet (gelijk geëindigd telt half).</summary>
    public const int OnlinePerOpponent = 100;

    /// <summary>Oude telling: enkel je beste online wedstrijden van de maand telden, zodat eindeloos spelen niet loonde.</summary>
    public const int OnlineBestMatches = 20;

    /// <summary>De bovenste plaatsen van de maand: zij krijgen op het einde een beker (de eerste de Gouden Bal).</summary>
    public const int PrizeZone = 3;

    /// <summary>Zoveel rijen onderaan staan in de "degradatiezone": enkel een prikkel, zonder gevolg.</summary>
    public const int DropZone = 3;

    /// <summary>Pas vanaf zoveel spelers in de maand is er een degradatiezone, anders kleurt de halve lijst rood.</summary>
    public const int DropZoneMinPlayers = 10;

    /// <summary>
    /// De dagen van een maand, als [From, To). Een daily telt in de maand van haar dag, een special in de maand van haar
    /// <b>startdatum</b>, ook als ze over twee maanden loopt: haar reeks draagt de startdatum als datum. Het maandklassement
    /// en de academie filteren allebei zo op de datum van de reeks, zodat een special overal in dezelfde maand staat.
    /// </summary>
    public static (DateOnly From, DateOnly To) Days(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        return (from, from.AddMonths(1));
    }

    public static int Bonus(int place) => place >= 1 && place <= PodiumBonus.Length ? PodiumBonus[place - 1] : 0;

    /// <summary>Oude telling: punten voor één online wedstrijd; <paramref name="beaten"/> mag x,5 zijn bij een gedeelde plaats.</summary>
    public static int OnlinePoints(double beaten) => (int)Math.Round(OnlineBase + OnlinePerOpponent * beaten);

    /// <summary>
    /// In welke zone een plaats staat, zoals de stand op tv: bovenaan de prijzenzone (beker), onderaan de degradatiezone
    /// (enkel als er genoeg spelers zijn). Bij een gedeelde plaats delen ze ook de zone.
    /// </summary>
    public static RankZone Zone(int rank, int players)
    {
        if (rank < 1 || players < 1) return RankZone.None;
        if (rank <= PrizeZone) return RankZone.Prize;
        return players >= DropZoneMinPlayers && rank > players - DropZone ? RankZone.Drop : RankZone.None;
    }

    public static string Explanation(int year, int month) =>
        (OnlineCounts(year, month)
            ? $"Maandpunten = je daily-scores + je special-scores + online-punten + podiumbonussen. " +
              $"Online: {OnlineBase} per wedstrijd + {OnlinePerOpponent} per tegenstander die je achter je liet; " +
              $"enkel je {OnlineBestMatches} beste wedstrijden tellen. "
            : "Maandpunten = je daily-scores + je special-scores + podiumbonussen. ") +
        $"Top 3 van elke daily en special: +{PodiumBonus[0]} / +{PodiumBonus[1]} / +{PodiumBonus[2]}.";
}

/// <summary>De zone van een rij in het maandklassement.</summary>
public enum RankZone { None, Prize, Drop }

/// <summary>
/// Eén rij in het maandklassement, met de onderdelen die het totaal vormen. <paramref name="PreviousRank"/> is de plaats
/// op het einde van gisteren (null als je er toen nog niet in stond, of bij een afgelopen maand: die stand is definitief).
/// De online-velden blijven enkel gevuld voor maanden waarin online nog meetelde.
/// </summary>
public record MonthEntry(
    int Rank, string UserName, string? AvatarId, int Level, bool IsMe,
    int Total, int Daily, int Specials, int Online, int Bonus,
    int DaysPlayed, int Podiums, int DaysWon, int Matches, int MatchesWon, int SpecialsPlayed,
    int? PreviousRank = null)
{
    /// <summary>Zoveel plaatsen gestegen sinds gisteren (negatief = gezakt, 0 = gelijk, null = nieuw of niet van toepassing).</summary>
    [JsonIgnore]
    public int? Change => PreviousRank is { } p ? p - Rank : null;
}

/// <summary>
/// Het maandklassement. <paramref name="ShowMovement"/>: toon stijgen en dalen tegenover gisteren (enkel in de lopende maand).
/// </summary>
public record MonthRanking(string Title, int Year, int Month, string Explanation, List<MonthEntry> Entries, bool ShowMovement = false);
