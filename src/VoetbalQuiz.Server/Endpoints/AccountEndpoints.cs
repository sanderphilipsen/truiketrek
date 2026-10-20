using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account");

        group.MapPost("/register", async (RegisterRequest req, UserManager<AppUser> users, SignInManager<AppUser> signIn, ResultsService results,
            IConfiguration config, TimeProvider clock) =>
        {
            var userName = req.UserName?.Trim() ?? "";
            if (userName.Length is < 2 or > 20)
                return Results.BadRequest(new ErrorResponse("Kies een gebruikersnaam van 2 tot 20 tekens."));

            // Nieuwe spelers vertrekken van nul, zodat ze hun eerste XP en badges in de beloningspopup zien.
            var user = new AppUser
            {
                UserName = userName, Email = req.Email?.Trim(), CreatedAt = clock.GetUtcNow().UtcDateTime,
                SeenXpParts = [.. Levels.Breakdown(new XpInput(0, 0, 0, 0, 0)).Select(p => p.Xp)], SeenBadges = [],
            };
            var result = await users.CreateAsync(user, req.Password ?? "");
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorResponse(Translate(result.Errors)));

            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(await InfoAsync(user, results, config));
        });

        group.MapPost("/login", async (LoginRequest req, UserManager<AppUser> users, SignInManager<AppUser> signIn, ResultsService results, IConfiguration config) =>
        {
            var key = req.UserNameOrEmail?.Trim() ?? "";
            var user = key.Contains('@') ? await users.FindByEmailAsync(key) : await users.FindByNameAsync(key);
            if (user is null)
                return Results.BadRequest(new ErrorResponse("Onbekende gebruiker of verkeerd wachtwoord."));

            var result = await signIn.PasswordSignInAsync(user, req.Password ?? "", isPersistent: true, lockoutOnFailure: true);
            if (result.IsLockedOut)
                return Results.BadRequest(new ErrorResponse("Te veel pogingen. Probeer het over enkele minuten opnieuw."));
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorResponse("Onbekende gebruiker of verkeerd wachtwoord."));

            return Results.Ok(await InfoAsync(user, results, config));
        });

        group.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, ResultsService results, IConfiguration config) =>
        {
            var user = await users.GetUserAsync(principal);
            return user is null ? Results.Unauthorized() : Results.Ok(await InfoAsync(user, results, config));
        }).RequireAuthorization();

        group.MapGet("/profile", async (ClaimsPrincipal principal, UserManager<AppUser> users, ResultsService results) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            var inputs = await results.XpInputsAsync();
            var input = inputs.GetValueOrDefault(user.Id) ?? new XpInput(0, 0, 0, 0, 0);
            return Results.Ok(new ProfileInfo(
                user.UserName!, user.Email ?? "", user.AvatarId, QuizClock.ToBrusselsDate(user.CreatedAt),
                Levels.For(Levels.Xp(input)), Levels.Breakdown(input)));
        }).RequireAuthorization();

        group.MapPost("/password", async (ChangePasswordRequest req, ClaimsPrincipal principal,
            UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            var result = await users.ChangePasswordAsync(user, req.CurrentPassword ?? "", req.NewPassword ?? "");
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorResponse(Translate(result.Errors)));

            // Het wachtwoord wijzigen vernieuwt de beveiligingsstempel: opnieuw aanmelden houdt deze sessie geldig.
            await signIn.RefreshSignInAsync(user);
            return Results.Ok();
        }).RequireAuthorization();

        group.MapPost("/avatar", async (SetAvatarRequest req, ClaimsPrincipal principal,
            UserManager<AppUser> users, ResultsService results, IConfiguration config) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (!Avatars.IsValid(req.AvatarId))
                return Results.BadRequest(new ErrorResponse("Onbekende avatar."));

            user.AvatarId = req.AvatarId;
            await users.UpdateAsync(user);
            results.ForgetUserCards();
            return Results.Ok(await InfoAsync(user, results, config));
        }).RequireAuthorization();
    }

    private static async Task<UserInfo> InfoAsync(AppUser user, ResultsService results, IConfiguration config) =>
        new(user.UserName!, user.Email ?? "", user.AvatarId, (await results.LevelOfAsync(user.Id)).Level,
            CurrentUser.IsAdminName(user.UserName, config));

    private static string Translate(IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(e => e.Code switch
        {
            "DuplicateUserName" => "Die gebruikersnaam is al bezet.",
            "DuplicateEmail" => "Er bestaat al een account met dit e-mailadres.",
            "InvalidEmail" => "Dat e-mailadres is ongeldig.",
            "InvalidUserName" => "De gebruikersnaam mag enkel letters, cijfers en - . _ bevatten.",
            "PasswordTooShort" => "Het wachtwoord moet minstens 6 tekens lang zijn.",
            "PasswordMismatch" => "Je huidige wachtwoord klopt niet.",
            _ => e.Description,
        }).Distinct());
}
