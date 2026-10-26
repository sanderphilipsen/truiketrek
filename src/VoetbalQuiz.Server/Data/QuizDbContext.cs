using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace VoetbalQuiz.Server.Data;

public class QuizDbContext(DbContextOptions<QuizDbContext> options) : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();
    public DbSet<DailyQuestion> DailyQuestions => Set<DailyQuestion>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<TrainingRun> TrainingRuns => Set<TrainingRun>();
    public DbSet<TrainingAttempt> TrainingAttempts => Set<TrainingAttempt>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
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

    // Sleutels van de aanmeldcookies: in de database, zodat spelers na een deploy aangemeld blijven.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Per context, naast de entiteiten in Data/Model.
        InhoudModel.Configure(builder);
        DailyModel.Configure(builder);
        TrainingModel.Configure(builder);
        OnlineModel.Configure(builder);
        KlassementenModel.Configure(builder);
        CommunicatieModel.Configure(builder);
    }
}
