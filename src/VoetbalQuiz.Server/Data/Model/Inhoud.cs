using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

public class Player
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<string> Answers { get; set; } = [];
    public string Country { get; set; } = "";
    public string Competition { get; set; } = "";
    public string Era { get; set; } = "modern";
    public int Difficulty { get; set; } = VoetbalQuiz.Shared.Difficulty.Default;
    public List<string> Hints { get; set; } = [];

    /// <summary>Enkel goedgekeurde spelers komen in de quiz.</summary>
    public PlayerStatus Status { get; set; } = PlayerStatus.Review;
    public List<string> Sources { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? ReviewNote { get; set; }
    public DateOnly? CheckedAt { get; set; }

    /// <summary>Aangepast in het admin-paneel: de JSON-seed overschrijft deze speler dan niet meer.</summary>
    public bool EditedInAdmin { get; set; }
    public string? UpdatedBy { get; set; }
}

// ---------- Vragenbank ----------

/// <summary>Een vraag voor de nieuwe vraagtypes (snelvuur, carrièrepad, ...). De inhoud zit als JSON in Data.</summary>
public class QuizItem
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }

    /// <summary>Unieke sleutel om de JSON-seed te koppelen (bv. de vraagtekst of het antwoord).</summary>
    public required string Key { get; set; }
    public required string Data { get; set; }
    public int Difficulty { get; set; } = VoetbalQuiz.Shared.Difficulty.Default;
    public PlayerStatus Status { get; set; } = PlayerStatus.Review;
    public List<string> Sources { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? ReviewNote { get; set; }
    public DateOnly? CheckedAt { get; set; }
    public bool EditedInAdmin { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Indexen, conversies en relaties van de spelers en de vragenbank.</summary>
internal static class InhoudModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Player>().HasIndex(p => p.Name).IsUnique();
        builder.Entity<Player>().Property(p => p.Status).HasConversion<string>();
        builder.Entity<QuizItem>().Property(q => q.Status).HasConversion<string>();
        builder.Entity<QuizItem>().Property(q => q.Type).HasConversion<string>();
        builder.Entity<QuizItem>().HasIndex(q => new { q.Type, q.Key }).IsUnique();
    }
}
