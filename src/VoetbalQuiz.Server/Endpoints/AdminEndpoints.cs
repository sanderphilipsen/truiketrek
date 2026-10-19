using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
                FeedbackEndpoints.IsAdmin(ctx.HttpContext.User, ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>())
                    ? await next(ctx)
                    : Results.Forbid())
            .AddEndpointFilter(QuizEndpoints.HandleQuizErrors);

        admin.MapGet("/dashboard", (AdminService svc, TimeProvider clock) => svc.DashboardAsync(QuizClock.Today(clock)));

        // Spelers
        admin.MapGet("/players/counts", (AdminService svc) => svc.CountsAsync());
        admin.MapGet("/players", (PlayerStatus? status, string? search, string? tag, AdminService svc) => svc.ListAsync(status, search, tag));
        admin.MapGet("/players/{id:int}", (int id, AdminService svc) => svc.GetAsync(id));
        admin.MapPost("/players", async (PlayerEdit edit, ClaimsPrincipal user, AdminService svc) =>
            new { Id = await svc.SaveAsync(edit, user.Identity!.Name!) });

        // Weekplanning
        admin.MapGet("/schedule", (AdminService svc) => svc.ScheduleAsync());
        admin.MapPost("/schedule", async (List<ScheduleDay> days, AdminService svc) =>
        {
            await svc.SaveScheduleAsync(days);
            return Results.Ok();
        });
        admin.MapPost("/schedule/types/{type}", async (QuestionType type, bool enabled, AdminService svc) =>
        {
            await svc.SetTypeEnabledAsync(type, enabled);
            return Results.Ok();
        });

        // Vragenbank
        admin.MapGet("/questions", (QuestionType? type, PlayerStatus? status, string? search, AdminService svc) => svc.QuizItemsAsync(type, status, search));
        admin.MapGet("/questions/{id:int}", (int id, AdminService svc) => svc.QuizItemAsync(id));
        admin.MapPost("/questions", async (QuizItemEdit edit, ClaimsPrincipal user, AdminService svc) =>
            new { Id = await svc.SaveQuizItemAsync(edit, user.Identity!.Name!) });

        // Accounts: een admin zet een tijdelijk wachtwoord voor wie het zijne vergeten is.
        admin.MapPost("/users/reset-password", async (ResetPasswordRequest req, ClaimsPrincipal user,
            PasswordResetService svc, ILogger<PasswordResetService> log) =>
        {
            var result = await svc.ResetAsync(req.UserName);
            if (result is null)
                return Results.NotFound(new ErrorResponse("Er bestaat geen gebruiker met die naam."));

            log.LogInformation("Wachtwoord van {User} gereset door {Admin}", result.UserName, user.Identity!.Name);
            return Results.Ok(result);
        });

        // Specials
        admin.MapGet("/specials", (AdminService svc) => svc.SpecialsAsync());
        admin.MapGet("/specials/{id:int}", (int id, AdminService svc) => svc.GetSpecialAsync(id));
        admin.MapPost("/specials", async (SpecialEdit edit, AdminService svc) => new { Id = await svc.SaveSpecialAsync(edit) });
        admin.MapDelete("/specials/{id:int}", async (int id, AdminService svc) =>
        {
            await svc.DeleteSpecialAsync(id);
            return Results.Ok();
        });
    }
}
