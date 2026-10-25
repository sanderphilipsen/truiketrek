using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Endpoints;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<QuizDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Quiz")));

// De sleutels die de aanmeldcookie versleutelen staan in de database. Anders kan een deploy of herstart
// op Azure een nieuwe sleutelring opleveren en moet iedereen opnieuw aanmelden.
builder.Services.AddDataProtection()
    .SetApplicationName("VoetbalQuiz")
    .PersistKeysToDbContext<QuizDbContext>();

// Admins staan in de configuratie onder "Admins" (gebruikersnamen); zie CurrentUser.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(CurrentUser.AdminPolicy, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(ctx => CurrentUser.IsAdminName(ctx.User.Identity?.Name, builder.Configuration)));
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

// Aanmelden met Google kan pas wanneer Authentication:Google:ClientId en ClientSecret ingesteld zijn
// (lokaal in user-secrets, op Azure als app-instelling). Zonder die sleutels verschijnt de knop niet.
if (GoogleLoginService.IsConfigured(builder.Configuration))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = builder.Configuration[GoogleLoginService.ClientIdKey]!;
        options.ClientSecret = builder.Configuration[GoogleLoginService.ClientSecretKey]!;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClaimActions.MapJsonKey(GoogleLoginService.EmailVerifiedClaim, "email_verified");
        // Geannuleerd of mislukt bij Google: terug naar het aanmeldscherm in plaats van een foutpagina.
        options.Events.OnRemoteFailure = ctx =>
        {
            ctx.Response.Redirect("/login?fout=google");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "voetbalquiz";
    // Lax en niet Strict: na de terugsprong van Google moet de cookie meteen meegaan.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton(TimeProvider.System);
// Op Azure staat /home/data buiten de app-map en overleeft het een deploy; lokaal is het een bestand naast het project.
builder.Services.AddSingleton(sp => new MaintenanceNotice(
    builder.Configuration["Maintenance:File"] is { Length: > 0 } file ? Path.Combine(builder.Environment.ContentRootPath, file) : null,
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<QuestionPicker>();
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<ResultsService>();
builder.Services.AddScoped<DerbyService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<OnlineService>();
builder.Services.AddScoped<MonthRankingService>();
builder.Services.AddScoped<GoldenBallService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<StockAlertService>();
builder.Services.AddScoped<GoogleLoginService>();
builder.Services.AddSingleton<LastSeenTracker>();
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

    // Releasenota die het deployscript meegaf: als "Wat is er nieuw" naar alle spelers. Een fout hierin mag de app niet tegenhouden.
    var releaseNote = Path.Combine(app.Environment.ContentRootPath, "release-note.json");
    if (File.Exists(releaseNote))
    {
        try
        {
            var note = JsonSerializer.Deserialize<ReleaseNote>(await File.ReadAllTextAsync(releaseNote), JsonSerializerOptions.Web);
            if (note is not { Version.Length: > 0 }) throw new JsonException("Geen versie in de releasenota.");
            if (await scope.ServiceProvider.GetRequiredService<NotificationService>().AddReleaseNoteAsync(new AnnouncementRequest(note.Title, note.Text, note.Link), note.Version))
                app.Logger.LogInformation("Releasenota {Version} verstuurd: {Title}", note.Version, note.Title);
        }
        catch (Exception ex) when (ex is JsonException or QuizException)
        {
            app.Logger.LogWarning(ex, "Releasenota in {File} niet verstuurd", releaseNote);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Azure zet HTTPS af vóór de app: zonder de doorgestuurde headers zou de terugkeer-URL voor Google http:// worden.
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

// "Laatst online" voor het admin-paneel: elk API-verzoek van een aangemelde speler telt (hooguit om de 5 minuten een schrijfactie).
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true && ctx.Request.Path.StartsWithSegments("/api"))
        await ctx.RequestServices.GetRequiredService<LastSeenTracker>().TouchAsync(ctx.User.UserId(), ctx.RequestServices.GetRequiredService<QuizDbContext>());
    await next(ctx);
});

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapQuizEndpoints();
app.MapTrainingEndpoints();
app.MapFeedbackEndpoints();
app.MapNotificationEndpoints();
app.MapMatchEndpoints();
app.MapAdminEndpoints();
app.MapProfileEndpoints();
app.MapHub<MatchHub>("/hubs/match");
// Zonder aanmelding: ook het aanmeldscherm toont dat er een update komt.
app.MapGet("/api/status", (MaintenanceNotice maintenance) => maintenance.Status());
// index.html altijd opnieuw laten valideren: zonder Cache-Control houdt een browser na een deploy
// een oude versie vast die naar verdwenen framework-bestanden verwijst, en start de app niet meer.
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();
