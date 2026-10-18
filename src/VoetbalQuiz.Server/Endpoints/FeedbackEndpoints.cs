using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").RequireAuthorization();

        // Gewone spelers zien hun eigen meldingen, admins zien alles.
        group.MapGet("/", async (ClaimsPrincipal user, QuizDbContext db, IConfiguration config) =>
        {
            var userId = UserId(user);
            var admin = IsAdmin(user, config);
            var items = await db.Feedback
                .Where(f => admin || f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FeedbackItem(f.Id, f.Type, f.Title, f.Description, f.User.UserName!,
                    f.CreatedAt, f.Status, f.Reply, f.UserId == userId))
                .ToListAsync();
            return new FeedbackOverview(admin, items);
        });

        group.MapPost("/", async (FeedbackRequest req, ClaimsPrincipal user, QuizDbContext db) =>
        {
            var title = req.Title?.Trim() ?? "";
            var description = req.Description?.Trim() ?? "";
            if (title.Length is < 3 or > 120)
                return Results.BadRequest(new ErrorResponse("Geef een titel van 3 tot 120 tekens."));
            if (description.Length is < 5 or > 4000)
                return Results.BadRequest(new ErrorResponse("Beschrijf het in 5 tot 4000 tekens."));

            db.Feedback.Add(new Feedback
            {
                UserId = UserId(user),
                Type = req.Type,
                Title = title,
                Description = description,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapPut("/{id:int}", async (int id, FeedbackUpdate req, ClaimsPrincipal user, QuizDbContext db, IConfiguration config) =>
        {
            if (!IsAdmin(user, config)) return Results.Forbid();
            var item = await db.Feedback.FindAsync(id);
            if (item is null) return Results.NotFound();

            item.Status = req.Status;
            item.Reply = string.IsNullOrWhiteSpace(req.Reply) ? null : req.Reply.Trim();
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Admins staan in de configuratie onder "Admins" (gebruikersnamen).</summary>
    public static bool IsAdmin(ClaimsPrincipal user, IConfiguration config) => IsAdminName(user.Identity?.Name, config);

    public static bool IsAdminName(string? userName, IConfiguration config) =>
        config.GetSection("Admins").Get<string[]>()?.Contains(userName, StringComparer.OrdinalIgnoreCase) == true;
}
