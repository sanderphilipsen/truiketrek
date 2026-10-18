using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/api/matches").RequireAuthorization().AddEndpointFilter(QuizEndpoints.HandleQuizErrors);

        matches.MapGet("/", (ClaimsPrincipal user, MatchService svc) => svc.GetLobbyAsync(UserId(user)));
        // Enkel de types die aan staan kan je kiezen voor een tafel.
        matches.MapGet("/types", (QuizService quiz) => quiz.ActiveTypesAsync());
        matches.MapPost("/", async (CreateMatchRequest req, ClaimsPrincipal user, MatchService svc) =>
            new { Id = await svc.CreateAsync(UserId(user), user.Identity!.Name!, req) });
        matches.MapGet("/{id:int}", (int id, ClaimsPrincipal user, MatchService svc) => svc.GetStateAsync(UserId(user), id));
        matches.MapPost("/{id:int}/join", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.JoinAsync(UserId(user), id);
            return await svc.GetStateAsync(UserId(user), id);
        });
        matches.MapPost("/{id:int}/leave", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.LeaveAsync(UserId(user), id);
            return Results.Ok();
        });
        matches.MapPost("/{id:int}/start", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.StartAsync(UserId(user), id);
            return await svc.GetStateAsync(UserId(user), id);
        });
        matches.MapPost("/{id:int}/guess", (int id, AnswerRequest req, ClaimsPrincipal user, MatchService svc) =>
            svc.GuessAsync(UserId(user), id, req.Guess));
        matches.MapPost("/{id:int}/giveup", (int id, ClaimsPrincipal user, MatchService svc) =>
            svc.GiveUpAsync(UserId(user), id));

        var online = app.MapGroup("/api/online").RequireAuthorization();
        online.MapGet("/stats", (ClaimsPrincipal user, OnlineService svc) => svc.GetStatsAsync(UserId(user)));
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
