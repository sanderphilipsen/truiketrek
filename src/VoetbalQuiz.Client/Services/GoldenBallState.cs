using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>
/// Wie welke Gouden Ballen heeft, zodat elke avatar zijn sterren en gouden rand kan tonen.
/// De server beslist alles (ook welke dag het in België is); hier wordt enkel af en toe opnieuw geladen.
/// </summary>
public class GoldenBallState(ApiClient api)
{
    /// <summary>Hooguit zo lang oud: de app kan over de maandwissel heen open blijven.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private Task? _loading;
    private DateTime _loadedAt;
    private int _generation;

    public GoldenBallOverview? Overview { get; private set; }

    /// <summary>Gaat af zodra er (nieuwe) gegevens zijn, zodat de avatars opnieuw tekenen.</summary>
    public event Action? Changed;

    public int StarsOf(string? userName) =>
        userName is not null && Overview?.Stars.TryGetValue(userName, out var stars) == true ? stars : 0;

    /// <summary>Won vorige maand de Gouden Bal: draagt deze maand de gouden rand.</summary>
    public bool IsHolder(string? userName) =>
        userName is not null && Overview?.Holders.Any(h => h.UserName == userName) == true;

    /// <summary>Laadt de gegevens als ze er nog niet zijn of te oud zijn.</summary>
    public Task EnsureLoadedAsync()
    {
        if (_loading is null || (_loading.IsCompleted && DateTime.UtcNow - _loadedAt > MaxAge))
        {
            _loadedAt = DateTime.UtcNow;
            _loading = LoadAsync(_generation);
        }
        return _loading;
    }

    /// <summary>De uitreiking is weggeklikt: meteen weg, en de server onthoudt het voor alle toestellen.</summary>
    public async Task DismissCeremonyAsync()
    {
        if (Overview?.Ceremony is null) return;
        Overview = Overview with { Ceremony = null };
        Changed?.Invoke();
        try
        {
            await api.MarkGoldenBallCeremonySeenAsync();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            // Lukt het niet, dan komt de uitreiking bij een volgende lading nog eens: geen ramp.
        }
    }

    /// <summary>Na afmelden: niets meer tonen tot de volgende aanmelding.</summary>
    public void Clear()
    {
        _generation++; // een lading die nog bezig is, wordt genegeerd
        _loading = null;
        Overview = null;
        Changed?.Invoke();
    }

    private async Task LoadAsync(int generation)
    {
        try
        {
            var overview = await api.GetGoldenBallAsync();
            if (generation != _generation) return;
            Overview = overview;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            if (generation == _generation) _loading = null; // later opnieuw proberen; zonder sterren werkt alles gewoon
        }
    }
}
