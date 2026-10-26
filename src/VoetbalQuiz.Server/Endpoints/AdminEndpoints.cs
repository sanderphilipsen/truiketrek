using System.Security.Claims;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(CurrentUser.AdminPolicy)
            .AddEndpointFilter(EndpointFilters.HandleQuizErrors);

        admin.MapGet("/dashboard", (AdminService svc, TimeProvider clock) => svc.DashboardAsync(QuizClock.Today(clock)));
        admin.MapGet("/users", (AdminService svc, IConfiguration config) => svc.UsersAsync(CurrentUser.AdminNames(config)));

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

        // Accounts: een admin past naam of e-mail van een speler aan. Admins staan op naam in de configuratie: een admin
        // hernoemen zou hem zijn rechten afnemen, een speler een admin-naam geven zou hem ze geven.
        admin.MapPost("/users/edit", async (EditUserRequest req, ClaimsPrincipal user, IConfiguration config,
            MemberEditService svc, ILogger<MemberEditService> log) =>
        {
            var oldName = req.UserName?.Trim() ?? "";
            var newName = req.NewUserName?.Trim() ?? "";
            if (newName.Length is < 2 or > 20)
                return Results.BadRequest(new ErrorResponse("Kies een gebruikersnaam van 2 tot 20 tekens."));
            var (wasAdmin, becomesAdmin) = (CurrentUser.IsAdminName(oldName, config), CurrentUser.IsAdminName(newName, config));
            if (wasAdmin && !becomesAdmin)
                return Results.BadRequest(new ErrorResponse("De naam van een admin pas je aan in de configuratie, niet hier."));
            if (!wasAdmin && becomesAdmin)
                return Results.BadRequest(new ErrorResponse("Die naam is voorbehouden voor een admin."));

            var result = await svc.EditAsync(oldName, newName, req.Email);
            if (result is null)
                return Results.NotFound(new ErrorResponse("Er bestaat geen speler met die naam."));
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorResponse(AccountEndpoints.Translate(result.Errors)));

            log.LogInformation("Account {User} aangepast door {Admin} (naam nu {NewName})", oldName, user.Identity!.Name, newName);
            return Results.Ok();
        });

        // Accounts: een admin verwijdert het account van een speler, met of zonder aanvraag (anonimiseren, zie
        // AccountDeletionService). Admins zelf niet: die staan in de configuratie en zouden zich zo buitensluiten.
        admin.MapPost("/users/delete", async (DeleteUserRequest req, ClaimsPrincipal user, IConfiguration config,
            AccountDeletionService svc, ILogger<AccountDeletionService> log) =>
        {
            if (CurrentUser.IsAdminName(req.UserName?.Trim(), config))
                return Results.BadRequest(new ErrorResponse("Een admin kan je niet verwijderen."));

            var deleted = await svc.DeleteAsync(req.UserName, onlyRequested: false);
            if (deleted is null)
                return Results.NotFound(new ErrorResponse("Er bestaat geen speler met die naam."));

            log.LogInformation("Account van {User} verwijderd door {Admin}", deleted, user.Identity!.Name);
            return Results.Ok();
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
