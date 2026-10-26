using Microsoft.EntityFrameworkCore;

namespace VoetbalQuiz.Server.Data;

// ---------- Gouden Bal ----------

/// <summary>
/// De nummer 1 van een afgelopen maand. Vastgelegd zodra de maand voorbij is (zie GoldenBallService),
/// zodat de sterren blijven, ook als er later iets aan de berekening verandert. Bij een gedeelde eerste plaats: één rij per winnaar.
/// </summary>
public class GoldenBall
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int Points { get; set; }
    public DateTime AwardedAt { get; set; }
}

/// <summary>Een maand die afgesloten is voor de Gouden Bal (ook als niemand punten had), zodat we ze maar één keer berekenen.</summary>
public class GoldenBallMonth
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime SettledAt { get; set; }
}

/// <summary>Indexen, conversies en relaties van de Gouden Bal.</summary>
internal static class KlassementenModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<GoldenBall>().HasIndex(g => new { g.Year, g.Month, g.UserId }).IsUnique();
        builder.Entity<GoldenBallMonth>().HasIndex(m => new { m.Year, m.Month }).IsUnique();
    }
}
