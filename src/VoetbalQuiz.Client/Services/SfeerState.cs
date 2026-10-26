using Microsoft.JSInterop;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>
/// Welke seizoenssfeer er nu op de app ligt (css/screens/sfeer.css, via data-sfeer op &lt;html&gt;). Twee bronnen:
/// de sfeer van de dag voor de hele app (<see cref="ServerStatus.Sfeer"/>, via <see cref="UpdateNotice"/>) en die van
/// een pagina (een special met een thema, zie Sfeer.razor). Een pagina wint zolang ze openstaat; sluit ze, dan geldt
/// weer de sfeer van de dag. MainLayout tekent de decoratie van <see cref="Current"/>.
/// </summary>
public sealed class SfeerState(IJSRuntime js)
{
    /// <summary>De sferen waarvoor sfeer.css iets doet.</summary>
    public static readonly HashSet<string> Known = [Spookhuis.Theme];

    private string? _app;
    private readonly List<(object Owner, string Name)> _pages = [];
    private string? _applied;

    public string? Current => _pages.Count > 0 ? _pages[^1].Name : _app;

    public event Action? Changed;

    public Task SetAppAsync(string? sfeer)
    {
        _app = Valid(sfeer);
        return ApplyAsync();
    }

    /// <summary>Een pagina zet haar sfeer (null of onbekend = geen eigen sfeer).</summary>
    public Task SetPageAsync(object owner, string? sfeer)
    {
        _pages.RemoveAll(p => p.Owner == owner);
        if (Valid(sfeer) is { } name) _pages.Add((owner, name));
        return ApplyAsync();
    }

    public Task ClearPageAsync(object owner) => SetPageAsync(owner, null);

    private static string? Valid(string? sfeer) => sfeer is not null && Known.Contains(sfeer) ? sfeer : null;

    private async Task ApplyAsync()
    {
        var wanted = Current;
        if (wanted == _applied) return;
        _applied = wanted;
        try
        {
            if (wanted is null) await js.InvokeVoidAsync("document.documentElement.removeAttribute", "data-sfeer");
            else await js.InvokeVoidAsync("document.documentElement.setAttribute", "data-sfeer", wanted);
        }
        catch (JSException) { } // geen sfeer: de app werkt gewoon
        catch (JSDisconnectedException) { }
        Changed?.Invoke();
    }
}
