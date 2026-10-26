using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

public class DailyChallenge
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public List<DailyQuestion> Questions { get; set; } = [];

    /// <summary>Ingevuld als dit de vragenreeks van een special is (anders: de gewone daily).</summary>
    public int? SpecialEventId { get; set; }
    public SpecialEvent? SpecialEvent { get; set; }

    /// <summary>Wordt ingevuld zodra de dag voorbij is (zie ResultsService).</summary>
    public DateTime? SettledAt { get; set; }
    public string? WinnerUserId { get; set; }
    public AppUser? Winner { get; set; }
    public int? WinnerPoints { get; set; }
}

public class DailyQuestion : IQuizQuestion
{
    public int Id { get; set; }
    public int DailyChallengeId { get; set; }
    public DailyChallenge DailyChallenge { get; set; } = null!;
    public int Number { get; set; }
    public QuestionType Type { get; set; } = QuestionType.WieBenIk;

    /// <summary>Bij "Wie ben ik?": de speler.</summary>
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Bij de andere types: de vraag uit de vragenbank.</summary>
    public int? QuizItemId { get; set; }
    public QuizItem? QuizItem { get; set; }

    /// <summary>Opstelling: welke plaatsen (0–10) ingevuld moeten worden. Voor iedereen dezelfde.</summary>
    public List<int> Blanks { get; set; } = [];

    /// <summary>Counter: de spelers (ids), in de volgorde waarin ze verschijnen.</summary>
    public List<int> CounterPlayerIds { get; set; } = [];

    /// <summary>Counter: de spelers zelf (niet opgeslagen, ingeladen door QuizService).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<Player> CounterPlayers { get; set; } = [];

    /// <summary>
    /// Kaart: welk deel van de kaartdag. Beide delen gebruiken dezelfde kaart (QuizItem): deel 1 de clubs, deel 2 de stadions.
    /// 0 bij de andere types.
    /// </summary>
    public int Part { get; set; }
}

/// <summary>Eén poging van een gebruiker op een vraag. Alle tijden zijn servertijden (UTC).</summary>
public class Attempt : AttemptState
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int DailyQuestionId { get; set; }
    public DailyQuestion DailyQuestion { get; set; } = null!;
}

// ---------- Specials ----------

/// <summary>Een themadag (bv. Christmas Special), beheerd in het admin-paneel.</summary>
public class SpecialEvent
{
    public int Id { get; set; }
    public required string Theme { get; set; }
    public required string Name { get; set; }
    /// <summary>Een lijnicoon uit <see cref="SpecialIcons"/> (naam, geen emoji).</summary>
    public string Icon { get; set; } = SpecialIcons.Default;
    public string Description { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Spelers met deze tag vormen de vragenpool (tenzij er handmatig spelers gekozen zijn).</summary>
    public string? Tag { get; set; }
    public List<int> PlayerIds { get; set; } = [];

    /// <summary>Welke vraagtypes de special mengt (leeg = enkel "Wie ben ik?").</summary>
    public List<QuestionType> Types { get; set; } = [];

    /// <summary>Kaart: het land van de kaart (ISO-code); null = willekeurig (zie QuestionPicker).</summary>
    public string? KaartCountry { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---------- Weekplanning ----------

/// <summary>Welk vraagtype de daily op een weekdag heeft, of een mix van types.</summary>
public class DailyScheduleDay
{
    public int Id { get; set; }
    public DayOfWeek Day { get; set; }
    public QuestionType Type { get; set; }

    /// <summary>Twee of meer types: die dag is een mix (Type wordt dan niet gebruikt).</summary>
    public List<QuestionType> MixTypes { get; set; } = [];

    /// <summary>Kaart: het land van de kaart (ISO-code); null = willekeurig (zie QuestionPicker).</summary>
    public string? KaartCountry { get; set; }
}

/// <summary>Een vraagtype dat de admin (tijdelijk) uitgeschakeld heeft. Het komt niet in dailies of specials.</summary>
public class DisabledQuestionType
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }
}

/// <summary>Indexen, conversies en relaties van de daily, de specials en de weekplanning.</summary>
internal static class DailyModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DailyQuestion>().Property(q => q.Type).HasConversion<string>();
        builder.Entity<DailyScheduleDay>().Property(d => d.Type).HasConversion<string>();
        builder.Entity<DailyScheduleDay>().HasIndex(d => d.Day).IsUnique();
        builder.Entity<DisabledQuestionType>().Property(d => d.Type).HasConversion<string>();
        builder.Entity<DisabledQuestionType>().HasIndex(d => d.Type).IsUnique();
        // Eén gewone daily per dag; specials hebben elk hun eigen reeks.
        builder.Entity<DailyChallenge>().HasIndex(c => c.Date).IsUnique().HasFilter("\"SpecialEventId\" IS NULL");
        builder.Entity<DailyChallenge>().HasIndex(c => c.SpecialEventId).IsUnique().HasFilter("\"SpecialEventId\" IS NOT NULL");
        builder.Entity<DailyChallenge>()
            .HasOne(c => c.Winner).WithMany()
            .HasForeignKey(c => c.WinnerUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<DailyQuestion>().HasIndex(q => new { q.DailyChallengeId, q.Number }).IsUnique();
        builder.Entity<Attempt>().HasIndex(a => new { a.UserId, a.DailyQuestionId }).IsUnique();
        builder.Entity<Attempt>().Property(a => a.Guesses).IsConcurrencyToken();
    }
}
