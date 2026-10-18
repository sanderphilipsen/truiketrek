using System.Net;
using System.Net.Http.Json;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

public class ApiException(string message) : Exception(message);

/// <summary>Getypeerde toegang tot de server-API. De login-cookie gaat automatisch mee (zelfde domein).</summary>
public class ApiClient(HttpClient http)
{
    public Task<UserInfo?> MeAsync() => GetOrNullAsync<UserInfo>("api/account/me");
    public Task<UserInfo> LoginAsync(LoginRequest req) => PostAsync<UserInfo>("api/account/login", req);
    public Task<UserInfo> RegisterAsync(RegisterRequest req) => PostAsync<UserInfo>("api/account/register", req);
    public Task LogoutAsync() => http.PostAsync("api/account/logout", null);

    // Daily en specials gebruiken dezelfde vragenflow; een special heeft enkel een ander adres.
    private static string Quiz(int? specialId) => specialId is { } id ? $"api/specials/{id}" : "api/daily";

    public Task<DailyOverview> GetDailyAsync(int? specialId = null) => GetAsync<DailyOverview>(Quiz(specialId));
    public Task<QuestionState> StartAsync(int number, int? specialId = null) =>
        PostAsync<QuestionState>($"{Quiz(specialId)}/questions/{number}/start", null);
    public Task<QuestionState> GetQuestionAsync(int number, int? specialId = null) =>
        GetAsync<QuestionState>($"{Quiz(specialId)}/questions/{number}");
    public Task<QuestionState> GuessAsync(int number, string guess, int? specialId = null) =>
        PostAsync<QuestionState>($"{Quiz(specialId)}/questions/{number}/guess", new AnswerRequest(guess));
    public Task<QuestionState> GiveUpAsync(int number, int? specialId = null) =>
        PostAsync<QuestionState>($"{Quiz(specialId)}/questions/{number}/giveup", null);

    // Specials
    public Task<List<SpecialSummary>> GetSpecialsAsync() => GetAsync<List<SpecialSummary>>("api/specials");

    // Spelersprofiel
    public Task<PublicProfile> GetPlayerProfileAsync(string userName) =>
        GetAsync<PublicProfile>($"api/players/{Uri.EscapeDataString(userName)}");

