namespace VoetbalQuiz.Client.Shared;

/// <summary>
/// Gegevens die in de privacyverklaring, de gebruiksvoorwaarden en de FAQ terugkomen. Er is nog geen e-mailadres
/// voor de app: contact loopt via de VAR. Komt er een adres, pas dan de zinnen hieronder aan.
/// </summary>
public static class LegalInfo
{
    /// <summary>Datum van de laatste wijziging aan de privacyverklaring of de voorwaarden.</summary>
    public const string Updated = "25 oktober 2026";

    public const int MinimumAge = 13;

    public const string ContactSentence =
        "Je bereikt de beheerder via de VAR in de app (Startpagina › Naar de VAR). Heb je geen account (meer), " +
        "vraag dan een speler die je kent om de beheerder te verwittigen.";

    public const string ContactShort = "stuur een bericht via de VAR in de app.";
}
