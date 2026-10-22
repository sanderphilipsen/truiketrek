using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Shared;

/// <summary>
/// Wat de daily en de online partijtjes allebei tonen tijdens een vraag: de punten die je nu kan verdienen
/// en wanneer er een nieuwe tip of stap aankomt. De server rekent de punten zelf na.
/// </summary>
public static class QuestionDisplay
{
    public static string PointsNow(QuestionType type, QuestionView? view, double elapsed, int wrongGuesses, double timeLimit) => type switch
    {
        QuestionType.WieBenIk => GameRules.PointsAt(elapsed, wrongGuesses).ToString(),
        // Carrièrepad, wissel, rugnummer, mannetje en kluts: 100 bij de eerste stap, 10 minder per extra stap, minimum 50.
        _ when QuestionRules.Reveals(type) => QuestionRules.RevealPoints(Math.Max(1, view?.Step ?? 1), wrongGuesses).ToString(),
        QuestionType.Counter => $"{view?.Found ?? 0}/{view?.Total ?? 0}",
        QuestionType.Emoji => QuestionRules.EmojiPoints(elapsed, wrongGuesses).ToString(),
        QuestionType.Snelvuur or QuestionType.NietBij => QuestionRules.SpeedPoints(elapsed, timeLimit).ToString(),
        QuestionType.Jaar => "100",
        QuestionType.Volgorde => QuestionTypes.Get(QuestionType.Volgorde).MaxPoints.ToString(),
        QuestionType.Opstelling => $"{LineupFound(view)}/{view?.Lineup?.Count(s => s.IsBlank) ?? 0}",
        _ => "",
    };

    /// <summary>
    /// Tekst voor een (verborgen) aria-live-regio bij de klok: schermlezers lezen de klok niet elke seconde voor,
    /// enkel als hij onder de 10 en onder de 3 seconden zakt.
    /// </summary>
    public static string ClockAnnouncement(double secondsLeft) => secondsLeft switch
    {
        <= 0 => "",
        <= 3 => "Laatste 3 seconden",
        <= 10 => "Nog minder dan 10 seconden",
        _ => "",
    };

    public static string PointsLabel(QuestionType type) => type switch
    {
        QuestionType.Jaar => "punten bij het exacte jaar",
        QuestionType.Opstelling => "namen gevonden",
        QuestionType.Volgorde => "punten bij de juiste volgorde",
        QuestionType.Counter => $"spelers geraden (+{QuestionRules.CounterPointsPerPlayer} per speler)",
        QuestionType.WieBenIk or QuestionType.Emoji => "punten",
        _ when QuestionRules.Reveals(type) => "punten",
        _ => "punten, sneller = meer",
    };

    /// <summary>Is er een nieuwe tip of stap vrijgegeven sinds de laatste toestand van de server?</summary>
    public static bool MoreHintsDue(QuestionType type, QuestionView? view, int visibleHints, double elapsed, double timeLimit) => type switch
    {
        QuestionType.WieBenIk => GameRules.VisibleHints(elapsed) > visibleHints,
        _ when QuestionRules.Reveals(type) =>
            QuestionRules.RevealStep(elapsed, view?.Steps ?? 1) > (view?.Step ?? 1)
            && elapsed < timeLimit - QuestionRules.CarriereExtraSeconds,
        _ => false,
    };

    /// <summary>Opstelling: hoeveel open plaatsen al ingevuld zijn.</summary>
    public static int LineupFound(QuestionView? view) => view?.Lineup?.Count(s => s.IsBlank && s.Name is not null) ?? 0;

    public static string Percent(double fraction) =>
        (Math.Clamp(fraction, 0, 1) * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
