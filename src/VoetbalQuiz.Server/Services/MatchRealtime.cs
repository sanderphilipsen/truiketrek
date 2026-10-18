using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// SignalR-hub. Clients schrijven zich in op de lobby of op een wedstrijd en krijgen een seintje
/// als er iets verandert; de toestand zelf halen ze op via de API.
/// </summary>
[Authorize]
public class MatchHub : Hub
{
    public Task WatchLobby() => Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
    public Task UnwatchLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
    public Task WatchMatch(int id) => Groups.AddToGroupAsync(Context.ConnectionId, $"match-{id}");
    public Task UnwatchMatch(int id) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{id}");
}

public interface IMatchNotifier
{
    Task LobbyChangedAsync();
    Task MatchChangedAsync(int matchId);
}

public class MatchNotifier(IHubContext<MatchHub> hub) : IMatchNotifier
{
    public Task LobbyChangedAsync() => hub.Clients.Group("lobby").SendAsync("LobbyChanged");
    public Task MatchChangedAsync(int matchId) => hub.Clients.Group($"match-{matchId}").SendAsync("MatchChanged", matchId);
}

/// <summary>Laat de klok van alle wedstrijden lopen: vragen afsluiten, volgende vraag starten, einde bepalen.</summary>
public class MatchEngine(IServiceScopeFactory scopes, ILogger<MatchEngine> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MatchService>().AdvanceAllAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fout bij het verder zetten van wedstrijden");
            }
        }
    }
}
