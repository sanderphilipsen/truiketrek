using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
// De rij uit de database, niet het gelijknamige type van de bibliotheek.
using PushSubscription = VoetbalQuiz.Server.Data.PushSubscription;

namespace VoetbalQuiz.Server.Services;

/// <summary>Wat een pushmelding toont, en welke pagina in de app ze opent (bv. "daily").</summary>
public record PushPayload(string Title, string Body, string Url, string Tag);

public enum PushResult { Sent, Gone, Failed }

/// <summary>Verstuurt één pushmelding naar één toestel. Een interface zodat tests zonder pushdienst kunnen.</summary>
public interface IPushSender
{
    /// <summary>De publieke VAPID-sleutel; null = pushmeldingen staan niet ingesteld en er wordt niets verstuurd.</summary>
    string? PublicKey { get; }

    /// <summary><see cref="PushResult.Gone"/>: de pushdienst kent dit toestel niet meer (afgemeld of verlopen).</summary>
    Task<PushResult> SendAsync(PushSubscription device, PushPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Web push met VAPID. De sleutels staan in de configuratie onder Push:PublicKey en Push:PrivateKey (lokaal in
/// user-secrets, op Azure als app-instelling; maak ze met scripts/vapid-keys.ps1). Zonder sleutels doet de app niets met push.
/// De sleutels mogen nooit wijzigen: elk abonnement hoort bij de publieke sleutel waarmee het gemaakt werd.
/// </summary>
public sealed class WebPushSender : IPushSender, IDisposable
{
    public const string PublicKeyKey = "Push:PublicKey";
    public const string PrivateKeyKey = "Push:PrivateKey";
    public const string SubjectKey = "Push:Subject";

    /// <summary>Zo lang probeert de pushdienst een toestel dat uit staat nog te bereiken; daarna is de herinnering voorbij.</summary>
    private static readonly int TimeToLive = (int)TimeSpan.FromHours(4).TotalSeconds;

    private readonly VapidAuthentication? _vapid;
    private readonly PushServiceClient _client = new(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }));
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(IConfiguration config, ILogger<WebPushSender> logger)
    {
        _logger = logger;
        if (config[PublicKeyKey] is not { Length: > 0 } publicKey || config[PrivateKeyKey] is not { Length: > 0 } privateKey) return;
        PublicKey = publicKey;
        _vapid = new VapidAuthentication(publicKey, privateKey)
        {
            // De pushdienst gebruikt dit om de afzender te contacteren bij problemen.
            Subject = config[SubjectKey] is { Length: > 0 } subject ? subject : "https://truitje-trek.azurewebsites.net",
        };
    }

    public string? PublicKey { get; }

    public async Task<PushResult> SendAsync(PushSubscription device, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (_vapid is null) return PushResult.Failed;

        var subscription = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = device.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);
        var message = new PushMessage(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web))
        {
            TimeToLive = TimeToLive,
            Urgency = PushMessageUrgency.Normal,
        };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, _vapid, cancellationToken);
            return PushResult.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushResult.Gone;
        }
        catch (Exception ex) when ((ex is PushServiceClientException or HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Pushmelding naar toestel {Id} mislukt", device.Id);
            return PushResult.Failed;
        }
    }

    public void Dispose() => _vapid?.Dispose();
}
