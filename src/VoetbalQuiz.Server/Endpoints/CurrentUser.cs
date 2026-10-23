using System.Security.Claims;

namespace VoetbalQuiz.Server.Endpoints;

/// <summary>Wie de aangemelde gebruiker is, en of die admin is.</summary>
public static class CurrentUser
{
    /// <summary>Autorisatiepolicy voor het admin-paneel (zie Program.cs).</summary>
    public const string AdminPolicy = "Admin";

    /// <summary>De id van de aangemelde gebruiker (enkel op endpoints met RequireAuthorization).</summary>
    public static string UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Admins staan in de configuratie onder "Admins" (gebruikersnamen).</summary>
    public static bool IsAdmin(ClaimsPrincipal user, IConfiguration config) => IsAdminName(user.Identity?.Name, config);

    public static bool IsAdminName(string? userName, IConfiguration config) =>
        AdminNames(config).Contains(userName, StringComparer.OrdinalIgnoreCase);

    public static string[] AdminNames(IConfiguration config) => config.GetSection("Admins").Get<string[]>() ?? [];
}
