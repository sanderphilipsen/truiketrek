using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>Houdt bij wie er aangemeld is, op basis van /api/account/me.</summary>
public class AuthState(ApiClient api) : AuthenticationStateProvider
{
    private UserInfo? _user;
    private bool _loaded;

    public UserInfo? User => _user;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_loaded)
        {
            try { _user = await api.MeAsync(); } catch { _user = null; }
            _loaded = true;
        }
        return Build();
    }

    public async Task LoginAsync(LoginRequest req) => SetUser(await api.LoginAsync(req));
    public async Task RegisterAsync(RegisterRequest req) => SetUser(await api.RegisterAsync(req));

    public async Task SetAvatarAsync(string avatarId) => SetUser(await api.SetAvatarAsync(avatarId));

    public async Task RefreshAsync()
    {
        try { SetUser(await api.MeAsync()); } catch { /* blijft zoals het was */ }
    }

    public async Task LogoutAsync()
    {
        await api.LogoutAsync();
        SetUser(null);
    }

    private void SetUser(UserInfo? user)
    {
        _user = user;
        _loaded = true;
        NotifyAuthenticationStateChanged(Task.FromResult(Build()));
    }

    private AuthenticationState Build()
    {
        if (_user is null) return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, _user.UserName)], "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
