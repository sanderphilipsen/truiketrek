using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace VoetbalQuiz.Server.Data;

public class QuizDbContext(DbContextOptions<QuizDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();
    public DbSet<DailyQuestion> DailyQuestions => Set<DailyQuestion>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<SpecialEvent> SpecialEvents => Set<SpecialEvent>();
    public DbSet<QuizItem> QuizItems => Set<QuizItem>();
    public DbSet<DailyScheduleDay> DailySchedule => Set<DailyScheduleDay>();
    public DbSet<DisabledQuestionType> DisabledQuestionTypes => Set<DisabledQuestionType>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchQuestion> MatchQuestions => Set<MatchQuestion>();
    public DbSet<MatchAnswer> MatchAnswers => Set<MatchAnswer>();
    public DbSet<GoldenBall> GoldenBalls => Set<GoldenBall>();
    public DbSet<GoldenBallMonth> GoldenBallMonths => Set<GoldenBallMonth>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Player>().HasIndex(p => p.Name).IsUnique();
        builder.Entity<Player>().Property(p => p.Status).HasConversion<string>();
        builder.Entity<QuizItem>().Property(q => q.Status).HasConversion<string>();
        builder.Entity<QuizItem>().Property(q => q.Type).HasConversion<string>();
        builder.Entity<QuizItem>().HasIndex(q => new { q.Type, q.Key }).IsUnique();
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
        builder.Entity<Feedback>().Property(f => f.Type).HasConversion<string>();
        builder.Entity<Feedback>().Property(f => f.Status).HasConversion<string>();
        builder.Entity<Match>().Property(m => m.Status).HasConversion<string>();
        builder.Entity<Match>().HasIndex(m => m.Status);
        builder.Entity<Match>().HasOne(m => m.Host).WithMany().HasForeignKey(m => m.HostUserId);
        builder.Entity<MatchPlayer>().HasIndex(p => new { p.MatchId, p.UserId }).IsUnique();
        builder.Entity<MatchPlayer>().Property(p => p.Weapon).HasConversion<string>();
        builder.Entity<MatchQuestion>().HasIndex(q => new { q.MatchId, q.Number }).IsUnique();
        builder.Entity<MatchQuestion>().Property(q => q.Type).HasConversion<string>();
        builder.Entity<MatchAnswer>().HasIndex(a => new { a.MatchQuestionId, a.UserId }).IsUnique();
        builder.Entity<Attempt>().HasIndex(a => new { a.UserId, a.DailyQuestionId }).IsUnique();
        builder.Entity<GoldenBall>().HasIndex(g => new { g.Year, g.Month, g.UserId }).IsUnique();
        builder.Entity<GoldenBallMonth>().HasIndex(m => new { m.Year, m.Month }).IsUnique();
    }
}
