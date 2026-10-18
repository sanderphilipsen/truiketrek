using System.Security.Claims;
using VoetbalQuiz.Server.Services;

namespace VoetbalQuiz.Server.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        // Profiel van een speler (ook van jezelf): badges, bekers en head-to-head.
        app.MapGet("/api/players/{userName}", (string userName, ClaimsPrincipal user, ProgressService progress) =>
                progress.GetProfileAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, userName))
            .RequireAuthorization()
            .AddEndpointFilter(QuizEndpoints.HandleQuizErrors);

        // Na een daily, special of wedstrijd: welke XP en badges kwamen erbij sinds de vorige keer?
        app.MapPost("/api/progress/reward", (ClaimsPrincipal user, ProgressService progress) =>
                progress.ClaimRewardAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!))
            .RequireAuthorization()
            .AddEndpointFilter(QuizEndpoints.HandleQuizErrors);
    }
}
