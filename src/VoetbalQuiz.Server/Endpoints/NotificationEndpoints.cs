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

        // Pushmeldingen: de publieke sleutel en je voorkeur, een toestel aan- of afmelden, de herinnering aan/uit.
        var push = app.MapGroup("/api/push").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        push.MapGet("/", (ClaimsPrincipal user, PushService pushes) => pushes.GetConfigAsync(user.UserId()));

        push.MapPost("/subscribe", async (PushSubscribeRequest req, ClaimsPrincipal user, PushService pushes) =>
        {
            await pushes.SubscribeAsync(user.UserId(), req);
            return Results.NoContent();
        });

        push.MapPost("/unsubscribe", async (PushUnsubscribeRequest req, ClaimsPrincipal user, PushService pushes) =>
        {
            await pushes.UnsubscribeAsync(user.UserId(), req.Endpoint);
            return Results.NoContent();
        });

        push.MapPut("/daily-reminder", async (DailyReminderRequest req, ClaimsPrincipal user, PushService pushes) =>
        {
            await pushes.SetDailyReminderAsync(user.UserId(), req.On);
            return Results.NoContent();
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
