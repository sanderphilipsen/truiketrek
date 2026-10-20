using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class QuizEndpoints
{
    public static void MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var daily = app.MapGroup("/api/daily").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        daily.MapGet("/", (ClaimsPrincipal user, QuizService quiz) => quiz.GetOverviewAsync(user.UserId()));
        // Home: je plaats in het klassement van vandaag en die bij je vorige bezoek (POST, want het bezoek wordt onthouden).
        daily.MapPost("/rank", (ClaimsPrincipal user, QuizService quiz) => quiz.VisitDailyRankAsync(user.UserId()));
        daily.MapPost("/questions/{number:int}/start", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.StartAsync(user.UserId(), number));
        daily.MapGet("/questions/{number:int}", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.GetStateAsync(user.UserId(), number));
        daily.MapPost("/questions/{number:int}/guess", (int number, AnswerRequest req, ClaimsPrincipal user, QuizService quiz) => quiz.GuessAsync(user.UserId(), number, req.Guess));
        daily.MapPost("/questions/{number:int}/giveup", (int number, ClaimsPrincipal user, QuizService quiz) => quiz.GiveUpAsync(user.UserId(), number));

        // Specials: dezelfde vragenflow, met een eigen reeks en klassement.
        var specials = app.MapGroup("/api/specials").RequireAuthorization().AddEndpointFilter(EndpointFilters.HandleQuizErrors);
        specials.MapGet("/", (ClaimsPrincipal user, QuizService quiz) => quiz.GetSpecialsAsync(user.UserId()));
        specials.MapGet("/{id:int}", (int id, ClaimsPrincipal user, QuizService quiz) => quiz.GetOverviewAsync(user.UserId(), id));
        specials.MapPost("/{id:int}/questions/{number:int}/start", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.StartAsync(user.UserId(), number, id));
        specials.MapGet("/{id:int}/questions/{number:int}", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.GetStateAsync(user.UserId(), number, id));
        specials.MapPost("/{id:int}/questions/{number:int}/guess", (int id, int number, AnswerRequest req, ClaimsPrincipal user, QuizService quiz) => quiz.GuessAsync(user.UserId(), number, req.Guess, id));
        specials.MapPost("/{id:int}/questions/{number:int}/giveup", (int id, int number, ClaimsPrincipal user, QuizService quiz) => quiz.GiveUpAsync(user.UserId(), number, id));

        var board = app.MapGroup("/api/leaderboard").RequireAuthorization();

        board.MapGet("/today", (ClaimsPrincipal user, ResultsService results) => results.GetDailyLeaderboardAsync(user.UserId()));
        board.MapGet("/special/{id:int}", (int id, ClaimsPrincipal user, ResultsService results) => results.GetSpecialLeaderboardAsync(user.UserId(), id));
        board.MapGet("/month/{year:int}/{month:int}", async (int year, int month, ClaimsPrincipal user, MonthRankingService ranking) =>
            year is < 2000 or > 2100 || month is < 1 or > 12
                ? Results.BadRequest(new ErrorResponse("Ongeldige maand."))
                : Results.Ok(await ranking.GetAsync(user.UserId(), year, month)));

        // De maandwinnaars staan apart: zie /api/gouden-bal.
        app.MapGet("/api/stats", (ClaimsPrincipal user, ResultsService results) => results.GetStatsAsync(user.UserId()))
            .RequireAuthorization();
    }
}
