using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Gedeelde testopzet: een lege SQLite-database in het geheugen, een klok die je zelf verzet en de services zoals de
/// server ze aan elkaar hangt. Elke test krijgt zijn eigen <see cref="TestDb"/>; de services worden pas gemaakt als je ze
/// gebruikt en delen binnen één test dezelfde context en cache.
/// </summary>
public sealed class TestDb : IDisposable
{
    /// <summary>Zaterdag 17 oktober 2026, 12:00 in België.</summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<QuizDbContext> _options;

    public TestDb(DateTimeOffset? now = null)
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options;
        Db = new QuizDbContext(_options);
        Db.Database.EnsureCreated();
        Clock = new FakeTimeProvider(now ?? DefaultNow);
    }

    public QuizDbContext Db { get; }
    public FakeTimeProvider Clock { get; }

    /// <summary>
    /// Een tweede context op dezelfde database, zoals een verzoek dat tegelijk binnenkomt. Met
    /// <paramref name="interceptors"/> kan een test iets laten gebeuren net voor die context bewaart.
    /// </summary>
    public QuizDbContext NewContext(params IInterceptor[] interceptors) =>
        interceptors.Length == 0
            ? new(_options)
            : new(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options);

    public string AddUser(string name)
    {
        var user = new AppUser { UserName = name };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user.Id;
    }

    // ---------- Services op de gedeelde context ----------

    private ResultsService? _results;
    private QuestionPicker? _picker;
    private QuizService? _quiz;
    private AdminService? _admin;
    private MonthRankingService? _ranking;
    private OnlineService? _online;
    private GoldenBallService? _goldenBall;
    private ProgressService? _progress;
    private TrainingService? _training;
    private FeedbackService? _feedback;

    public ResultsService Results => _results ??= NewResults(Db);
    public QuestionPicker Picker => _picker ??= new QuestionPicker(Db, Clock);
    public QuizService Quiz => _quiz ??= new QuizService(Db, Clock, Results, Picker);
    public AdminService Admin => _admin ??= new AdminService(Db, Picker, Clock);
    public MonthRankingService Ranking => _ranking ??= new MonthRankingService(Db, Results);
    public OnlineService Online => _online ??= new OnlineService(Db, Results);
    public GoldenBallService GoldenBall => _goldenBall ??= new GoldenBallService(Db, Clock, Ranking, Results);
    public ProgressService Progress => _progress ??= new ProgressService(Db, Clock, Results, Online, Ranking, GoldenBall);
    public TrainingService Training => _training ??= new TrainingService(Db, Clock, Quiz, Results);
    public FeedbackService Feedback => _feedback ??= new FeedbackService(Db, Clock);

    /// <summary>Online wedstrijden zonder SignalR; <paramref name="random"/> legt de wapens van een derby vast.</summary>
    public MatchService Matches(Random? random = null) => new(Db, Clock, Results, Picker, new DerbyService(Db, Clock, Picker), new NoNotifier(), random);

    // ---------- Services op een andere context (bv. een verzoek dat tegelijk binnenkomt) ----------

    /// <summary>Met een eigen, lege cache.</summary>
    public ResultsService NewResults(QuizDbContext db) => new(db, Clock, new MemoryCache(new MemoryCacheOptions()));

    public QuizService NewQuiz(QuizDbContext db) => new(db, Clock, NewResults(db), new QuestionPicker(db, Clock));

    public MatchService NewMatches(QuizDbContext db)
    {
        var picker = new QuestionPicker(db, Clock);
        return new MatchService(db, Clock, NewResults(db), picker, new DerbyService(db, Clock, picker), new NoNotifier());
    }

    private sealed class NoNotifier : IMatchNotifier
    {
        public Task LobbyChangedAsync() => Task.CompletedTask;
        public Task MatchChangedAsync(int matchId) => Task.CompletedTask;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
