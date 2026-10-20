using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Wat er in een vraag zit (spelers, counter-spelers), op één plek voor de daily en specials (<see cref="DailyQuestion"/>)
/// en online (<see cref="MatchQuestion"/>). Een counter bewaart zijn spelers als lijst van ids zonder foreign key;
/// de spelers zelf (<c>CounterPlayers</c>) moeten na het laden apart ingeladen worden.
/// </summary>
public static class QuestionContent
{
    /// <summary>Alle spelers die in deze vraag zitten: de speler van "Wie ben ik?"/kluts en de spelers van een counter.</summary>
    public static IEnumerable<int> PlayersOf(DailyQuestion q) => PlayersOf(q.PlayerId, q.CounterPlayerIds);

    /// <inheritdoc cref="PlayersOf(DailyQuestion)"/>
    public static IEnumerable<int> PlayersOf(MatchQuestion q) => PlayersOf(q.PlayerId, q.CounterPlayerIds);

    private static IEnumerable<int> PlayersOf(int? playerId, List<int> counterPlayerIds) =>
        playerId is { } p ? [p, .. counterPlayerIds] : counterPlayerIds;

    /// <summary>Laadt de counter-spelers van al deze vragen in één query.</summary>
    public static Task LoadCounterPlayersAsync(this QuizDbContext db, IEnumerable<DailyQuestion> questions) =>
        LoadAsync(db, questions.ToList(), q => q.CounterPlayerIds, (q, players) => q.CounterPlayers = players);

    /// <inheritdoc cref="LoadCounterPlayersAsync(QuizDbContext, IEnumerable{DailyQuestion})"/>
    public static Task LoadCounterPlayersAsync(this QuizDbContext db, IEnumerable<MatchQuestion> questions) =>
        LoadAsync(db, questions.ToList(), q => q.CounterPlayerIds, (q, players) => q.CounterPlayers = players);

    private static async Task LoadAsync<T>(QuizDbContext db, List<T> questions, Func<T, List<int>> ids, Action<T, List<Player>> set)
    {
        var all = questions.SelectMany(ids).Distinct().ToList();
        if (all.Count == 0) return;
        var players = await db.Players.Where(p => all.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var q in questions)
            set(q, ids(q).Where(players.ContainsKey).Select(id => players[id]).ToList());
    }
}
