using System.Net;
using System.Net.Http.Json;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

public class ApiException(string message) : Exception(message);

/// <summary>Getypeerde toegang tot de server-API. De login-cookie gaat automatisch mee (zelfde domein).</summary>
public class ApiClient(HttpClient http)
{
    /// <summary>
    /// Wie er aangemeld is, of null bij 401. Andere fouten (bv. een 503 terwijl de server na een deploy
    /// opstart) gooien een uitzondering: dat zegt niets over de aanmelding.
    /// </summary>
    public async Task<UserInfo?> MeAsync()
    {
        var response = await http.GetAsync("api/account/me");
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserInfo>();
    }
    public Task<UserInfo> LoginAsync(LoginRequest req) => PostAsync<UserInfo>("api/account/login", req);
    public Task<UserInfo> RegisterAsync(RegisterRequest req) => PostAsync<UserInfo>("api/account/register", req);
    public Task LogoutAsync() => http.PostAsync("api/account/logout", null);
    public Task<LoginOptions> GetLoginOptionsAsync() => GetAsync<LoginOptions>("api/account/options");
    /// <summary>Na Google: de voorgestelde naam voor een nieuwe speler, of null als de Google-aanmelding verlopen is.</summary>
    public async Task<GoogleSignupInfo?> GetGoogleSignupAsync()
    {
        var response = await http.GetAsync("api/account/google/nieuw");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GoogleSignupInfo>();
    }
    public Task<UserInfo> GoogleSignupAsync(GoogleSignupRequest req) => PostAsync<UserInfo>("api/account/google/nieuw", req);

    // Daily, specials en training gebruiken dezelfde vragenflow; enkel het adres verschilt.
    public static string Quiz(int? specialId) => specialId is { } id ? $"api/specials/{id}" : "api/daily";
    public static string TrainingRun(int runId) => $"api/training/runs/{runId}";

    public Task<DailyOverview> GetDailyAsync(int? specialId = null) => GetOverviewAsync(Quiz(specialId));
    /// <summary>Je plaats van vandaag en die bij je vorige bezoek; de server onthoudt dit bezoek (enkel Home roept dit op).</summary>
    public Task<DailyRank> VisitDailyRankAsync() => PostAsync<DailyRank>("api/daily/rank", null);

    /// <summary>Overzicht van een reeks; <paramref name="quiz"/> is <see cref="Quiz"/> of <see cref="TrainingRun"/>.</summary>
    public Task<DailyOverview> GetOverviewAsync(string quiz) => GetAsync<DailyOverview>(quiz);
    public Task<QuestionState> StartAsync(string quiz, int number) =>
        PostAsync<QuestionState>($"{quiz}/questions/{number}/start", null);
    public Task<QuestionState> GetQuestionAsync(string quiz, int number) =>
        GetAsync<QuestionState>($"{quiz}/questions/{number}");
    public Task<QuestionState> GuessAsync(string quiz, int number, string guess) =>
        PostAsync<QuestionState>($"{quiz}/questions/{number}/guess", new AnswerRequest(guess));
    public Task<QuestionState> GiveUpAsync(string quiz, int number) =>
        PostAsync<QuestionState>($"{quiz}/questions/{number}/giveup", null);

    // Training: oude dailies opnieuw spelen (telt nergens mee)
    /// <summary>Zonder maand: de maand van de laatste daily die je kan trainen (bepaald door de server, in Belgische tijd).</summary>
    public Task<TrainingArchive> GetTrainingArchiveAsync(int? year = null, int? month = null) =>
        GetAsync<TrainingArchive>(year is { } y && month is { } m ? $"api/training/archive/{y}/{m}" : "api/training/archive");
    public Task<TrainingDayDetail> GetTrainingDayAsync(DateOnly date) => GetAsync<TrainingDayDetail>($"api/training/days/{date:yyyy-MM-dd}");
    public Task<TrainingRunInfo> StartTrainingRunAsync(DateOnly date) =>
        PostAsync<TrainingRunInfo>($"api/training/days/{date:yyyy-MM-dd}/runs", null);
    public Task<TrainingDayDetail> GetTrainingSpecialAsync(int specialId) => GetAsync<TrainingDayDetail>($"api/training/specials/{specialId}");
    public Task<TrainingRunInfo> StartTrainingSpecialRunAsync(int specialId) =>
        PostAsync<TrainingRunInfo>($"api/training/specials/{specialId}/runs", null);
    /// <summary>"Opnieuw spelen": een nieuwe run op dezelfde daily of special als deze run.</summary>
    public Task<TrainingRunInfo> StartTrainingAgainAsync(int runId) => PostAsync<TrainingRunInfo>($"{TrainingRun(runId)}/again", null);
    public Task<TrainingSummary> GetTrainingSummaryAsync(int runId) => GetAsync<TrainingSummary>($"{TrainingRun(runId)}/summary");

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

    public Task<ResetPasswordResult> ResetUserPasswordAsync(string userName) =>
        PostAsync<ResetPasswordResult>("api/admin/users/reset-password", new ResetPasswordRequest(userName));
    public Task<AdminDashboard> GetAdminDashboardAsync() => GetAsync<AdminDashboard>("api/admin/dashboard");
    public Task<List<AdminUserItem>> GetAdminUsersAsync() => GetAsync<List<AdminUserItem>>("api/admin/users");
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
    public Task<Leaderboard> GetSpecialLeaderboardAsync(int specialId) => GetAsync<Leaderboard>($"api/leaderboard/special/{specialId}");
    public Task<MonthRanking> GetMonthRankingAsync(int year, int month) => GetAsync<MonthRanking>($"api/leaderboard/month/{year}/{month}");
    public Task<OnlineLeaderboard> GetOnlineLeaderboardAsync() => GetAsync<OnlineLeaderboard>("api/leaderboard/online");

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
    public Task<MatchState> UseWeaponAsync(int id, string? target) => PostAsync<MatchState>($"api/matches/{id}/weapon", new UseWeaponRequest(target));
    public Task<OnlineStats> GetOnlineStatsAsync() => GetAsync<OnlineStats>("api/online/stats");

    private record CreatedMatch(int Id);

    public Task<StatsOverview> GetStatsAsync() => GetAsync<StatsOverview>("api/stats");
    public Task<GoldenBallOverview> GetGoldenBallAsync() => GetAsync<GoldenBallOverview>("api/gouden-bal");
    public Task<ServerStatus> GetStatusAsync() => GetAsync<ServerStatus>("api/status");

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
