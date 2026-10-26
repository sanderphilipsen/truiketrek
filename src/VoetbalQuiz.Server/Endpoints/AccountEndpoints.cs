using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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

        group.MapPost("/login", async (LoginRequest req, HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn,
            GoogleLoginService google, ResultsService results, IConfiguration config) =>
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

            // Kwam de speler van "Doorgaan met Google" (fout=koppel)? Nu het wachtwoord klopt, Google koppelen.
            if (await signIn.GetExternalLoginInfoAsync() is { } external)
            {
                await google.LinkAfterPasswordAsync(user, external);
                await http.SignOutAsync(IdentityConstants.ExternalScheme);
            }

            return Results.Ok(await InfoAsync(user, results, config));
        });

        group.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok();
        });

        group.MapGet("/options", (IConfiguration config) => new LoginOptions(GoogleLoginService.IsConfigured(config)));

        // Stap 1: naar Google. Google stuurt terug naar /signin-google, dat de gegevens in de externe cookie zet
        // en daarna doorgaat naar /google/klaar.
        group.MapGet("/google", (string? terug, SignInManager<AppUser> signIn, IConfiguration config) =>
        {
            if (!GoogleLoginService.IsConfigured(config)) return Results.NotFound();
            var properties = signIn.ConfigureExternalAuthenticationProperties(GoogleLoginService.Provider,
                "/api/account/google/klaar" + ReturnQuery(terug));
            return Results.Challenge(properties, [GoogleLoginService.Provider]);
        });

        // Stap 2: bestaande of gekoppelde speler meteen aanmelden; een nieuwe speler kiest eerst een gebruikersnaam.
        group.MapGet("/google/klaar", async (string? terug, HttpContext http, SignInManager<AppUser> signIn,
            UserManager<AppUser> users, GoogleLoginService google) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/login?fout=google");

            var (next, user) = await google.FindOrLinkAsync(info);
            switch (next)
            {
                case GoogleNext.ChooseUserName:
                    return Results.LocalRedirect("~/login/naam" + ReturnQuery(terug));
                case GoogleNext.SignInWithPasswordFirst:
                    // De externe cookie blijft staan: /login koppelt Google na een geslaagde aanmelding met het wachtwoord.
                    return Results.LocalRedirect("~/login?fout=koppel" + ReturnQuery(terug, '&'));
                case GoogleNext.EmailNotVerified:
                    await http.SignOutAsync(IdentityConstants.ExternalScheme);
                    return Results.LocalRedirect("~/login?fout=onbevestigd");
            }

            await http.SignOutAsync(IdentityConstants.ExternalScheme);
            if (await users.IsLockedOutAsync(user!)) return Results.LocalRedirect("~/login?fout=geblokkeerd");
            await signIn.SignInAsync(user!, isPersistent: true);
            return Results.LocalRedirect("~/" + ReturnPath.Safe(terug));
        });

        group.MapGet("/google/nieuw", async (SignInManager<AppUser> signIn, GoogleLoginService google) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            return info is null
                ? Results.NotFound()
                : Results.Ok(new GoogleSignupInfo(await google.SuggestUserNameAsync(info), GoogleLoginService.Email(info) ?? ""));
        });

        group.MapPost("/google/nieuw", async (GoogleSignupRequest req, HttpContext http, SignInManager<AppUser> signIn,
            GoogleLoginService google, ResultsService results, IConfiguration config) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            if (info is null)
                return Results.BadRequest(new ErrorResponse("Je aanmelding bij Google is verlopen. Probeer het opnieuw."));

            var userName = req.UserName?.Trim() ?? "";
            if (userName.Length is < 2 or > 20)
                return Results.BadRequest(new ErrorResponse("Kies een gebruikersnaam van 2 tot 20 tekens."));

            var (user, result) = await google.CreateAsync(info, userName);
            if (user is null)
                return Results.BadRequest(new ErrorResponse(Translate(result.Errors)));

            await http.SignOutAsync(IdentityConstants.ExternalScheme);
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(await InfoAsync(user, results, config));
        });

        group.MapGet("/me",async (ClaimsPrincipal principal, UserManager<AppUser> users, ResultsService results, IConfiguration config) =>
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
                Levels.For(Levels.Xp(input)), Levels.Breakdown(input), await users.HasPasswordAsync(user), user.DeletionRequestedAt));
        }).RequireAuthorization();

        // Account verwijderen: de speler vraagt het aan (of trekt het in), een admin voert het uit.
        group.MapPost("/deletion-request", async (ClaimsPrincipal principal, AccountDeletionService deletion) =>
        {
            await deletion.RequestAsync(principal.UserId());
            return Results.Ok();
        }).RequireAuthorization();

        group.MapDelete("/deletion-request", async (ClaimsPrincipal principal, AccountDeletionService deletion) =>
        {
            await deletion.CancelAsync(principal.UserId());
            return Results.Ok();
        }).RequireAuthorization();

        group.MapPost("/password", async (ChangePasswordRequest req, ClaimsPrincipal principal,
            UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            // Wie zich enkel met Google aanmeldt, heeft nog geen wachtwoord en kiest er gewoon een.
            var result = await users.HasPasswordAsync(user)
                ? await users.ChangePasswordAsync(user, req.CurrentPassword ?? "", req.NewPassword ?? "")
                : await users.AddPasswordAsync(user, req.NewPassword ?? "");
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

    private static string ReturnQuery(string? terug, char separator = '?') =>
        ReturnPath.Safe(terug) is { Length: > 0 } safe ? separator + "terug=" + Uri.EscapeDataString(safe) : "";

    internal static string Translate(IEnumerable<IdentityError> errors) =>
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
