using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

/// <summary>Academie (training): oude dailies en afgelopen specials opnieuw spelen (telt nergens mee). Zie <see cref="TrainingService"/>.</summary>
public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        var training = app.MapGroup("/api/training").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        training.MapGet("/archive", (ClaimsPrincipal user, TrainingService service) => service.GetArchiveAsync(user.UserId()));
        training.MapGet("/archive/{year:int}/{month:int}", async (int year, int month, ClaimsPrincipal user, TrainingService service) =>
            year is < 2000 or > 2100 || month is < 1 or > 12
                ? Results.BadRequest(new ErrorResponse("Ongeldige maand."))
                : Results.Ok(await service.GetArchiveAsync(user.UserId(), year, month)));
        training.MapGet("/days/{date}", (DateOnly date, ClaimsPrincipal user, TrainingService service) => service.GetDayAsync(user.UserId(), date));
        training.MapPost("/days/{date}/runs", (DateOnly date, ClaimsPrincipal user, TrainingService service) => service.StartRunAsync(user.UserId(), date));
        training.MapGet("/specials/{specialId:int}", (int specialId, ClaimsPrincipal user, TrainingService service) => service.GetSpecialAsync(user.UserId(), specialId));
        training.MapPost("/specials/{specialId:int}/runs", (int specialId, ClaimsPrincipal user, TrainingService service) =>
            service.StartSpecialRunAsync(user.UserId(), specialId));

        training.MapGet("/runs/{runId:int}", (int runId, ClaimsPrincipal user, TrainingService service) => service.GetRunOverviewAsync(user.UserId(), runId));
        training.MapPost("/runs/{runId:int}/again", (int runId, ClaimsPrincipal user, TrainingService service) => service.StartAgainAsync(user.UserId(), runId));
        training.MapGet("/runs/{runId:int}/summary", (int runId, ClaimsPrincipal user, TrainingService service) => service.GetSummaryAsync(user.UserId(), runId));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/start", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.StartAsync(user.UserId(), runId, number));
        training.MapGet("/runs/{runId:int}/questions/{number:int}", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.GetStateAsync(user.UserId(), runId, number));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/guess", (int runId, int number, AnswerRequest req, ClaimsPrincipal user, TrainingService service) =>
            service.GuessAsync(user.UserId(), runId, number, req.Guess));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/giveup", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.GiveUpAsync(user.UserId(), runId, number));
    }
}
