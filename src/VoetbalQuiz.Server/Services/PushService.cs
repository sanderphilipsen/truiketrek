using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Pushmeldingen: toestellen aan- en afmelden, de herinnering aan/uit, en om <see cref="PushRules.DailyReminderHour"/>u
/// de herinnering voor wie de daily nog niet speelde (zie <see cref="DailyReminderService"/>).
/// </summary>
public class PushService(QuizDbContext db, TimeProvider clock, IPushSender sender, ILogger<PushService> logger)
{
    /// <summary>
    /// Enkel de pushdiensten van de grote browsers. De server stuurt naar het adres dat de browser opgaf; zonder deze lijst
    /// kan iemand de server laten posten naar om het even welk adres.
    /// </summary>
    private static readonly string[] PushHosts =
        ["fcm.googleapis.com", "push.services.mozilla.com", "push.apple.com", "notify.windows.com"];

    public async Task<PushConfig> GetConfigAsync(string userId) =>
        new(sender.PublicKey, await db.Users.Where(u => u.Id == userId).Select(u => u.DailyReminder).SingleAsync());

    public async Task SetDailyReminderAsync(string userId, bool on) =>
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(set => set.SetProperty(u => u.DailyReminder, on));

    /// <summary>
    /// Dit toestel wil meldingen voor deze speler. Was het van een andere speler (afgemeld en een ander aangemeld op
    /// dezelfde browser), dan gaat het over naar wie nu aangemeld is.
    /// </summary>
    public async Task SubscribeAsync(string userId, PushSubscribeRequest req)
    {
        if (!IsPushEndpoint(req.Endpoint))
            throw new QuizException("Dit toestel kan geen meldingen krijgen.");
        if (req.P256dh is not { Length: > 0 and <= PushSubscriptionLimits.Key } || req.Auth is not { Length: > 0 and <= PushSubscriptionLimits.Key })
            throw new QuizException("Dit toestel kan geen meldingen krijgen.");

        var device = await db.PushSubscriptions.SingleOrDefaultAsync(p => p.Endpoint == req.Endpoint);
        if (device is null)
        {
            device = new PushSubscription { UserId = userId, Endpoint = req.Endpoint, P256dh = req.P256dh, Auth = req.Auth };
            db.PushSubscriptions.Add(device);
        }
        else if (device.UserId == userId && device.P256dh == req.P256dh && device.Auth == req.Auth)
            return; // niets veranderd: Home meldt een toestel bij elk bezoek opnieuw aan
        device.UserId = userId;
        device.P256dh = req.P256dh;
        device.Auth = req.Auth;
        device.CreatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        // Hooguit MaxDevices toestellen: het oudste valt weg.
        var surplus = await db.PushSubscriptions.Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip(PushRules.MaxDevices).Select(p => p.Id).ToListAsync();
        if (surplus.Count > 0)
            await db.PushSubscriptions.Where(p => surplus.Contains(p.Id)).ExecuteDeleteAsync();
    }

    /// <summary>Bij het afmelden of het uitzetten in de browser: dit toestel krijgt niets meer voor deze speler.</summary>
    public async Task UnsubscribeAsync(string userId, string endpoint) =>
        await db.PushSubscriptions.Where(p => p.UserId == userId && p.Endpoint == endpoint).ExecuteDeleteAsync();

    internal static bool IsPushEndpoint(string? endpoint) =>
        endpoint is { Length: > 0 and <= PushSubscriptionLimits.Endpoint }
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && PushHosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.Ordinal));

    /// <summary>
    /// De herinnering van vandaag: voor elke speler met de herinnering aan, minstens één toestel, die vandaag de daily nog
    /// niet speelde en vandaag nog geen herinnering kreeg. Buiten het uur (<see cref="PushRules.DailyReminderDue"/>) gebeurt
    /// er niets. Geeft het aantal spelers dat een herinnering kreeg.
    /// </summary>
    public async Task<int> SendDailyRemindersAsync(CancellationToken cancellationToken = default)
    {
        if (sender.PublicKey is null) return 0;
        var now = clock.GetUtcNow();
        if (!PushRules.DailyReminderDue(QuizDay.ToBrussels(now.UtcDateTime))) return 0;
        var today = QuizDay.DateOf(now);

        var userIds = await db.Users
            .Where(u => u.DailyReminder && u.DeletedAt == null && u.DailyReminderSent != today)
            .Where(u => db.PushSubscriptions.Any(p => p.UserId == u.Id))
            .Where(u => !db.Attempts.Any(a => a.UserId == u.Id && a.AnsweredAt != null
                && a.DailyQuestion.DailyChallenge.Date == today && a.DailyQuestion.DailyChallenge.SpecialEventId == null))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var userId in userIds)
        {
            // Eerst onthouden, dan versturen: liever eens een herinnering te weinig dan twee keer dezelfde.
            var claimed = await db.Users.Where(u => u.Id == userId && u.DailyReminderSent != today)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.DailyReminderSent, today), cancellationToken);
            if (claimed == 0) continue;

            var dates = await db.Attempts
                .Where(a => a.UserId == userId && a.AnsweredAt != null && a.DailyQuestion.DailyChallenge.SpecialEventId == null)
                .Select(a => a.DailyQuestion.DailyChallenge.Date)
                .Distinct()
                .ToListAsync(cancellationToken);
            var streak = ResultsService.Streaks(dates, today).Current;
            var payload = new PushPayload(PushRules.DailyReminderTitle, PushRules.DailyReminderText(streak), "daily", "daily-reminder");

            var devices = await db.PushSubscriptions.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
            foreach (var device in devices)
            {
                if (await sender.SendAsync(device, payload, cancellationToken) == PushResult.Gone)
                    await db.PushSubscriptions.Where(p => p.Id == device.Id).ExecuteDeleteAsync(cancellationToken);
            }
            sent++;
        }

        if (sent > 0) logger.LogInformation("Daily-herinnering verstuurd naar {Count} spelers", sent);
        return sent;
    }
}

/// <summary>Kijkt om de paar minuten of het tijd is voor de daily-herinnering (zie <see cref="PushService"/>).</summary>
public class DailyReminderService(IServiceScopeFactory scopes, IPushSender sender, ILogger<DailyReminderService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (sender.PublicKey is null)
        {
            logger.LogInformation("Pushmeldingen staan niet ingesteld ({Key}): geen daily-herinnering", WebPushSender.PublicKeyKey);
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PushService>().SendDailyRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fout bij het versturen van de daily-herinnering");
            }
        }
    }
}
