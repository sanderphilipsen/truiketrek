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
    }
}
