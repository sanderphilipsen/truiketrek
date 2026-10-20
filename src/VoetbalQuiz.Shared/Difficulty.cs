namespace VoetbalQuiz.Shared;

/// <summary>Moeilijkheid van een speler of vraag: 1 (iedereen kent het) tot 5 (enkel voor echte kenners).</summary>
public static class Difficulty
{
    public const int Min = 1, Max = 5, Default = 3;

    public static readonly List<(int Value, string Label)> Levels =
    [
        (1, "Iedereen kent het"),
        (2, "Makkelijk"),
        (3, "Voetbalfan"),
        (4, "Moeilijk"),
        (5, "Echte kenner"),
    ];

    public static int Clamp(int? difficulty) => Math.Clamp(difficulty ?? Default, Min, Max);

    /// <summary>"★★★☆☆" voor 3.</summary>
    public static string Stars(int difficulty) => new string('★', Clamp(difficulty)) + new string('☆', Max - Clamp(difficulty));

    /// <summary>
    /// De moeilijkheden van een reeks (daily of special): precies één opwarmer van 1 ster en één uitsmijter van 5 sterren,
    /// de rest 2, 3 en 4 sterren in de verhouding 1:3:1. Bij minder dan 3 vragen enkel het midden.
    /// Bv. 10 vragen: 1, 2, 2, 3, 3, 3, 3, 4, 4, 5.
    /// </summary>
    public static List<int> Plan(int questions)
    {
        if (questions <= 0) return [];
        var extremes = questions >= 3;
        var middle = extremes ? questions - 2 : questions;
        var side = (int)Math.Round(middle / 5.0, MidpointRounding.AwayFromZero);
        return
        [
            .. extremes ? [Min] : Array.Empty<int>(),
            .. Enumerable.Repeat(2, side),
            .. Enumerable.Repeat(3, middle - 2 * side),
            .. Enumerable.Repeat(4, side),
            .. extremes ? [Max] : Array.Empty<int>(),
        ];
    }

    /// <summary>Het gemiddelde van een reeks, afgerond op een halve ster; 0 als de reeks leeg is.</summary>
    public static double Average(IEnumerable<double> difficulties)
    {
        var list = difficulties.ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Average() * 2, MidpointRounding.AwayFromZero) / 2;
    }
}
