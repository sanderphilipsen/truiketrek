using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>De afrekening van een online vraag (<see cref="MatchScoring"/>), zonder database: enkel het aggregaat.</summary>
public class MatchScoringTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    private static Match NewMatch(params string[] users) => new()
    {
        Name = "test",
        HostUserId = users[0],
        Players = [.. users.Select(u => new MatchPlayer { UserId = u, JoinedAt = T0 })],
    };

    private static MatchQuestion Snelvuur(Match match, int correct = 1)
    {
        var data = new SnelvuurData("Vraag?", ["A", "B", "C", "D"], correct);
        var q = new MatchQuestion
        {
            Number = 1, Type = QuestionType.Snelvuur, StartsAt = T0,
            QuizItem = new QuizItem { Type = QuestionType.Snelvuur, Key = "v", Data = QuestionEngine.Serialize(data) },
        };
        match.Questions.Add(q);
        return q;
    }

    private static MatchAnswer Answer(MatchQuestion q, string user, bool correct, int points, double at, int? choice = null)
    {
        var a = new MatchAnswer
        {
            UserId = user, Correct = correct, SpeedPoints = points, AnsweredAt = T0.AddSeconds(at), Choice = choice,
        };
        q.Answers.Add(a);
        return a;
    }

    private static MatchPlayer Player(Match m, string user) => m.Players.Single(p => p.UserId == user);

    [Fact]
    public void Positions_follow_the_order_of_correct_answers_and_go_to_the_score()
    {
        var m = NewMatch("a", "b", "c");
        var q = Snelvuur(m);
        var a = Answer(q, "a", true, 80, 5);
        var b = Answer(q, "b", true, 90, 3);
        var c = Answer(q, "c", false, 0, 2);

        MatchScoring.EndQuestion(m, q, T0.AddSeconds(20));

        Assert.Equal(T0.AddSeconds(20), q.EndedAt);
        Assert.Equal((1, MatchRules.BonusFor(1)), (b.Position, b.Bonus));
        Assert.Equal((2, MatchRules.BonusFor(2)), (a.Position, a.Bonus));
        Assert.Null(c.Position);
        Assert.Equal(90 + MatchRules.BonusFor(1), Player(m, "b").Score);
        Assert.Equal(80 + MatchRules.BonusFor(2), Player(m, "a").Score);
        Assert.Equal(0, Player(m, "c").Score);
    }

    [Fact]
    public void An_unfinished_counter_keeps_its_partial_points_and_a_leaver_gets_nothing()
    {
        var m = NewMatch("a", "b", "weg");
        var q = new MatchQuestion { Number = 1, Type = QuestionType.Counter, StartsAt = T0 };
        m.Questions.Add(q);
        var open = new MatchAnswer { UserId = "a", Found = [0, 2] };
        q.Answers.Add(open);
        Player(m, "weg").LeftAt = T0.AddSeconds(1);
        Player(m, "weg").Score = 120;
        var leaver = Answer(q, "weg", true, 100, 1);
        var stayer = Answer(q, "b", true, 60, 4);

        MatchScoring.EndQuestion(m, q, T0.AddSeconds(30));

        Assert.Equal(QuestionRules.CounterPoints(2), open.SpeedPoints);
        Assert.Equal((null, 0, 0), (leaver.Position, leaver.SpeedPoints, leaver.Bonus));
        Assert.Equal(0, Player(m, "weg").Score);
        Assert.Equal(1, stayer.Position); // de positie van de vertrekker gaat naar wie bleef
        Assert.Equal(60 + MatchRules.BonusFor(1), Player(m, "b").Score);
    }

    [Fact]
    public void A_red_card_comes_before_a_steal()
    {
        var m = NewMatch("doelwit", "rood", "dief");
        var q = Snelvuur(m);
        var target = Answer(q, "doelwit", true, 100, 2);
        target.RedCardFromUserId = "rood";
        target.StolenByUserId = "dief";

        MatchScoring.EndQuestion(m, q, T0.AddSeconds(20));

        var (speed, bonus) = (DerbyRules.AfterRedCard(100), DerbyRules.AfterRedCard(MatchRules.BonusFor(1)));
        var stolen = DerbyRules.Stolen(speed) + DerbyRules.Stolen(bonus);
        Assert.Equal(100 + MatchRules.BonusFor(1) - speed - bonus, Player(m, "rood").WeaponEffect);
        Assert.Equal(stolen, Player(m, "dief").Score);
        Assert.Equal(stolen, Player(m, "dief").WeaponEffect);
        Assert.Equal(speed + bonus - stolen, Player(m, "doelwit").Score);
    }

    [Fact]
    public void A_won_gamble_doubles_and_a_lost_gamble_never_goes_below_zero()
    {
        var m = NewMatch("winnaar", "verliezer");
        var q = Snelvuur(m);
        Answer(q, "winnaar", true, 70, 2).Gamble = true;
        Answer(q, "verliezer", false, 0, 3).Gamble = true;
        Player(m, "verliezer").Score = 20;

        MatchScoring.EndQuestion(m, q, T0.AddSeconds(20));

        var won = 70 + MatchRules.BonusFor(1);
        Assert.Equal(2 * won, Player(m, "winnaar").Score);
        Assert.Equal(won, Player(m, "winnaar").WeaponEffect);
        Assert.Equal(0, Player(m, "verliezer").Score);
        Assert.Equal(-Math.Min(DerbyRules.GamblePenalty, 20), Player(m, "verliezer").WeaponEffect);
    }

    [Fact]
    public void Trump_rejudges_every_choice_against_the_trump_answer()
    {
        var m = NewMatch("trump", "ander");
        var q = Snelvuur(m, correct: 1);
        q.TrumpUserId = "trump";
        var mine = Answer(q, "trump", false, 0, 4, choice: 2);
        var other = Answer(q, "ander", true, 85, 2, choice: 1);

        MatchScoring.EndQuestion(m, q, T0.AddSeconds(20));

        Assert.True(mine.Correct);
        Assert.Equal(QuestionRules.SpeedPoints(4, QuestionRules.SnelvuurSeconds), mine.SpeedPoints);
        Assert.Equal(1, mine.Position);
        Assert.False(other.Correct);
        Assert.Equal(0, other.SpeedPoints);
        Assert.Equal(85, Player(m, "trump").WeaponEffect);
        Assert.Equal("C", QuestionEngine.AnswerLabel(MatchScoring.Judged(q)));
        Assert.Equal("B", QuestionEngine.AnswerLabel(q));
    }

    [Fact]
    public void Finish_shares_ranks_on_a_tie_and_puts_leavers_last_with_zero()
    {
        var m = NewMatch("a", "b", "c", "vroeg", "laat");
        (Player(m, "a").Score, Player(m, "b").Score, Player(m, "c").Score) = (100, 100, 40);
        (Player(m, "vroeg").LeftAt, Player(m, "vroeg").Score) = (T0.AddSeconds(10), 300);
        (Player(m, "laat").LeftAt, Player(m, "laat").Score) = (T0.AddSeconds(50), 300);

        MatchScoring.Finish(m, T0.AddMinutes(5));

        Assert.Equal(MatchStatus.Finished, m.Status);
        Assert.Equal(T0.AddMinutes(5), m.FinishedAt);
        Assert.Equal([1, 1, 3, 5, 4], m.Players.Select(p => p.Rank!.Value));
        Assert.Equal(0, Player(m, "vroeg").Score);
        Assert.Equal(0, Player(m, "laat").Score);
    }
}
