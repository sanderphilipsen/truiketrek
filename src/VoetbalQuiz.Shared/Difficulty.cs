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

    /// <summary>Het gemiddelde van een reeks, afgerond op een halve ster; 0 als de reeks leeg is.</summary>
    public static double Average(IEnumerable<double> difficulties)
    {
        var list = difficulties.ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Average() * 2, MidpointRounding.AwayFromZero) / 2;
    }
}
