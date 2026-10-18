namespace VoetbalQuiz.Shared;

/// <summary>
/// Spelregels die zowel de server (puntentelling) als de client (weergave) gebruiken.
/// </summary>
public static class GameRules
{
    public const int QuestionsPerDay = 10;
    public const int HintCount = 5;

    /// <summary>Elke zoveel seconden verschijnt er een nieuwe tip.</summary>
    public const double HintIntervalSeconds = 5;

    /// <summary>Hoe lang je na het laatste tipniveau nog kan antwoorden voor 50 punten.</summary>
    public const double LastChanceSeconds = 5;

    /// <summary>Punten die er per foute poging afgaan.</summary>
    public const int WrongGuessPenalty = 5;

    /// <summary>Speling voor netwerkvertraging bij het bepalen van het tipniveau.</summary>
    public const double LatencyGraceSeconds = 0.5;

    public const int LastChancePoints = 50;

    /// <summary>Totale tijd per vraag (30 seconden).</summary>
    public static double TimeLimitSeconds => HintCount * HintIntervalSeconds + LastChanceSeconds;

    /// <summary>Aantal zichtbare tips na <paramref name="elapsedSeconds"/> seconden (1..5).</summary>
    public static int VisibleHints(double elapsedSeconds)
    {
        if (elapsedSeconds < 0) return 1;
        return Math.Min(HintCount, 1 + (int)Math.Floor(elapsedSeconds / HintIntervalSeconds));
    }

    /// <summary>
    /// Punten die op dit moment te verdienen zijn: 100, 90, 80, 70, 60 per zichtbare tip,
    /// daarna 50 tot de tijd om is, min de strafpunten voor foute pogingen.
    /// </summary>
    public static int PointsAt(double elapsedSeconds, int wrongGuesses = 0)
    {
        if (elapsedSeconds >= TimeLimitSeconds) return 0;
        var basePoints = elapsedSeconds >= HintCount * HintIntervalSeconds
            ? LastChancePoints
            : 100 - (VisibleHints(elapsedSeconds) - 1) * 10;
        return Math.Max(0, basePoints - wrongGuesses * WrongGuessPenalty);
    }

    /// <summary>Punten voor een juist antwoord dat de server na <paramref name="elapsedSeconds"/> ontvangt.</summary>
    public static int PointsForAnswer(double elapsedSeconds, int wrongGuesses) =>
        PointsAt(Math.Max(0, elapsedSeconds - LatencyGraceSeconds), wrongGuesses);
}
