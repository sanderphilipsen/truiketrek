using Microsoft.AspNetCore.Identity;

namespace VoetbalQuiz.Server.Data;

public class AppUser : IdentityUser
{
    /// <summary>Wanneer de speler zich registreerde (UTC); gezet bij het registreren.</summary>
    public DateTime CreatedAt { get; set; }
    public string? AvatarId { get; set; }

    /// <summary>XP per onderdeel (zoals Levels.Breakdown) die de speler al zag in de beloningspopup. Null = nog nooit.</summary>
    public List<int>? SeenXpParts { get; set; }

    /// <summary>Badges die de speler al zag in de beloningspopup. Null = nog nooit.</summary>
    public List<string>? SeenBadges { get; set; }

    /// <summary>
    /// Je plaats in het klassement van vandaag bij je vorige bezoek aan Home, zodat Home "▲ 3 plaatsen" kan tonen.
    /// Geldt enkel voor de dag in <see cref="HomeRankDate"/> (Belgische datum); null = toen nog niet in het klassement.
    /// </summary>
    public int? HomeRank { get; set; }
    public DateOnly? HomeRankDate { get; set; }

    /// <summary>
    /// Laatste API-verzoek van de speler (UTC), voor "laatst online" in het admin-paneel. Hooguit om de 5 minuten
    /// bijgewerkt (zie LastSeenTracker). Null = nog nooit gezien sinds deze kolom bestaat.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>Tot wanneer de speler de releasenota's (<see cref="Announcement"/>) gezien heeft (UTC). Null = nog nooit.</summary>
    public DateTime? AnnouncementsReadAt { get; set; }

    /// <summary>De maand (eerste dag) waarvan de speler de uitreiking van de Gouden Bal al zag. Null = nog nooit.</summary>
    public DateOnly? GoldenBallSeenMonth { get; set; }

    /// <summary>Wanneer de speler vroeg om zijn account te verwijderen (UTC). Null = geen lopende aanvraag.</summary>
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>
    /// Wanneer een admin het account verwijderde (UTC). De rij blijft bestaan onder een anonieme naam, zonder e-mail of
    /// aanmeldgegevens, zodat oude klassementen niet verschuiven (zie AccountDeletionService).
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
