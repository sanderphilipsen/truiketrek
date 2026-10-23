using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        group.MapGet("/", (ClaimsPrincipal user, NotificationService notifications) => notifications.ListAsync(user.UserId()));

        group.MapGet("/count", (ClaimsPrincipal user, NotificationService notifications) => notifications.CountUnreadAsync(user.UserId()));

        group.MapPost("/read", async (ClaimsPrincipal user, NotificationService notifications) =>
        {
            await notifications.MarkAllReadAsync(user.UserId());
            return Results.Ok();
        });

        // Releasenota's ("Wat is er nieuw") schrijven en wissen: enkel admins.
        var admin = app.MapGroup("/api/admin/announcements")
            .RequireAuthorization(CurrentUser.AdminPolicy)
            .AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        admin.MapGet("/", (NotificationService notifications) => notifications.ListAnnouncementsAsync());

        admin.MapPost("/", async (AnnouncementRequest req, NotificationService notifications) =>
        {
            await notifications.AddAnnouncementAsync(req);
            return Results.Ok();
        });

        admin.MapDelete("/{id:int}", async (int id, NotificationService notifications) =>
            await notifications.DeleteAnnouncementAsync(id) ? Results.Ok() : Results.NotFound());
    }
}
