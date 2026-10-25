using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Endpoints;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Inhoud van release-note.json, die deploy-azure.ps1 met de app meepubliceert.</summary>
public record ReleaseNote(string Version, string Title, string Text, string? Link);

/// <summary>
/// Het meldingencentrum: persoonlijke meldingen (<see cref="Notification"/>) en releasenota's voor iedereen
/// (<see cref="Announcement"/>). Wie de lijst opent, heeft alles gezien.
/// </summary>
public partial class NotificationService(QuizDbContext db, TimeProvider clock, IConfiguration config)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<NotificationCount> CountUnreadAsync(string userId)
    {
        var personal = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);
        var since = await AnnouncementsSinceAsync(userId);
        var news = await db.Announcements.CountAsync(a => a.CreatedAt > since);
        return new NotificationCount(personal + Math.Min(news, NotificationRules.MaxAnnouncements));
    }

    /// <summary>Nieuwste eerst. Ruimt meteen gelezen meldingen op die ouder zijn dan <see cref="NotificationRules.KeepReadDays"/>.</summary>
    public async Task<List<NotificationItem>> ListAsync(string userId)
    {
        var cutoff = Now.AddDays(-NotificationRules.KeepReadDays);
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt < cutoff).ExecuteDeleteAsync();

        var personal = await db.Notifications
            .Where(n => n.UserId == userId)
            .Select(n => new NotificationItem(n.Kind, n.Title, n.Text, n.Link, n.CreatedAt, n.ReadAt == null))
            .ToListAsync();

        // Nieuwe spelers zien enkel releasenota's van na hun registratie.
        var user = await db.Users.Where(u => u.Id == userId).Select(u => new { u.CreatedAt, u.AnnouncementsReadAt }).SingleAsync();
        var readAt = user.AnnouncementsReadAt ?? DateTime.MinValue;
        var news = await db.Announcements
            .Where(a => a.CreatedAt > user.CreatedAt)
            .OrderByDescending(a => a.CreatedAt)
            .Take(NotificationRules.MaxAnnouncements)
            .Select(a => new NotificationItem(NotificationKind.Nieuws, a.Title, a.Text, a.Link, a.CreatedAt, a.CreatedAt > readAt))
            .ToListAsync();

        return [.. personal.Concat(news).OrderByDescending(n => n.CreatedAt)];
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var now = Now;
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now));
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AnnouncementsReadAt, now));
    }

    /// <summary>Zet een melding klaar; ze wordt bewaard bij de volgende SaveChanges van de aanroeper.</summary>
    public void Add(string userId, NotificationKind kind, string title, string? text, string? link) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Kind = kind,
            Title = Shorten(title, 120),
            Text = text is null ? null : Shorten(text, 300),
            Link = link,
            CreatedAt = Now,
        });

    /// <summary>Een melding voor elke admin behalve <paramref name="exceptUserId"/> (wie het zelf deed, hoeft het niet te horen).</summary>
    public async Task AddForAdminsAsync(string exceptUserId, NotificationKind kind, string title, string? text, string? link)
    {
        var names = CurrentUser.AdminNames(config).Select(n => n.ToLowerInvariant()).ToList();
        var admins = await db.Users
            .Where(u => u.Id != exceptUserId && names.Contains(u.UserName!.ToLower()))
            .Select(u => u.Id)
            .ToListAsync();
        foreach (var admin in admins) Add(admin, kind, title, text, link);
    }

    // ---------- Releasenota's (admin) ----------

    public Task<List<AnnouncementItem>> ListAnnouncementsAsync() =>
        db.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AnnouncementItem(a.Id, a.Title, a.Text, a.Link, a.CreatedAt))
            .ToListAsync();

    /// <summary>
    /// De releasenota die deploy-azure.ps1 meegaf (release-note.json): één keer per versie, ook als de app herstart.
    /// False als die versie al verstuurd was.
    /// </summary>
    public async Task<bool> AddReleaseNoteAsync(AnnouncementRequest req, string version)
    {
        if (await db.Announcements.AnyAsync(a => a.Version == version)) return false;
        await AddAnnouncementAsync(req, version);
        return true;
    }

    public async Task AddAnnouncementAsync(AnnouncementRequest req, string? version = null)
    {
        var title = req.Title?.Trim() ?? "";
        var text = req.Text?.Trim() ?? "";
        var link = string.IsNullOrWhiteSpace(req.Link) ? null : req.Link.Trim().TrimStart('/');
        if (title.Length is < 3 or > 80)
            throw new QuizException("Geef een titel van 3 tot 80 tekens.");
        if (text.Length is < 5 or > 1000)
            throw new QuizException("Schrijf een tekst van 5 tot 1000 tekens.");
        // Enkel een pagina in de app: geen externe adressen of javascript:-links.
        if (link is not null && !AppLink().IsMatch(link))
            throw new QuizException("De link moet een pagina in de app zijn, bv. academie of online.");

        db.Announcements.Add(new Announcement { Title = title, Text = text, Link = link, CreatedAt = Now, Version = version });
        await db.SaveChangesAsync();
    }

    /// <summary>False als de releasenota niet bestaat.</summary>
    public async Task<bool> DeleteAnnouncementAsync(int id) =>
        await db.Announcements.Where(a => a.Id == id).ExecuteDeleteAsync() > 0;

    private async Task<DateTime> AnnouncementsSinceAsync(string userId)
    {
        var user = await db.Users.Where(u => u.Id == userId).Select(u => new { u.CreatedAt, u.AnnouncementsReadAt }).SingleAsync();
        return user.AnnouncementsReadAt is { } read && read > user.CreatedAt ? read : user.CreatedAt;
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    [GeneratedRegex(@"^[a-z0-9][a-z0-9/_\-?=&]{0,199}$", RegexOptions.IgnoreCase)]
    private static partial Regex AppLink();
}
