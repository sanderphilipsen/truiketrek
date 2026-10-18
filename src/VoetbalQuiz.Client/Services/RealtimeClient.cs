using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace VoetbalQuiz.Client.Services;

/// <summary>
/// Live verbinding met de server (SignalR). De server stuurt enkel een seintje dat er iets veranderde;
/// de pagina haalt dan zelf de nieuwe toestand op.
/// </summary>
public sealed class RealtimeClient(NavigationManager nav) : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly HashSet<int> _watchedMatches = [];
    private bool _watchLobby;

    public event Func<Task>? LobbyChanged;
    public event Func<int, Task>? MatchChanged;

    private async Task<HubConnection> ConnectAsync()
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Disconnected) await _connection.StartAsync();
            return _connection;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri("hubs/match"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On("LobbyChanged", async () =>
        {
            if (LobbyChanged is not null) await LobbyChanged.Invoke();
        });
        _connection.On<int>("MatchChanged", async id =>
        {
            if (MatchChanged is not null) await MatchChanged.Invoke(id);
        });

        // Na een herverbinding opnieuw inschrijven en meteen verversen (we kunnen iets gemist hebben).
        _connection.Reconnected += async _ =>
        {
            if (_watchLobby) await _connection.InvokeAsync("WatchLobby");
            foreach (var id in _watchedMatches) await _connection.InvokeAsync("WatchMatch", id);
            if (LobbyChanged is not null) await LobbyChanged.Invoke();
            foreach (var id in _watchedMatches)
                if (MatchChanged is not null) await MatchChanged.Invoke(id);
        };

        await _connection.StartAsync();
        return _connection;
    }

    public async Task WatchLobbyAsync()
    {
        _watchLobby = true;
        await (await ConnectAsync()).InvokeAsync("WatchLobby");
    }

    public async Task UnwatchLobbyAsync()
    {
        _watchLobby = false;
        if (_connection?.State == HubConnectionState.Connected) await _connection.InvokeAsync("UnwatchLobby");
    }

    public async Task WatchMatchAsync(int id)
    {
        _watchedMatches.Add(id);
        await (await ConnectAsync()).InvokeAsync("WatchMatch", id);
    }

    public async Task UnwatchMatchAsync(int id)
    {
        _watchedMatches.Remove(id);
        if (_connection?.State == HubConnectionState.Connected) await _connection.InvokeAsync("UnwatchMatch", id);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
