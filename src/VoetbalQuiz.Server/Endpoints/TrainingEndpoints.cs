using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

/// <summary>Academie (training): oude dailies en afgelopen specials opnieuw spelen (telt nergens mee). Zie <see cref="TrainingService"/>.</summary>
public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        var training = app.MapGroup("/api/training").RequireAuthorization().AddEndpointFilter(QuizEndpoints.HandleQuizErrors);

        training.MapGet("/archive", (ClaimsPrincipal user, TrainingService service) => service.GetArchiveAsync(UserId(user)));
        training.MapGet("/archive/{year:int}/{month:int}", async (int year, int month, ClaimsPrincipal user, TrainingService service) =>
            year is < 2000 or > 2100 || month is < 1 or > 12
                ? Results.BadRequest(new ErrorResponse("Ongeldige maand."))
                : Results.Ok(await service.GetArchiveAsync(UserId(user), year, month)));
        training.MapGet("/days/{date}", (DateOnly date, ClaimsPrincipal user, TrainingService service) => service.GetDayAsync(UserId(user), date));
        training.MapPost("/days/{date}/runs", (DateOnly date, ClaimsPrincipal user, TrainingService service) => service.StartRunAsync(UserId(user), date));
        training.MapGet("/specials/{specialId:int}", (int specialId, ClaimsPrincipal user, TrainingService service) => service.GetSpecialAsync(UserId(user), specialId));
        training.MapPost("/specials/{specialId:int}/runs", (int specialId, ClaimsPrincipal user, TrainingService service) =>
            service.StartSpecialRunAsync(UserId(user), specialId));

        training.MapGet("/runs/{runId:int}", (int runId, ClaimsPrincipal user, TrainingService service) => service.GetRunOverviewAsync(UserId(user), runId));
        training.MapPost("/runs/{runId:int}/again", (int runId, ClaimsPrincipal user, TrainingService service) => service.StartAgainAsync(UserId(user), runId));
        training.MapGet("/runs/{runId:int}/summary", (int runId, ClaimsPrincipal user, TrainingService service) => service.GetSummaryAsync(UserId(user), runId));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/start", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.StartAsync(UserId(user), runId, number));
        training.MapGet("/runs/{runId:int}/questions/{number:int}", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.GetStateAsync(UserId(user), runId, number));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/guess", (int runId, int number, AnswerRequest req, ClaimsPrincipal user, TrainingService service) =>
            service.GuessAsync(UserId(user), runId, number, req.Guess));
        training.MapPost("/runs/{runId:int}/questions/{number:int}/giveup", (int runId, int number, ClaimsPrincipal user, TrainingService service) =>
            service.GiveUpAsync(UserId(user), runId, number));
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
