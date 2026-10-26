namespace VoetbalQuiz.Shared;

/// <summary>
/// Pushmeldingen voor de app. <paramref name="PublicKey"/> is de publieke VAPID-sleutel; null = pushmeldingen staan niet
/// ingesteld op de server (dan toont de app er niets over). <paramref name="DailyReminder"/>: de herinnering voor de daily
/// staat aan (standaard aan, uit te zetten in je spelersfiche).
/// </summary>
public record PushConfig(string? PublicKey, bool DailyReminder);

/// <summary>Een toestel dat meldingen wil krijgen: het abonnement van de browser (endpoint en sleutels).</summary>
public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);

public record PushUnsubscribeRequest(string Endpoint);

public record DailyReminderRequest(bool On);

public static class PushRules
{
    /// <summary>Vanaf dit uur (Belgische tijd) krijgt wie de daily nog niet speelde een herinnering.</summary>
    public const int DailyReminderHour = 19;

    /// <summary>
    /// Na dit uur geen herinnering meer, bv. als de server laat op de avond herstart: midden in de nacht een melding
    /// krijgen voor een daily die bijna voorbij is, helpt niemand.
    /// </summary>
    public const int DailyReminderUntilHour = 23;

    /// <summary>Zoveel toestellen per speler; een nieuw toestel daarboven verdringt het oudste.</summary>
    public const int MaxDevices = 10;

    /// <summary>Is het nu (Belgische tijd) het moment voor de herinnering van vandaag?</summary>
    public static bool DailyReminderDue(DateTime brussels) =>
        brussels.Hour is >= DailyReminderHour and < DailyReminderUntilHour;

    public const string DailyReminderTitle = "De daily wacht op je ⚽";

    /// <summary><paramref name="streak"/>: je reeks tot en met gisteren, die vandaag breekt als je niet speelt.</summary>
    public static string DailyReminderText(int streak) => streak >= 2
        ? $"Je speelde {streak} dagen op rij. Speel vandaag ook, anders breekt je reeks."
        : "Je speelde vandaag nog niet. Wie pakt de dagzege?";
}
