using System.Text.Json;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>
/// Kondigt de server een update aan (het deployscript zet dat vlak voor het publiceren)? Elke halve minuut opnieuw
/// gevraagd, zodat een app die openstaat het op tijd toont. Antwoordt de server even niet (de herstart zelf),
/// dan blijft de laatste toestand staan.
/// </summary>
public sealed class UpdateNotice(ApiClient api) : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public const string Message = "Er worden momenteel updates uitgevoerd, we zijn weldra terug.";

    private PeriodicTimer? _timer;

    /// <summary>Wanneer de update begint (UTC), of null.</summary>
    public DateTime? UpdateAt { get; private set; }

    public bool IsComing => UpdateAt is not null;

    /// <summary>Gaat af wanneer er een update aangekondigd wordt en wanneer ze voorbij is.</summary>
    public event Action? Changed;

    public void Start()
    {
        if (_timer is not null) return;
        _timer = new PeriodicTimer(Interval);
        _ = PollAsync(_timer);
    }

    private async Task PollAsync(PeriodicTimer timer)
    {
        await RefreshAsync();
        while (await timer.WaitForNextTickAsync()) await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var updateAt = (await api.GetStatusAsync()).UpdateAt;
            if (updateAt == UpdateAt) return;
            UpdateAt = updateAt;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or JsonException) { }
    }

    public void Dispose() => _timer?.Dispose();
}
