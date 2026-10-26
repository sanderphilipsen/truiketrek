using Microsoft.JSInterop;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>Wat dit toestel met pushmeldingen kan (zie window.truitje.pushState in index.html).</summary>
public enum PushDeviceState
{
    /// <summary>De browser kent geen pushmeldingen.</summary>
    Unsupported,
    /// <summary>iPhone of iPad in de browser: pushmeldingen werken daar enkel als de app op het beginscherm staat.</summary>
    IosInstall,
    /// <summary>Nog niet gevraagd.</summary>
    Default,
    Granted,
    /// <summary>De speler weigerde; enkel in de instellingen van de browser terug aan te zetten.</summary>
    Denied,
}

/// <summary>Pushmeldingen op dit toestel: toestemming vragen, het toestel aanmelden bij de server, en de herinnering aan/uit.</summary>
public class PushClient(ApiClient api, IJSRuntime js)
{
    public async Task<PushDeviceState> GetDeviceStateAsync()
    {
        try
        {
            return await js.InvokeAsync<string>("truitje.pushState") switch
            {
                "ios-install" => PushDeviceState.IosInstall,
                "default" => PushDeviceState.Default,
                "granted" => PushDeviceState.Granted,
                "denied" => PushDeviceState.Denied,
                _ => PushDeviceState.Unsupported,
            };
        }
        catch (JSException) { return PushDeviceState.Unsupported; }
    }

    /// <summary>
    /// Dit toestel aanmelden voor meldingen. Met <paramref name="ask"/> vraagt de browser toestemming (enkel na een tik);
    /// zonder enkel als die er al is. True als het toestel nu meldingen krijgt.
    /// </summary>
    public async Task<bool> EnableDeviceAsync(string publicKey, bool ask)
    {
        PushSubscribeRequest? subscription;
        try { subscription = await js.InvokeAsync<PushSubscribeRequest?>("truitje.pushSubscribe", publicKey, ask); }
        catch (JSException) { return false; }
        if (subscription is null) return false;
        await api.SubscribePushAsync(subscription);
        return true;
    }

    /// <summary>Bij het afmelden: dit toestel krijgt geen meldingen meer voor deze speler. Een fout houdt het afmelden niet tegen.</summary>
    public async Task ForgetDeviceAsync()
    {
        try
        {
            if (await js.InvokeAsync<string?>("truitje.pushEndpoint") is { } endpoint)
                await api.UnsubscribePushAsync(new PushUnsubscribeRequest(endpoint));
        }
        catch (Exception ex) when (ex is JSException or ApiException or HttpRequestException) { }
    }
}
