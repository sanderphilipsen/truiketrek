using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Beheer van de spelersdatabase en de specials (enkel voor admins).</summary>
public class AdminService(QuizDbContext db, QuestionPicker picker, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- Spelers ----------

    public async Task<PlayerCounts> CountsAsync()
    {
        var byStatus = await db.Players.GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        int Count(PlayerStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        return new PlayerCounts(Count(PlayerStatus.Review), Count(PlayerStatus.Approved), Count(PlayerStatus.Rejected));
    }

    public async Task<List<PlayerListItem>> ListAsync(PlayerStatus? status, string? search, string? tag)
    {
        var usage = await UsageAsync();
        var lastUsed = await LastUsedAsync();
        var players = await db.Players
            .Where(p => status == null || p.Status == status)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var term = AnswerMatcher.Normalize(search);
        return players
            .Where(p => term.Length == 0 || AnswerMatcher.Normalize(p.Name).Contains(term))
            .Where(p => string.IsNullOrEmpty(tag) || p.Tags.Contains(tag))
            .Select(p => new PlayerListItem(p.Id, p.Name, p.Status, p.Country, p.Competition, p.Era, p.Difficulty,
                p.Tags, p.Sources.Count, p.CheckedAt, usage.GetValueOrDefault(p.Id),
                lastUsed.TryGetValue(p.Id, out var last) ? last : null, p.ReviewNote))
            .ToList();
    }

    public async Task<PlayerEdit> GetAsync(int id)
    {
        var p = await db.Players.FindAsync(id) ?? throw new QuizException("Speler niet gevonden.");
        var usage = await UsageAsync();
        return new PlayerEdit(p.Id, p.Name, p.Answers, p.Country, p.Competition, p.Era, p.Difficulty, p.Hints,
            p.Sources, p.Tags, p.Status, p.ReviewNote, p.CheckedAt, p.UpdatedBy, usage.GetValueOrDefault(p.Id));
    }

    /// <summary>Nieuwe speler (id 0) of een bestaande bijwerken. Goedkeuren kan enkel met bronnen.</summary>
    public async Task<int> SaveAsync(PlayerEdit edit, string adminName)
    {
        var clean = Clean(edit);
        var problems = PlayerValidation.Problems(clean, clean.Status == PlayerStatus.Approved);
        if (problems.Count > 0) throw new QuizException(string.Join(" ", problems));

        if (await db.Players.AnyAsync(p => p.Name == clean.Name && p.Id != clean.Id))
            throw new QuizException("Er bestaat al een speler met deze naam.");

        var player = clean.Id == 0
            ? db.Players.Add(new Player { Name = clean.Name }).Entity
            : await db.Players.FindAsync(clean.Id) ?? throw new QuizException("Speler niet gevonden.");

        player.Name = clean.Name;
        player.Answers = clean.Answers;
        player.Country = clean.Country;
        player.Competition = clean.Competition;
        player.Era = clean.Era is QuestionRules.ClassicEra ? QuestionRules.ClassicEra : "modern";
        player.Difficulty = Difficulty.Clamp(clean.Difficulty);
        player.Hints = clean.Hints;
        player.Sources = clean.Sources;
        player.Tags = clean.Tags.Where(t => PlayerTags.All.Any(x => x.Key == t)).Distinct().ToList();
        player.Status = clean.Status;
        player.ReviewNote = clean.Status == PlayerStatus.Approved ? null : clean.ReviewNote;
        if (clean.Status == PlayerStatus.Approved) player.CheckedAt = DateOnly.FromDateTime(Now);
        player.EditedInAdmin = true;
        player.UpdatedBy = adminName;

        await db.SaveChangesAsync();
        return player.Id;
    }

    private static PlayerEdit Clean(PlayerEdit e) => e with
    {
        Name = e.Name.Trim(),
        Answers = e.Answers.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList(),
        Hints = e.Hints.Select(h => h.Trim()).ToList(),
        Sources = e.Sources.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList(),
        Country = e.Country.Trim(),
        Competition = e.Competition.Trim(),
        ReviewNote = string.IsNullOrWhiteSpace(e.ReviewNote) ? null : e.ReviewNote.Trim(),
    };

    private Task<Dictionary<int, DateOnly>> LastUsedAsync() =>
        db.DailyQuestions.Where(q => q.PlayerId != null)
            .GroupBy(q => q.PlayerId!.Value).Select(g => new { g.Key, Last = g.Max(q => q.DailyChallenge.Date) })
            .ToDictionaryAsync(x => x.Key, x => x.Last);

    private Task<Dictionary<int, int>> UsageAsync() =>
        db.DailyQuestions.Where(q => q.PlayerId != null)
            .GroupBy(q => q.PlayerId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    // ---------- Dashboard ----------

    public async Task<AdminDashboard> DashboardAsync(DateOnly today)
    {
        var feedback = await db.Feedback.GroupBy(f => f.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var specials = await SpecialsAsync();
        var approved = await db.Players.Where(p => p.Status == PlayerStatus.Approved).ToListAsync();
        var tagCounts = PlayerTags.All.ToDictionary(t => t.Key, t => approved.Count(p => p.Tags.Contains(t.Key)));

        // Hoeveel dagen kan de daily nog zonder dat een speler terugkomt?
        var used = (await LastUsedAsync()).Keys.ToHashSet();
        var neverUsed = approved.Count(p => !used.Contains(p.Id));

        return new AdminDashboard(
            await CountsAsync(),
            feedback.Where(f => f.Key == FeedbackStatus.Nieuw).Sum(f => f.Count),
            feedback.Where(f => f.Key is FeedbackStatus.Nieuw or FeedbackStatus.Bekeken or FeedbackStatus.Gepland).Sum(f => f.Count),
            specials.Where(s => s.StartDate <= today && today <= s.EndDate).ToList(),
            specials.Where(s => s.StartDate > today).OrderBy(s => s.StartDate).ToList(),
            tagCounts,
            neverUsed / GameRules.QuestionsPerDay);
    }

    // ---------- Weekplanning ----------

    public async Task<ScheduleOverview> ScheduleAsync()
    {
        var stored = await db.DailySchedule.ToDictionaryAsync(d => d.Day);
        var days = QuestionTypes.Week.Select(d => stored.TryGetValue(d, out var row)
            ? new ScheduleDay(d, row.Type, row.MixTypes.Count > 1 ? row.MixTypes : null)
            : new ScheduleDay(d, QuestionTypes.DefaultFor(d))).ToList();
        var disabled = await picker.DisabledTypesAsync();
        // Hoeveel vragen van elk type een week vraagt, zoals de dagen er werkelijk uitzien (mix, uitgeschakelde types).
        var weekly = days.SelectMany(d => QuestionTypes.Mix(QuestionTypes.ResolveDay(d.Planned, disabled)))
            .GroupBy(m => m.Key).ToDictionary(g => g.Key, g => g.Sum(m => m.Value));

        var items = await db.QuizItems.Select(i => new { i.Id, i.Type, i.Status }).ToListAsync();
        var usedItems = (await db.DailyQuestions.Where(q => q.QuizItemId != null).Select(q => q.QuizItemId!.Value).ToListAsync()).ToHashSet();
        var players = await db.Players.Select(p => new { p.Id, p.Status }).ToListAsync();
        var usedPlayers = (await db.DailyQuestions.Where(q => q.PlayerId != null).Select(q => q.PlayerId!.Value).ToListAsync()).ToHashSet();

        var stock = QuestionTypes.All.Select(info =>
        {
            // Kluts en counter putten uit dezelfde spelers als "Wie ben ik?".
            var (approved, review, neverUsed) = info.FromPlayers
                ? (players.Count(p => p.Status == PlayerStatus.Approved), players.Count(p => p.Status == PlayerStatus.Review),
                   players.Count(p => p.Status == PlayerStatus.Approved && !usedPlayers.Contains(p.Id)))
                : (items.Count(i => i.Type == info.Type && i.Status == PlayerStatus.Approved),
                   items.Count(i => i.Type == info.Type && i.Status == PlayerStatus.Review),
                   items.Count(i => i.Type == info.Type && i.Status == PlayerStatus.Approved && !usedItems.Contains(i.Id)));
            var perWeek = weekly.GetValueOrDefault(info.Type) * (info.Type == QuestionType.Counter ? QuestionRules.CounterPlayers : 1);
            return new TypeStock(info.Type, approved, review, neverUsed, perWeek == 0 ? 0 : neverUsed / perWeek, perWeek);
        }).ToList();

        return new ScheduleOverview(days, stock, [.. disabled.Order()]);
    }

    public async Task SaveScheduleAsync(List<ScheduleDay> days)
    {
        var stored = await db.DailySchedule.ToListAsync();
        foreach (var day in days)
        {
            var row = stored.FirstOrDefault(d => d.Day == day.Day);
            var mix = day.IsMix ? day.Mix!.Distinct().Order().ToList() : [];
            if (row is null) db.DailySchedule.Add(row = new DailyScheduleDay { Day = day.Day });
            row.Type = mix.Count > 0 ? mix[0] : day.Type;
            row.MixTypes = mix;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Zet een vraagtype aan of uit voor de dailies en specials. Er moet altijd minstens één type aan staan.</summary>
    public async Task SetTypeEnabledAsync(QuestionType type, bool enabled)
    {
        var row = await db.DisabledQuestionTypes.SingleOrDefaultAsync(d => d.Type == type);
        if (enabled)
        {
            if (row is not null) db.DisabledQuestionTypes.Remove(row);
        }
        else if (row is null)
        {
            if (await db.DisabledQuestionTypes.CountAsync() >= QuestionTypes.All.Count - 1)
                throw new QuizException("Er moet minstens één vraagtype aan staan.");
            db.DisabledQuestionTypes.Add(new DisabledQuestionType { Type = type });
        }
        await db.SaveChangesAsync();
    }

    // ---------- Vragenbank ----------

    public async Task<List<QuizItemListItem>> QuizItemsAsync(QuestionType? type, PlayerStatus? status, string? search)
    {
        var usage = await db.DailyQuestions.Where(q => q.QuizItemId != null)
            .GroupBy(q => q.QuizItemId!.Value)
            .Select(g => new { g.Key, Count = g.Count(), Last = g.Max(q => q.DailyChallenge.Date) })
            .ToDictionaryAsync(x => x.Key);
        var items = await db.QuizItems
            .Where(i => (type == null || i.Type == type) && (status == null || i.Status == status))
            .ToListAsync();

        var term = AnswerMatcher.Normalize(search);
        return items.Select(i =>
            {
                var (label, answer) = QuestionEngine.Describe(i.Type, i.Data);
                var u = usage.GetValueOrDefault(i.Id);
                return new QuizItemListItem(i.Id, i.Type, label, answer, i.Status, i.Difficulty, i.Tags, i.Sources.Count,
                    i.CheckedAt, u?.Count ?? 0, u?.Last, i.ReviewNote);
            })
            .Where(i => term.Length == 0 || AnswerMatcher.Normalize(i.Label + " " + i.Answer).Contains(term))
            .OrderBy(i => i.Type).ThenBy(i => i.Label)
            .ToList();
    }

    public async Task<QuizItemEdit> QuizItemAsync(int id)
    {
        var i = await db.QuizItems.FindAsync(id) ?? throw new QuizException("Vraag niet gevonden.");
        var used = await db.DailyQuestions.CountAsync(q => q.QuizItemId == id);
        return new QuizItemEdit(i.Id, i.Type, i.Data, i.Difficulty, i.Sources, i.Tags, i.Status, i.ReviewNote, i.CheckedAt, i.UpdatedBy, used);
    }

    public async Task<int> SaveQuizItemAsync(QuizItemEdit edit, string adminName)
    {
        if (QuestionTypes.Get(edit.Type).FromPlayers)
            throw new QuizException($"Vragen van het type '{QuestionTypes.Get(edit.Type).Name}' komen uit de spelers: die beheer je bij Spelers.");
        var problems = QuestionEngine.Problems(edit.Type, edit.Data);
        var sources = edit.Sources.Select(s => s.Trim()).Where(s => Uri.TryCreate(s, UriKind.Absolute, out _)).Distinct().ToList();
        if (edit.Status == PlayerStatus.Approved && sources.Count == 0) problems.Add("Voeg minstens één bron (URL) toe voor je goedkeurt.");
        if (problems.Count > 0) throw new QuizException(string.Join(" ", problems));

        var key = QuestionEngine.KeyFor(edit.Type, edit.Data);
        if (await db.QuizItems.AnyAsync(i => i.Type == edit.Type && i.Key == key && i.Id != edit.Id))
            throw new QuizException("Deze vraag bestaat al.");

        var item = edit.Id == 0
            ? db.QuizItems.Add(new QuizItem { Type = edit.Type, Key = key, Data = edit.Data }).Entity
            : await db.QuizItems.FindAsync(edit.Id) ?? throw new QuizException("Vraag niet gevonden.");

        item.Key = key;
        item.Data = edit.Data;
        item.Difficulty = Difficulty.Clamp(edit.Difficulty);
        item.Sources = sources;
        item.Tags = edit.Tags.Where(t => PlayerTags.All.Any(x => x.Key == t)).Distinct().ToList();
        item.Status = edit.Status;
        item.ReviewNote = edit.Status == PlayerStatus.Approved ? null : edit.ReviewNote;
        if (edit.Status == PlayerStatus.Approved) item.CheckedAt = DateOnly.FromDateTime(Now);
        item.EditedInAdmin = true;
        item.UpdatedBy = adminName;
        await db.SaveChangesAsync();
        return item.Id;
    }

    // ---------- Specials ----------

    public async Task<List<SpecialAdminItem>> SpecialsAsync()
    {
        var specials = await db.SpecialEvents.OrderByDescending(s => s.StartDate).ToListAsync();
        var started = (await db.DailyChallenges.Where(c => c.SpecialEventId != null)
            .Select(c => c.SpecialEventId!.Value).ToListAsync()).ToHashSet();
        var participants = await db.Attempts
            .Where(a => a.DailyQuestion.DailyChallenge.SpecialEventId != null)
            .GroupBy(a => a.DailyQuestion.DailyChallenge.SpecialEventId!.Value)
            .Select(g => new { SpecialId = g.Key, Count = g.Select(a => a.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.SpecialId, x => x.Count);

        var result = new List<SpecialAdminItem>();
        foreach (var s in specials)
        {
            var pool = (await picker.SpecialPoolAsync(s)).Count;
            result.Add(new SpecialAdminItem(s.Id, s.Theme, s.Name, s.Icon, s.StartDate, s.EndDate, s.Tag, pool,
                started.Contains(s.Id), participants.GetValueOrDefault(s.Id), s.Types));
        }
        return result;
    }

    public async Task<SpecialEdit> GetSpecialAsync(int id)
    {
        var s = await db.SpecialEvents.FindAsync(id) ?? throw new QuizException("Special niet gevonden.");
        return new SpecialEdit(s.Id, s.Theme, s.Name, s.Icon, s.Description, s.StartDate, s.EndDate, s.Tag, s.PlayerIds, s.Types);
    }

    public async Task<int> SaveSpecialAsync(SpecialEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Name)) throw new QuizException("Geef de special een naam.");
        if (edit.EndDate < edit.StartDate) throw new QuizException("De einddatum ligt voor de startdatum.");
        if (PlayerTags.Split(edit.Tag).Any(tag => PlayerTags.All.All(t => t.Key != tag))) throw new QuizException("Onbekend thema.");
        edit = edit with { Tag = PlayerTags.Join(PlayerTags.Split(edit.Tag)) };

        var special = edit.Id == 0
            ? db.SpecialEvents.Add(new SpecialEvent { Theme = edit.Theme, Name = edit.Name, CreatedAt = Now }).Entity
            : await db.SpecialEvents.FindAsync(edit.Id) ?? throw new QuizException("Special niet gevonden.");

        var locked = special.Id != 0 && await db.DailyChallenges.AnyAsync(c => c.SpecialEventId == special.Id);
        var types = (edit.Types ?? []).Distinct().ToList();
        if (locked && (special.Tag != edit.Tag || !special.PlayerIds.SequenceEqual(edit.PlayerIds) || special.StartDate != edit.StartDate
                       || !special.Types.SequenceEqual(types)))
            throw new QuizException("Deze special wordt al gespeeld: enkel de naam, beschrijving, icoon en einddatum kunnen nog wijzigen.");

        special.Theme = edit.Theme;
        special.Name = edit.Name.Trim();
        special.Icon = string.IsNullOrWhiteSpace(edit.Icon) ? "✨" : edit.Icon.Trim()[..Math.Min(edit.Icon.Trim().Length, 8)];
        special.Description = edit.Description?.Trim() ?? "";
        special.StartDate = edit.StartDate;
        special.EndDate = edit.EndDate;
        special.Tag = edit.Tag;
        special.PlayerIds = edit.PlayerIds.Distinct().ToList();
        special.Types = types;

        var shortages = await picker.SpecialShortagesAsync(special);
        if (shortages.Count > 0) throw new QuizException("Niet genoeg vragen: " + string.Join(" ", shortages));

        await db.SaveChangesAsync();
        return special.Id;
    }

    public async Task DeleteSpecialAsync(int id)
    {
        if (await db.DailyChallenges.AnyAsync(c => c.SpecialEventId == id))
            throw new QuizException("Deze special is al gespeeld en kan niet meer verwijderd worden.");
        var special = await db.SpecialEvents.FindAsync(id);
        if (special is not null)
        {
            db.SpecialEvents.Remove(special);
            await db.SaveChangesAsync();
        }
    }
}
