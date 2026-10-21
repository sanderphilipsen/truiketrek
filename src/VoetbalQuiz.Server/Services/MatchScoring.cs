using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// De afrekening van een online partijtje: de klok van een vraag, hoe een antwoord beoordeeld wordt (ook met een Trump
/// booster), de punten, posities en derby-effecten als een vraag afloopt, en de eindstand. Pure logica op het
/// aggregaat <see cref="Match"/> (zoals <see cref="QuestionEngine"/> voor één vraag): geen database, de tijd komt als
/// parameter binnen. Het laden, bewaren en het verloop in de tijd doet <see cref="MatchService"/>.
/// </summary>
public static class MatchScoring
{
    // ---------- Klok van een vraag ----------

    public static double TimeLimit(MatchQuestion q) => QuestionEngine.TimeLimit(q);

    /// <summary>Hoe lang de vraag al loopt (negatief = ze start nog).</summary>
    public static double Elapsed(MatchQuestion q, DateTime now) => (now - q.StartsAt!.Value).TotalSeconds;

    /// <summary>De tijd van deze speler voor deze vraag: met een verlenging (derby) wat langer.</summary>
    public static double LimitFor(MatchQuestion q, MatchAnswer? a) => TimeLimit(q) + (a?.ExtraSeconds ?? 0);

    /// <summary>
    /// De tijd waarmee een antwoord gescoord wordt. In de verlenging telt het als op de valreep
    /// (de laagste punten van een juist antwoord), niet als te laat.
    /// </summary>
    public static double ScoringElapsed(MatchQuestion q, MatchAnswer a, DateTime now) =>
        a.ExtraSeconds > 0 ? Math.Min(Elapsed(q, now), TimeLimit(q) - 0.01) : Elapsed(q, now);

    // ---------- Beoordelen ----------

    /// <summary>
    /// De vraag zoals ze beoordeeld wordt. Met een Trump booster (en een antwoord van wie hem inzette)
    /// is diens keuze het juiste antwoord; anders de gewone vraag.
    /// </summary>
    public static IQuizQuestion Judged(MatchQuestion question) =>
        TrumpChoice(question) is { } choice ? QuestionEngine.WithAnswer(question, choice) : question;

    public static int? TrumpChoice(MatchQuestion question) =>
        question.TrumpUserId is { } trump && DerbyRules.TrumpWorksOn(question.Type)
            ? question.Answers.SingleOrDefault(a => a.UserId == trump && a.AnsweredAt != null)?.Choice
            : null;

    // ---------- Afrekenen ----------

    /// <summary>
    /// Sluit de vraag af. Wie het eerst juist was krijgt de grootste bonus; alle punten gaan naar de tussenstand
    /// (ook deelpunten: een jaartal dat er net naast zit, of een half ingevulde opstelling of counter).
    /// De volgorde is een spelregel: Trump, posities, vertrekkers op 0, rode kaarten, gokken, stelen, en dan de score.
    /// </summary>
    public static void EndQuestion(Match match, MatchQuestion question, DateTime now)
    {
        question.EndedAt = now;
        foreach (var answer in question.Answers.Where(a => a.AnsweredAt is null))
            answer.Points = QuestionEngine.PointsWhenStopped(question, answer);
        ApplyTrump(match, question);

        // Wie vertrok, telt niet meer mee: geen positie (die gaat naar wie bleef) en geen punten.
        var left = match.Players.Where(p => p.LeftAt != null).Select(p => p.UserId).ToHashSet();
        var position = 1;
        foreach (var answer in question.Answers.Where(a => a.Correct && !left.Contains(a.UserId)).OrderBy(a => a.AnsweredAt))
        {
            answer.Position = position;
            answer.Bonus = MatchRules.BonusFor(position);
            position++;
        }
        // Ook niets om te stelen bij een vertrekker.
        foreach (var answer in question.Answers.Where(a => left.Contains(a.UserId)))
            (answer.Points, answer.Bonus) = (0, 0);
        ApplyRedCards(match, question);
        var losers = ApplyGambles(match, question);
        var steals = ApplySteals(match, question);
        foreach (var answer in question.Answers)
            match.Players.Single(p => p.UserId == answer.UserId).Score += answer.Points + answer.Bonus;
        foreach (var (thief, amount) in steals)
            match.Players.Single(p => p.UserId == thief).Score += amount;
        foreach (var loser in losers)
        {
            // Een verloren gok kost punten van je totaal, maar je zakt nooit onder 0.
            var penalty = Math.Min(DerbyRules.GamblePenalty, loser.Score);
            loser.Score -= penalty;
            loser.WeaponEffect = -penalty;
        }
        foreach (var player in match.Players.Where(p => left.Contains(p.UserId)))
            player.Score = 0;
    }

