using Microsoft.EntityFrameworkCore;

namespace VoetbalQuiz.Server.Data;

/// <summary>
/// Eén trainingsrun: een gebruiker speelt een oude daily opnieuw. Telt nergens mee (geen klassement, XP, reeks of badges),
/// en staat daarom in een eigen tabel: alles wat <see cref="QuizDbContext.Attempts"/> leest, blijft zo vanzelf competitie.
/// De eerste run op een dag die je nooit echt speelde, toont het klassement van die dag wel als "nagespeeld"
/// (zie <c>ResultsService.ReplaysAsync</c>); die wordt afgeleid, niet bewaard.
/// </summary>
public class TrainingRun
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int DailyChallengeId { get; set; }
    public DailyChallenge DailyChallenge { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    /// <summary>Ingevuld zodra de laatste vraag beantwoord is.</summary>
    public DateTime? FinishedAt { get; set; }
    public List<TrainingAttempt> Attempts { get; set; } = [];
}

/// <summary>Een vraag binnen een trainingsrun.</summary>
public class TrainingAttempt : AttemptState
{
    public int Id { get; set; }
    public int TrainingRunId { get; set; }
    public TrainingRun TrainingRun { get; set; } = null!;
    public int DailyQuestionId { get; set; }
    public DailyQuestion DailyQuestion { get; set; } = null!;
}

/// <summary>Indexen, conversies en relaties van de academie.</summary>
internal static class TrainingModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<TrainingRun>().HasIndex(r => new { r.UserId, r.DailyChallengeId });
        builder.Entity<TrainingAttempt>().HasIndex(a => new { a.TrainingRunId, a.DailyQuestionId }).IsUnique();
        builder.Entity<TrainingAttempt>().Property(a => a.Guesses).IsConcurrencyToken();
    }
}
