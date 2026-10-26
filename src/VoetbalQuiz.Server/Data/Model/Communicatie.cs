using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

public class Feedback
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public FeedbackType Type { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Nieuw;
    public string? Reply { get; set; }
}

/// <summary>Een melding voor één gebruiker, bv. "nieuwe bug gemeld" voor een admin of "je melding is opgelost".</summary>
public class Notification
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public NotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public string? Text { get; set; }
    /// <summary>Pagina in de app waar de melding heen gaat, bv. "feedback".</summary>
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>Een releasenota van een admin voor alle spelers ("Wat is er nieuw"). Gezien per speler via AppUser.AnnouncementsReadAt.</summary>
public class Announcement
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Versie van de deploy waarmee deze releasenota automatisch meekwam; null als een admin ze zelf schreef.</summary>
    public string? Version { get; set; }
}

/// <summary>
/// Een toestel (browser) dat pushmeldingen krijgt: het abonnement dat de browser gaf. Een speler kan er meerdere hebben;
/// hetzelfde toestel (endpoint) hoort bij hooguit één speler, de laatste die er zich aanmeldde.
/// </summary>
public class PushSubscription
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    /// <summary>Het adres bij de pushdienst van de browser (Google, Mozilla, Apple, Microsoft).</summary>
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Hoe lang de velden van een pushabonnement mogen zijn (ruim boven wat browsers geven).</summary>
public static class PushSubscriptionLimits
{
    public const int Endpoint = 1000;
    public const int Key = 200;
}

/// <summary>Indexen, conversies en relaties van feedback, meldingen en releasenota's.</summary>
internal static class CommunicatieModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Feedback>().Property(f => f.Type).HasConversion<string>();
        builder.Entity<Feedback>().Property(f => f.Status).HasConversion<string>();
        builder.Entity<Notification>().Property(n => n.Kind).HasConversion<string>();
        builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.Entity<Announcement>().HasIndex(a => a.CreatedAt);
        builder.Entity<Announcement>().Property(a => a.Version).HasMaxLength(40);
        builder.Entity<Announcement>().HasIndex(a => a.Version).IsUnique();
        builder.Entity<PushSubscription>().Property(p => p.Endpoint).HasMaxLength(PushSubscriptionLimits.Endpoint);
        builder.Entity<PushSubscription>().Property(p => p.P256dh).HasMaxLength(PushSubscriptionLimits.Key);
        builder.Entity<PushSubscription>().Property(p => p.Auth).HasMaxLength(PushSubscriptionLimits.Key);
        builder.Entity<PushSubscription>().HasIndex(p => p.Endpoint).IsUnique();
        builder.Entity<PushSubscription>().HasIndex(p => p.UserId);
    }
}
