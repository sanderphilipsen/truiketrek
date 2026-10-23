using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Meldingen van spelers (fouten, ideeën) en het antwoord van een admin.</summary>
public class FeedbackService(QuizDbContext db, TimeProvider clock, NotificationService notifications)
{
    /// <summary>Gewone spelers zien hun eigen meldingen, admins zien alles. Nieuwste eerst.</summary>
    public async Task<FeedbackOverview> ListAsync(string userId, bool admin)
    {
        var items = await db.Feedback
            .Where(f => admin || f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FeedbackItem(f.Id, f.Type, f.Title, f.Description, f.User.UserName!,
                f.CreatedAt, f.Status, f.Reply, f.UserId == userId))
            .ToListAsync();
        return new FeedbackOverview(admin, items);
    }

    public async Task AddAsync(string userId, FeedbackRequest req)
    {
        var title = req.Title?.Trim() ?? "";
        var description = req.Description?.Trim() ?? "";
        if (title.Length is < 3 or > 120)
            throw new QuizException("Geef een titel van 3 tot 120 tekens.");
        if (description.Length is < 5 or > 4000)
            throw new QuizException("Beschrijf het in 5 tot 4000 tekens.");

        db.Feedback.Add(new Feedback
        {
            UserId = userId,
            Type = req.Type,
            Title = title,
            Description = description,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        });
        var userName = await db.Users.Where(u => u.Id == userId).Select(u => u.UserName).SingleAsync();
        await notifications.AddForAdminsAsync(userId, NotificationKind.NieuweFeedback,
            req.Type == FeedbackType.Bug ? "Nieuwe bug gemeld" : "Nieuw idee", $"{userName}: {title}", "admin/feedback");
        await db.SaveChangesAsync();
    }

    /// <summary>Status en antwoord van een admin. False als de melding niet bestaat.</summary>
    public async Task<bool> UpdateAsync(int id, FeedbackUpdate req)
    {
        var item = await db.Feedback.FindAsync(id);
        if (item is null) return false;

        var reply = string.IsNullOrWhiteSpace(req.Reply) ? null : req.Reply.Trim();
        // De melder hoort het bij een nieuw antwoord of een andere status; enkel opslaan zonder wijziging stuurt niets.
        var newReply = reply is not null && reply != item.Reply;
        if (newReply || req.Status != item.Status)
            notifications.Add(item.UserId, NotificationKind.FeedbackAntwoord,
                newReply ? "Antwoord op je melding" : $"Je melding staat op {req.Status.ToString().ToLowerInvariant()}",
                newReply ? $"{item.Title}: {reply}" : item.Title, "feedback");

        item.Status = req.Status;
        item.Reply = reply;
        await db.SaveChangesAsync();
        return true;
    }
}
