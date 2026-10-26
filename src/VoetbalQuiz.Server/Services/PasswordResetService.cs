using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Zet een willekeurig tijdelijk wachtwoord voor een speler die het zijne vergeten is.
/// De admin geeft het door; de speler kiest daarna zelf een nieuw wachtwoord in Profiel.
/// </summary>
public class PasswordResetService(UserManager<AppUser> users)
{
    // Geen tekens die je makkelijk verwart bij het overtypen (0/O, 1/l/I).
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 12;

    public async Task<ResetPasswordResult?> ResetAsync(string? userName)
    {
        var user = await users.FindByNameAsync(userName?.Trim() ?? "");
        // Een verwijderd account blijft dicht: een reset zou anders het slot opheffen.
        if (user is null || user.DeletedAt is not null) return null;

        var password = RandomNumberGenerator.GetString(Alphabet, Length);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        // Wie zich door te veel pogingen buitensloot, kan meteen weer aanmelden.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return new ResetPasswordResult(user.UserName!, password);
    }
}