    // Admin
    public Task<ScheduleOverview> GetScheduleAsync() => GetAsync<ScheduleOverview>("api/admin/schedule");
    public async Task SaveScheduleAsync(List<ScheduleDay> days) => await EnsureOkAsync(await http.PostAsJsonAsync("api/admin/schedule", days));
    public async Task SetTypeEnabledAsync(QuestionType type, bool enabled) =>
        await EnsureOkAsync(await http.PostAsync($"api/admin/schedule/types/{type}?enabled={(enabled ? "true" : "false")}", null));
    public Task<List<QuizItemListItem>> GetQuizItemsAsync(QuestionType? type, PlayerStatus? status, string? search)
    {
        var query = new List<string>();
        if (type is not null) query.Add($"type={type}");
        if (status is not null) query.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        return GetAsync<List<QuizItemListItem>>("api/admin/questions" + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }
    public Task<QuizItemEdit> GetQuizItemAsync(int id) => GetAsync<QuizItemEdit>($"api/admin/questions/{id}");
    public async Task<int> SaveQuizItemAsync(QuizItemEdit edit) => (await PostAsync<CreatedId>("api/admin/questions", edit)).Id;

    public Task<AdminDashboard> GetAdminDashboardAsync() => GetAsync<AdminDashboard>("api/admin/dashboard");
    public Task<PlayerCounts> GetPlayerCountsAsync() => GetAsync<PlayerCounts>("api/admin/players/counts");
    public Task<List<PlayerListItem>> GetAdminPlayersAsync(PlayerStatus? status, string? search, string? tag)
    {
        // Enkel ingevulde filters meesturen (een lege status kan de server niet inlezen).
        var query = new List<string>();
        if (status is not null) query.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrEmpty(tag)) query.Add($"tag={Uri.EscapeDataString(tag)}");
        return GetAsync<List<PlayerListItem>>("api/admin/players" + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }
    public Task<PlayerEdit> GetAdminPlayerAsync(int id) => GetAsync<PlayerEdit>($"api/admin/players/{id}");
    public async Task<int> SaveAdminPlayerAsync(PlayerEdit edit) => (await PostAsync<CreatedId>("api/admin/players", edit)).Id;
    public Task<List<SpecialAdminItem>> GetAdminSpecialsAsync() => GetAsync<List<SpecialAdminItem>>("api/admin/specials");
    public Task<SpecialEdit> GetAdminSpecialAsync(int id) => GetAsync<SpecialEdit>($"api/admin/specials/{id}");
    public async Task<int> SaveAdminSpecialAsync(SpecialEdit edit) => (await PostAsync<CreatedId>("api/admin/specials", edit)).Id;
    public async Task DeleteAdminSpecialAsync(int id) => await EnsureOkAsync(await http.DeleteAsync($"api/admin/specials/{id}"));

    private record CreatedId(int Id);

    public Task<Leaderboard> GetTodayLeaderboardAsync() => GetAsync<Leaderboard>("api/leaderboard/today");
    public Task<MonthRanking> GetMonthRankingAsync(int year, int month) => GetAsync<MonthRanking>($"api/leaderboard/month/{year}/{month}");

    public Task<ProfileInfo> GetProfileAsync() => GetAsync<ProfileInfo>("api/account/profile");
    public Task<RewardSummary> ClaimRewardAsync() => PostAsync<RewardSummary>("api/progress/reward", null);
    public async Task ChangePasswordAsync(ChangePasswordRequest req) =>
        await EnsureOkAsync(await http.PostAsJsonAsync("api/account/password", req));
    public Task<UserInfo> SetAvatarAsync(string avatarId) => PostAsync<UserInfo>("api/account/avatar", new SetAvatarRequest(avatarId));

    public Task<FeedbackOverview> GetFeedbackAsync() => GetAsync<FeedbackOverview>("api/feedback");
    public async Task SendFeedbackAsync(FeedbackRequest req) => await EnsureOkAsync(await http.PostAsJsonAsync("api/feedback", req));
    public async Task UpdateFeedbackAsync(int id, FeedbackUpdate req) => await EnsureOkAsync(await http.PutAsJsonAsync($"api/feedback/{id}", req));

    // Online
    public Task<LobbyOverview> GetLobbyAsync() => GetAsync<LobbyOverview>("api/matches");
    public Task<List<QuestionType>> GetMatchTypesAsync() => GetAsync<List<QuestionType>>("api/matches/types");
    public async Task<int> CreateMatchAsync(CreateMatchRequest req) => (await PostAsync<CreatedMatch>("api/matches", req)).Id;
    public Task<MatchState> GetMatchAsync(int id) => GetAsync<MatchState>($"api/matches/{id}");
    public Task<MatchState> JoinMatchAsync(int id) => PostAsync<MatchState>($"api/matches/{id}/join", null);
    public async Task LeaveMatchAsync(int id) => await EnsureOkAsync(await http.PostAsync($"api/matches/{id}/leave", null));
    public Task<MatchState> StartMatchAsync(int id) => PostAsync<MatchState>($"api/matches/{id}/start", null);
    public Task<MatchState> MatchGuessAsync(int id, string guess) => PostAsync<MatchState>($"api/matches/{id}/guess", new AnswerRequest(guess));
    public Task<MatchState> MatchGiveUpAsync(int id) => PostAsync<MatchState>($"api/matches/{id}/giveup", null);
    public Task<OnlineStats> GetOnlineStatsAsync() => GetAsync<OnlineStats>("api/online/stats");

    private record CreatedMatch(int Id);

    public Task<StatsOverview> GetStatsAsync() => GetAsync<StatsOverview>("api/stats");

    private async Task<T?> GetOrNullAsync<T>(string url) where T : class
    {
        var response = await http.GetAsync(url);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : null;
    }

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private async Task<T> PostAsync<T>(string url, object? body) =>
        await ReadAsync<T>(body is null ? await http.PostAsync(url, null) : await http.PostAsJsonAsync(url, body));

    /// <summary>Voor endpoints zonder antwoord: gooit de foutmelding als het misliep.</summary>
    private static async Task EnsureOkAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) await ReadAsync<object>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<T>())!;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new ApiException("Je bent niet (meer) aangemeld.");

        ErrorResponse? error = null;
        try { error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); } catch { /* geen JSON */ }
        throw new ApiException(error?.Message ?? "Er ging iets mis. Probeer opnieuw.");
    }
}