    /// <summary>
    /// Eindstand: wie bleef, volgens de punten (gelijk = zelfde plaats). Daaronder wie vertrok, met 0 punten:
    /// wie het langst bleef het hoogst. Blijft er één speler over, dan wint die dus zowiezo.
    /// </summary>
    public static void Finish(Match match, DateTime now)
    {
        match.Status = MatchStatus.Finished;
        match.FinishedAt = now;
        var stayed = match.Players.Where(p => p.LeftAt == null).OrderByDescending(p => p.Score).ToList();
        for (var i = 0; i < stayed.Count; i++)
            stayed[i].Rank = i > 0 && stayed[i - 1].Score == stayed[i].Score ? stayed[i - 1].Rank : i + 1;
        var rank = stayed.Count;
        foreach (var p in match.Players.Where(p => p.LeftAt != null).OrderByDescending(p => p.LeftAt))
        {
            p.Rank = ++rank;
            p.Score = 0; // ook als het vertrek samenviel met het afsluiten van een vraag
        }
    }

    /// <summary>
    /// Trump: elke keuze opnieuw beoordelen tegen het "officiële" antwoord, aan hetzelfde tempo als toen.
    /// Wat tegenstanders erdoor verliezen, komt in het derby-rapport.
    /// </summary>
    private static void ApplyTrump(Match match, MatchQuestion question)
    {
        if (TrumpChoice(question) is null) return;
        var judged = Judged(question);
        var lost = 0;
        foreach (var answer in question.Answers.Where(a => a.Choice != null && a.AnsweredAt != null))
        {
            // Een keuzevraag zet enkel dezelfde keuze opnieuw: de poging zelf verandert niet.
            var outcome = QuestionEngine.Guess(judged, answer, answer.Choice!.Value.ToString(), (answer.AnsweredAt!.Value - question.StartsAt!.Value).TotalSeconds);
            if (answer.UserId != question.TrumpUserId) lost += Math.Max(0, answer.Points - outcome.Points);
            answer.Correct = outcome.Correct;
            answer.Points = outcome.Points;
        }
        match.Players.Single(p => p.UserId == question.TrumpUserId).WeaponEffect = lost;
    }

    /// <summary>Rode kaart: halve punten (snelheid en bonus) voor deze vraag.</summary>
    private static void ApplyRedCards(Match match, MatchQuestion question)
    {
        foreach (var answer in question.Answers.Where(a => a.RedCardFromUserId != null))
        {
            var before = answer.Points + answer.Bonus;
            answer.Points = DerbyRules.AfterRedCard(answer.Points);
            answer.Bonus = DerbyRules.AfterRedCard(answer.Bonus);
            match.Players.Single(p => p.UserId == answer.RedCardFromUserId).WeaponEffect = before - answer.Points - answer.Bonus;
        }
    }

    /// <summary>
    /// Gokchinees: punten gehaald (ook deelpunten) = dubbel, inclusief positiebonus.
    /// Niets gehaald = <see cref="DerbyRules.GamblePenalty"/> kwijt; dat geeft het terug, af te trekken na de score.
    /// </summary>
    private static List<MatchPlayer> ApplyGambles(Match match, MatchQuestion question)
    {
        var losers = new List<MatchPlayer>();
        foreach (var answer in question.Answers.Where(a => a.Gamble))
        {
            var player = match.Players.Single(p => p.UserId == answer.UserId);
            var points = answer.Points + answer.Bonus;
            if (points > 0)
            {
                (answer.Points, answer.Bonus) = (answer.Points * 2, answer.Bonus * 2);
                player.WeaponEffect = points;
            }
            else losers.Add(player);
        }
        return losers;
    }

    /// <summary>
    /// Propere handen: de helft van wat het doelwit bij deze vraag haalt (na rode kaart en gok), gaat naar de dief.
    /// Geeft per dief het bedrag terug, om bij de score op te tellen.
    /// </summary>
    private static Dictionary<string, int> ApplySteals(Match match, MatchQuestion question)
    {
        var gains = new Dictionary<string, int>();
        foreach (var answer in question.Answers.Where(a => a.StolenByUserId != null))
        {
            var (speed, bonus) = (DerbyRules.Stolen(answer.Points), DerbyRules.Stolen(answer.Bonus));
            (answer.Points, answer.Bonus) = (answer.Points - speed, answer.Bonus - bonus);
            gains[answer.StolenByUserId!] = gains.GetValueOrDefault(answer.StolenByUserId!) + speed + bonus;
            match.Players.Single(p => p.UserId == answer.StolenByUserId).WeaponEffect = speed + bonus;
        }
        return gains;
    }
}
