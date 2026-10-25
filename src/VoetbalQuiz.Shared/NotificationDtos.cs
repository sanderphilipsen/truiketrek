namespace VoetbalQuiz.Shared;

/// <summary>Soorten meldingen in het meldingencentrum. Nieuws zijn de releasenota's voor iedereen.</summary>
public enum NotificationKind { Nieuws, NieuweFeedback, FeedbackAntwoord, Voorraad, Verwijderaanvraag }

/// <summary>Eén regel in het meldingencentrum. <paramref name="IsNew"/>: nog niet gezien vóór dit bezoek.</summary>
public record NotificationItem(
    NotificationKind Kind, string Title, string? Text, string? Link, DateTime CreatedAt, bool IsNew);

public record NotificationCount(int Unread);

/// <summary>Een releasenota schrijven (admin). Link is een pagina in de app, bv. "academie"; leeg = geen link.</summary>
public record AnnouncementRequest(string Title, string Text, string? Link);

public record AnnouncementItem(int Id, string Title, string Text, string? Link, DateTime CreatedAt);

public static class NotificationRules
{
    /// <summary>Gelezen meldingen verdwijnen na zoveel dagen.</summary>
    public const int KeepReadDays = 30;

    /// <summary>Hoeveel releasenota's het meldingencentrum hooguit toont.</summary>
    public const int MaxAnnouncements = 10;

    /// <summary>De admins krijgen een waarschuwing als de ongebruikte vragen van een type niet meer volstaan voor zoveel dagen daily.</summary>
    public const int StockWarnDays = 14;
}
