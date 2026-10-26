using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

public class Match
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string HostUserId { get; set; }
    public AppUser Host { get; set; } = null!;
    public int Seats { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Lobby;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>Gekozen vraagtypes: één type of een mix (leeg = "Wie ben ik?", zoals de oudere partijtjes).</summary>
    public List<QuestionType> Types { get; set; } = [];

    /// <summary>Derby mode: elke speler krijgt bij de aftrap een wapen (zie <see cref="DerbyRules"/>).</summary>
    public bool Derby { get; set; }

    /// <summary>Hoeveel vragen de organisator koos (zie <see cref="MatchRules.ClampQuestions"/>).</summary>
    public int QuestionCount { get; set; } = MatchRules.DefaultQuestions;

    /// <summary>
    /// Concurrency token van de hele wedstrijd: elke wijziging (lobby, gok, wapen, de klok van de engine) verhoogt het.
    /// Schreef iemand anders intussen, dan begint <see cref="Services.MatchService"/> opnieuw vanaf een verse toestand.
    /// </summary>
    public int Version { get; set; }
    public List<MatchPlayer> Players { get; set; } = [];
    public List<MatchQuestion> Questions { get; set; } = [];
}

public class MatchPlayer
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    /// <summary>Verlaten tijdens de wedstrijd: telt niet meer mee om een vraag vroeg af te sluiten.</summary>
    public DateTime? LeftAt { get; set; }
    public int Score { get; set; }
    public int? Rank { get; set; }

    // Derby: één wapen per speler, één keer in te zetten.
    public DerbyWeapon? Weapon { get; set; }
    public DateTime? WeaponUsedAt { get; set; }
    /// <summary>De vraag waarop het wapen effect heeft (klaargezet: de volgende vraag).</summary>
    public int? WeaponQuestion { get; set; }
    /// <summary>Rode kaart: wie ze kreeg.</summary>
    public string? WeaponTargetUserId { get; set; }
    /// <summary>Wat het wapen anderen kostte of jezelf opleverde in punten, voor het derby-rapport.</summary>
    public int WeaponEffect { get; set; }
}

public class MatchQuestion : IQuizQuestion
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public int Number { get; set; }
    public QuestionType Type { get; set; } = QuestionType.WieBenIk;

    /// <summary>Bij "Wie ben ik?" en kluts: de speler.</summary>
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Bij de types uit de vragenbank: de vraag.</summary>
    public int? QuizItemId { get; set; }
    public QuizItem? QuizItem { get; set; }

    /// <summary>Opstelling: welke plaatsen ingevuld moeten worden. Voor iedereen in het partijtje dezelfde.</summary>
    public List<int> Blanks { get; set; } = [];

    /// <summary>Counter: de spelers (ids), in de volgorde waarin ze verschijnen.</summary>
    public List<int> CounterPlayerIds { get; set; } = [];

    /// <summary>Counter: de spelers zelf (niet opgeslagen, ingeladen door MatchService).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<Player> CounterPlayers { get; set; } = [];

    /// <summary>Online is er geen kaart (een solo-type, zie <see cref="QuestionTypeInfo.Alone"/>), dus nooit een deel.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int Part => 0;

    /// <summary>Wanneer de eerste tip verschijnt (null = nog niet aan de beurt).</summary>
    public DateTime? StartsAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<MatchAnswer> Answers { get; set; } = [];

    // Derby: wapens op deze vraag (elk maximaal één keer per vraag).
    /// <summary>Trump booster: het antwoord van deze speler wordt het juiste.</summary>
    public string? TrumpUserId { get; set; }
    /// <summary>Bengaals vuur: wie het afstak en wanneer (klaargezet = vanaf de start van de vraag).</summary>
    public string? FlareUserId { get; set; }
    public DateTime? FlareAt { get; set; }
    /// <summary>Wissel: wie de vraag liet vervangen.</summary>
    public string? SwapUserId { get; set; }
}

/// <summary>
/// Het antwoord van een speler op een online vraag: dezelfde poging als in de daily (<see cref="AttemptState"/>, met
/// <see cref="AttemptState.Guesses"/> als concurrency token), plus wat online erbij komt (positie, bonus, derby).
/// <see cref="AttemptState.Points"/> zijn de punten zonder positiebonus (ook deelpunten bij jaar, opstelling en counter;
/// kolom <c>SpeedPoints</c>). <see cref="AttemptState.StartedAt"/> is de start van de vraag (de klok is gedeeld).
/// </summary>
public class MatchAnswer : AttemptState
{
    public int Id { get; set; }
    public int MatchQuestionId { get; set; }
    public MatchQuestion MatchQuestion { get; set; } = null!;
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    /// <summary>De speler paste ("Ik pas" of "Ik ben klaar").</summary>
    public bool GaveUp { get; set; }
    /// <summary>Volgorde van de juiste antwoorden, ingevuld als de vraag afloopt.</summary>
    public int? Position { get; set; }
    public int Bonus { get; set; }
    /// <summary>Derby: rode kaart gekregen van deze speler (halve punten voor deze vraag).</summary>
    public string? RedCardFromUserId { get; set; }
    /// <summary>Derby (propere handen): deze speler steelt de helft van de punten van deze vraag.</summary>
    public string? StolenByUserId { get; set; }
    /// <summary>Derby (gokchinees): dubbel of niks op deze vraag.</summary>
    public bool Gamble { get; set; }
    /// <summary>Derby (verlenging): extra seconden voor deze speler bij deze vraag.</summary>
    public int ExtraSeconds { get; set; }
}

/// <summary>Indexen, conversies en relaties van de online wedstrijden.</summary>
internal static class OnlineModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Match>().Property(m => m.Status).HasConversion<string>();
        builder.Entity<Match>().HasIndex(m => m.Status);
        builder.Entity<Match>().Property(m => m.Version).IsConcurrencyToken();
        builder.Entity<Match>().HasOne(m => m.Host).WithMany().HasForeignKey(m => m.HostUserId);
        builder.Entity<MatchPlayer>().HasIndex(p => new { p.MatchId, p.UserId }).IsUnique();
        builder.Entity<MatchPlayer>().Property(p => p.Weapon).HasConversion<string>();
        builder.Entity<MatchQuestion>().HasIndex(q => new { q.MatchId, q.Number }).IsUnique();
        builder.Entity<MatchQuestion>().Property(q => q.Type).HasConversion<string>();
        builder.Entity<MatchAnswer>().HasIndex(a => new { a.MatchQuestionId, a.UserId }).IsUnique();
        builder.Entity<MatchAnswer>().Property(a => a.Points).HasColumnName("SpeedPoints");
        builder.Entity<MatchAnswer>().Property(a => a.Guesses).IsConcurrencyToken();
    }
}
