using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Aanmelden met Google: een bestaande koppeling vinden, een account met hetzelfde bevestigde e-mailadres koppelen,
/// of een nieuwe speler aanmaken met een zelfgekozen gebruikersnaam. Een account waarvan het e-mailadres nooit
/// bevestigd is, wordt pas gekoppeld nadat de speler zich er met het wachtwoord op aanmeldde.
/// </summary>
public sealed class GoogleLoginService(UserManager<AppUser> users, TimeProvider clock)
{
    public const string Provider = "Google";
    public const string ClientIdKey = "Authentication:Google:ClientId";
    public const string ClientSecretKey = "Authentication:Google:ClientSecret";
    public const string EmailVerifiedClaim = "email_verified";
    public const string EmailNotVerifiedCode = "GoogleEmailNotVerified";

    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[ClientIdKey]) && !string.IsNullOrWhiteSpace(config[ClientSecretKey]);

    /// <summary>De speler bij deze Google-aanmelding (bij <see cref="GoogleNext.SignIn"/>), en wat er verder moet gebeuren.</summary>
    public async Task<(GoogleNext Next, AppUser? User)> FindOrLinkAsync(ExternalLoginInfo info)
    {
        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is not null) return (GoogleNext.SignIn, user);

        var email = Email(info);
        if (email is null || !IsEmailVerified(info)) return (GoogleNext.EmailNotVerified, null);

        user = await users.FindByEmailAsync(email);
        if (user is null) return (GoogleNext.ChooseUserName, null);

        // Bij /register wordt het e-mailadres niet bevestigd: het kan dat van iemand anders zijn. Automatisch koppelen
        // zou die persoon het account geven, dus eerst aanmelden met het wachtwoord (LinkAfterPasswordAsync).
        if (!user.EmailConfirmed) return (GoogleNext.SignInWithPasswordFirst, null);

        var result = await users.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, Provider));
        return result.Succeeded ? (GoogleNext.SignIn, user) : (GoogleNext.SignInWithPasswordFirst, null);
    }

    /// <summary>
    /// Koppelt Google aan een account waarop de speler zich net met het wachtwoord aanmeldde. Zo is bewezen dat die
    /// zowel het account als het Google-adres bezit. Enkel als het (door Google bevestigde) adres dat van het account is.
    /// </summary>
    public async Task<bool> LinkAfterPasswordAsync(AppUser user, ExternalLoginInfo info)
    {
        var email = Email(info);
        if (info.LoginProvider != Provider || email is null || !IsEmailVerified(info)
            || !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            return false;

        var result = await users.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, Provider));
        if (!result.Succeeded) return false;
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
        }
        return true;
    }

    /// <summary>Een vrije gebruikersnaam op basis van de Google-voornaam, als voorstel op het scherm "Kies je gebruikersnaam".</summary>
    public async Task<string> SuggestUserNameAsync(ExternalLoginInfo info)
    {
        var source = info.Principal.FindFirstValue(ClaimTypes.GivenName)
            ?? info.Principal.FindFirstValue(ClaimTypes.Name)
            ?? Email(info)?.Split('@')[0];
        var baseName = CleanUserName(source);
        if (await users.FindByNameAsync(baseName) is null) return baseName;

        for (var i = 2; ; i++)
        {
            var suffix = i.ToString(CultureInfo.InvariantCulture);
            var candidate = baseName[..Math.Min(baseName.Length, 20 - suffix.Length)] + suffix;
            if (await users.FindByNameAsync(candidate) is null) return candidate;
        }
    }

    /// <summary>Maakt een nieuwe speler zonder wachtwoord, gekoppeld aan deze Google-aanmelding met een bevestigd e-mailadres.</summary>
    public async Task<(AppUser? User, IdentityResult Result)> CreateAsync(ExternalLoginInfo info, string userName)
    {
        var email = Email(info);
        if (email is null || !IsEmailVerified(info))
            return (null, IdentityResult.Failed(new IdentityError { Code = EmailNotVerifiedCode, Description = "Je Google-account heeft geen bevestigd e-mailadres." }));

        // Nieuwe spelers vertrekken van nul, zodat ze hun eerste XP en badges in de beloningspopup zien.
        var user = new AppUser
        {
            UserName = userName, Email = email, EmailConfirmed = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            SeenXpParts = [.. Levels.Breakdown(new XpInput(0, 0, 0, 0, 0)).Select(p => p.Xp)], SeenBadges = [],
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) return (null, result);

        result = await users.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, Provider));
        if (!result.Succeeded)
        {
            await users.DeleteAsync(user);
            return (null, result);
        }
        return (user, result);
    }

    public static string? Email(ExternalLoginInfo info) =>
        info.Principal.FindFirstValue(ClaimTypes.Email) is { Length: > 0 } email ? email.Trim() : null;

    private static bool IsEmailVerified(ExternalLoginInfo info) =>
        string.Equals(info.Principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Enkel letters zonder accenten, cijfers en - . _ (zoals Identity toelaat), 2 tot 20 tekens.</summary>
    public static string CleanUserName(string? name)
    {
        var plain = new StringBuilder();
        foreach (var c in (name ?? "").Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_') plain.Append(c);
            if (plain.Length == 20) break;
        }
        return plain.Length >= 2 ? plain.ToString() : "speler";
    }
}

/// <summary>Wat er na een Google-aanmelding moet gebeuren.</summary>
public enum GoogleNext
{
    /// <summary>Gekende of net gekoppelde speler: meteen aanmelden.</summary>
    SignIn,
    /// <summary>Nog geen account met dit e-mailadres: een gebruikersnaam kiezen.</summary>
    ChooseUserName,
    /// <summary>Er is een account met dit adres, maar dat adres is nooit bevestigd: eerst aanmelden met het wachtwoord.</summary>
    SignInWithPasswordFirst,
    /// <summary>Google gaf geen bevestigd e-mailadres mee.</summary>
    EmailNotVerified,
}
