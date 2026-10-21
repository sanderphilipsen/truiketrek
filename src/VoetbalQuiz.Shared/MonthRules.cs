namespace VoetbalQuiz.Shared;

/// <summary>
/// Het maandklassement: daily, specials en online wedstrijden samen.
/// Maandpunten = daily-scores + special-scores + online-punten + podiumbonussen.
/// </summary>
public static class MonthRules
{
    /// <summary>Bonus voor de top 3 van elke daily en elke special (pas als die dag/special afgelopen is).</summary>
    public static readonly int[] PodiumBonus = [100, 50, 25];

    /// <summary>Online: punten voor elke uitgespeelde wedstrijd...</summary>
    public const int OnlineBase = 100;

    /// <summary>...plus zoveel per tegenstander die je achter je liet (gelijk geëindigd telt half).</summary>
    public const int OnlinePerOpponent = 100;

    /// <summary>Enkel je beste online wedstrijden van de maand tellen, zodat eindeloos spelen niet loont.</summary>
    public const int OnlineBestMatches = 20;

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

    /// <summary>Punten voor één online wedstrijd: <paramref name="beaten"/> mag x,5 zijn bij een gedeelde plaats.</summary>
    public static int OnlinePoints(double beaten) => (int)Math.Round(OnlineBase + OnlinePerOpponent * beaten);

    public static string Explanation =>
        $"Maandpunten = je daily-scores + je special-scores + online-punten + podiumbonussen. " +
        $"Online: {OnlineBase} per wedstrijd + {OnlinePerOpponent} per tegenstander die je achter je liet; " +
        $"enkel je {OnlineBestMatches} beste wedstrijden tellen. " +
        $"Top 3 van elke daily en special: +{PodiumBonus[0]} / +{PodiumBonus[1]} / +{PodiumBonus[2]}.";
}

/// <summary>Eén rij in het maandklassement, met de onderdelen die het totaal vormen.</summary>
public record MonthEntry(
    int Rank, string UserName, string? AvatarId, int Level, bool IsMe,
    int Total, int Daily, int Specials, int Online, int Bonus,
    int DaysPlayed, int Podiums, int DaysWon, int Matches, int MatchesWon, int SpecialsPlayed);

public record MonthRanking(string Title, int Year, int Month, string Explanation, List<MonthEntry> Entries);
