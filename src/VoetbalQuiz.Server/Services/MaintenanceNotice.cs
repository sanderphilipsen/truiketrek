using System.Reflection;
using System.Text.Json;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Er komt zo een update. Het deployscript (scripts/deploy-azure.ps1) zet vóór het publiceren een bestand op de server
/// en haalt het weg zodra de nieuwe versie draait. Het bestand staat buiten de app-map, zodat de deploy het niet wist.
/// Zolang het er staat, is de app dicht en start niemand een nieuwe vraag: een herstart zou de klok laten doorlopen.
/// </summary>
public class MaintenanceNotice(string? file, TimeProvider clock)
{
    /// <summary>Een vergeten bestand (bv. een afgebroken deploy) blokkeert niet eeuwig.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    public const string BlockedMessage = "Er worden momenteel updates uitgevoerd, we zijn weldra terug.";

    /// <summary>Wanneer de update begint, of null als er geen aankomt.</summary>
    public DateTime? UpdateAt()
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
        try
        {
            var notice = JsonSerializer.Deserialize<NoticeFile>(File.ReadAllText(file), JsonSerializerOptions.Web);
            if (notice is null) return null;
            var at = notice.UpdateAt.ToUniversalTime();
            return clock.GetUtcNow() - at > MaxAge ? null : at.UtcDateTime;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public ServerStatus Status() => new(UpdateAt(), Version);

    /// <summary>Het deployscript geeft elke publicatie een eigen versie mee (zie scripts/deploy-azure.ps1).</summary>
    private static readonly string? Version = typeof(MaintenanceNotice).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private record NoticeFile(DateTimeOffset UpdateAt);
}
