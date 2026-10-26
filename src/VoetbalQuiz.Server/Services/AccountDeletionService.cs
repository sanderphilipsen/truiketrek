using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Een speler vraagt de verwijdering van zijn account aan; een admin voert ze uit. Verwijderen = anonimiseren: de rij
/// blijft, zodat dailies, maandklassementen, Gouden Ballen en online wedstrijden van anderen niet verschuiven, maar alles
/// wat naar de persoon leidt verdwijnt (naam, e-mail, wachtwoord, Google-koppeling, meldingen, feedback, trainingen).
/// </summary>
public class AccountDeletionService(QuizDbContext db, TimeProvider clock, NotificationService notifications, ResultsService results)
{
    /// <summary>Voorvoegsel van de naam die een verwijderd account in de klassementen krijgt.</summary>
    public const string AnonymousPrefix = "Oud-speler ";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task RequestAsync(string userId)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        if (user.DeletionRequestedAt is not null) return;

        user.DeletionRequestedAt = Now;
        await notifications.AddForAdminsAsync(userId, NotificationKind.Verwijderaanvraag,
            "Verwijderaanvraag", $"{user.UserName} wil zijn account laten verwijderen.", "admin/leden");
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(string userId) =>
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionRequestedAt, (DateTime?)null));

    /// <summary>
    /// Verwijdert het account van een speler, standaard enkel met een lopende aanvraag; een admin kan met
    /// <paramref name="onlyRequested"/> = false ook zonder aanvraag verwijderen. Null als er geen zo een speler is.
    /// </summary>
    public async Task<string?> DeleteAsync(string? userName, bool onlyRequested = true)
    {
        var normalized = userName?.Trim().ToUpperInvariant() ?? "";
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalized
            && (!onlyRequested || u.DeletionRequestedAt != null) && u.DeletedAt == null);
        if (user is null) return null;

        var oldName = user.UserName!;
        var anonymous = AnonymousPrefix + user.Id[..8];
        user.UserName = anonymous;
        user.NormalizedUserName = anonymous.ToUpperInvariant();
        user.Email = null;
        user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PasswordHash = null;
        user.PhoneNumber = null;
        user.AvatarId = null;
        user.LastSeenAt = null;
        user.DeletedAt = Now;
        // Een nieuwe stempel maakt de aanmeldcookie ongeldig; het slot houdt het account dicht.
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await db.UserLogins.Where(l => l.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserClaims.Where(c => c.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserRoles.Where(r => r.UserId == user.Id).ExecuteDeleteAsync();
        await db.Feedback.Where(f => f.UserId == user.Id).ExecuteDeleteAsync();
        await db.Notifications.Where(n => n.UserId == user.Id).ExecuteDeleteAsync();
        await db.TrainingRuns.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(); // de pogingen volgen via cascade
        await db.SaveChangesAsync();

        results.ForgetUserCards();
        return oldName;
    }
}
