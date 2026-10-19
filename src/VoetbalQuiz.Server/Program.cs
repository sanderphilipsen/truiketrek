using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Endpoints;
using VoetbalQuiz.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<QuizDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Quiz")));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>(options =>
    {
        // Vriendengroep: eenvoudige wachtwoordregels.
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<QuizDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "voetbalquiz";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<ResultsService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<OnlineService>();
builder.Services.AddScoped<MonthRankingService>();
builder.Services.AddScoped<GoldenBallService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddSingleton<IMatchNotifier, MatchNotifier>();
builder.Services.AddHostedService<MatchEngine>();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddScoped<PlayerSeeder>();
builder.Services.AddScoped<QuestionSeeder>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PlayerSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<QuestionSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapQuizEndpoints();
app.MapFeedbackEndpoints();
app.MapMatchEndpoints();
app.MapAdminEndpoints();
app.MapProfileEndpoints();
app.MapHub<MatchHub>("/hubs/match");
// index.html altijd opnieuw laten valideren: zonder Cache-Control houdt een browser na een deploy
// een oude versie vast die naar verdwenen framework-bestanden verwijst, en start de app niet meer.
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();
