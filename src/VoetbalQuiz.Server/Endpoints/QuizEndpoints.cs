using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class QuizEndpoints
{
    public static void MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var daily = app.MapGroup("/api/daily").RequireAuthorization().AddEndpointFilter(HandleQuizErrors);

        daily.MapGet("/", (ClaimsPrincipal user, QuizService quiz) => quiz.GetOverviewAsync(UserId(user)));
        daily.MapPost("/questions/{number:int}/start", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.StartAsync(UserId(user), number));
        daily.MapGet("/questions/{number:int}", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.GetStateAsync(UserId(user), number));
        daily.MapPost("/questions/{number:int}/guess", (int number, AnswerRequest req, ClaimsPrincipal user, QuizService quiz) => quiz.GuessAsync(UserId(user), number, req.Guess));
        daily.MapPost("/questions/{number:int}/giveup", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.GiveUpAsync(UserId(user), number));

        // Specials: dezelfde vragenflow, met een eigen reeks en klassement.
        var specials = app.MapGroup("/api/specials").RequireAuthorization().AddEndpointFilter(HandleQuizErrors);
        specials.MapGet("/", (ClaimsPrincipal user, ResultsService results) => results.GetSpecialsAsync(UserId(user)));
        specials.MapGet("/{id:int}", (int id, ClaimsPrincipal user, QuizService quiz) => quiz.GetOverviewAsync(UserId(user), id));
        specials.MapPost("/{id:int}/questions/{number:int}/start", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.StartAsync(UserId(user), number, id));
        specials.MapGet("/{id:int}/questions/{number:int}", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.GetStateAsync(UserId(user), number, id));
        specials.MapPost("/{id:int}/questions/{number:int}/guess", (int id, int number, AnswerRequest req, ClaimsPrincipal user, QuizService quiz) => quiz.GuessAsync(UserId(user), number, req.Guess, id));
        specials.MapPost("/{id:int}/questions/{number:int}/giveup", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.GiveUpAsync(UserId(user), number, id));

        var board = app.MapGroup("/api/leaderboard").RequireAuthorization();

        board.MapGet("/today", (ClaimsPrincipal user, ResultsService results) => results.GetDailyLeaderboardAsync(UserId(user)));
        board.MapGet("/month/{year:int}/{month:int}", async (int year, int month, ClaimsPrincipal user, MonthRankingService ranking) =>
            year is < 2000 or > 2100 || month is < 1 or > 12
                ? Results.BadRequest(new ErrorResponse("Ongeldige maand."))
                : Results.Ok(await ranking.GetAsync(UserId(user), year, month)));

        app.MapGet("/api/stats", async (ClaimsPrincipal user, ResultsService results, MonthRankingService ranking, TimeProvider clock) =>
        {
            var stats = await results.GetStatsAsync(UserId(user));
            return stats with { MonthChampions = await ranking.ChampionsAsync(UserId(user), QuizClock.Today(clock)) };
        }).RequireAuthorization();
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public static async ValueTask<object?> HandleQuizErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (QuizException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }
}
