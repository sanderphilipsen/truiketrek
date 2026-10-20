using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        // Gewone spelers zien hun eigen meldingen, admins zien alles.
        group.MapGet("/", (ClaimsPrincipal user, FeedbackService feedback, IConfiguration config) =>
            feedback.ListAsync(user.UserId(), CurrentUser.IsAdmin(user, config)));

        group.MapPost("/", async (FeedbackRequest req, ClaimsPrincipal user, FeedbackService feedback) =>
        {
            await feedback.AddAsync(user.UserId(), req);
            return Results.Ok();
        });

        group.MapPut("/{id:int}", async (int id, FeedbackUpdate req, FeedbackService feedback) =>
                await feedback.UpdateAsync(id, req) ? Results.Ok() : Results.NotFound())
            .RequireAuthorization(CurrentUser.AdminPolicy);
    }
}
