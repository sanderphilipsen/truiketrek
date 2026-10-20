using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/api/matches").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        matches.MapGet("/", (ClaimsPrincipal user, MatchService svc) => svc.GetLobbyAsync(user.UserId()));
        // Enkel de types die aan staan kan je kiezen voor een partijtje.
        matches.MapGet("/types", (QuizService quiz) => quiz.ActiveTypesAsync());
        matches.MapPost("/", async (CreateMatchRequest req, ClaimsPrincipal user, MatchService svc) =>
            new { Id = await svc.CreateAsync(user.UserId(), user.Identity!.Name!, req) });
        matches.MapGet("/{id:int}", (int id, ClaimsPrincipal user, MatchService svc) => svc.GetStateAsync(user.UserId(), id));
        matches.MapPost("/{id:int}/join", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.JoinAsync(user.UserId(), id);
            return await svc.GetStateAsync(user.UserId(), id);
        });
        matches.MapPost("/{id:int}/leave", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.LeaveAsync(user.UserId(), id);
            return Results.Ok();
        });
        matches.MapPost("/{id:int}/start", async (int id, ClaimsPrincipal user, MatchService svc) =>
        {
            await svc.StartAsync(user.UserId(), id);
            return await svc.GetStateAsync(user.UserId(), id);
        });
        matches.MapPost("/{id:int}/guess", (int id, AnswerRequest req, ClaimsPrincipal user, MatchService svc) =>
            svc.GuessAsync(user.UserId(), id, req.Guess));
        matches.MapPost("/{id:int}/giveup", (int id, ClaimsPrincipal user, MatchService svc) =>
            svc.GiveUpAsync(user.UserId(), id));
        matches.MapPost("/{id:int}/weapon", (int id, UseWeaponRequest req, ClaimsPrincipal user, MatchService svc) =>
            svc.UseWeaponAsync(user.UserId(), id, req.Target));

        var online = app.MapGroup("/api/online").RequireAuthorization();
        online.MapGet("/stats", (ClaimsPrincipal user, OnlineService svc) => svc.GetStatsAsync(user.UserId()));
    }
}
