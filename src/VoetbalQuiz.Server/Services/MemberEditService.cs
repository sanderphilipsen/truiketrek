using Microsoft.AspNetCore.Identity;
using VoetbalQuiz.Server.Data;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Een admin past de gebruikersnaam en/of het e-mailadres van een speler aan. Alles in de quiz hangt aan de
/// gebruikers-id, dus klassementen en uitslagen volgen de nieuwe naam meteen.
/// </summary>
public class MemberEditService(UserManager<AppUser> users, ResultsService results)
{
    /// <summary>Null als er geen (niet-verwijderde) speler met die naam is; anders het resultaat van Identity.</summary>
    public async Task<IdentityResult?> EditAsync(string? userName, string? newUserName, string? email)
    {
        var user = await users.FindByNameAsync(userName?.Trim() ?? "");
        if (user is null || user.DeletedAt is not null) return null;

        newUserName = newUserName?.Trim() ?? "";
        email = email?.Trim() ?? "";
        var emailChanged = !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase);
        if (newUserName == user.UserName && !emailChanged) return IdentityResult.Success;

        // Beide in één keer: UpdateAsync controleert eerst (uniek, geldige tekens) en bewaart dan alles of niets.
        user.UserName = newUserName;
        if (emailChanged)
        {
            user.Email = email;
            // Een nieuw adres is niet bevestigd: Google koppelt er pas aan na aanmelden met het wachtwoord.
            user.EmailConfirmed = false;
        }
        var result = await users.UpdateAsync(user);
        if (result.Succeeded) results.ForgetUserCards();
        return result;
    }
}
