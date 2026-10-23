using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Derby: een wapen inzetten (controleren, de plaats op de vraag claimen, het effect klaarzetten, een wissel) en wat
/// de client van de derby te zien krijgt. Werkt op een geladen <see cref="Match"/>; laden, bewaren en de toestand
/// teruggeven doet <see cref="MatchService"/>. Wat een wapen kost of oplevert bij het afsluiten van een vraag, rekent
/// <see cref="MatchScoring"/> af; de getallen staan in <see cref="DerbyRules"/>.
/// </summary>
public class DerbyService(QuizDbContext db, TimeProvider clock, QuestionPicker picker)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Zet je wapen in. Tijdens een vraag heeft het effect op die vraag; tijdens het aftellen of de tussenstand
    /// wordt het klaargezet voor de vraag die komt. De plaats op de vraag en het wapen worden meteen in de database
    /// geclaimd; de rest van de wijzigingen staat op <paramref name="match"/> en wordt door de aanroeper bewaard.
    /// </summary>
    public async Task UseWeaponAsync(Match match, string userId, string? target)
    {
        if (!match.Derby) throw new QuizException("Dit is geen derby.");
        var me = match.Players.SingleOrDefault(p => p.UserId == userId && p.LeftAt == null)
            ?? throw new QuizException("Je speelt niet mee in dit partijtje.");
        if (me.Weapon is not { } weapon) throw new QuizException("Je hebt geen wapen.");

        var targetPlayer = DerbyRules.NeedsTarget(weapon)
            ? match.Players.SingleOrDefault(p => p.User.UserName == target && p.LeftAt == null && p.UserId != userId)
                ?? throw new QuizException("Kies eerst een tegenstander.")
            : null;
        var (question, elapsed) = WeaponTarget(match);
        if (WhyNot(match, me, question, elapsed, targetPlayer) is { } why) throw new QuizException(why);
        await ClaimAsync(me, question!, weapon);

        switch (weapon)
        {
            case DerbyWeapon.RodeKaart:
                AnswerOf(question!, targetPlayer!.UserId).RedCardFromUserId = userId;
                me.WeaponTargetUserId = targetPlayer.UserId;
                break;
            case DerbyWeapon.PropereHanden:
                AnswerOf(question!, targetPlayer!.UserId).StolenByUserId = userId;
                me.WeaponTargetUserId = targetPlayer.UserId;
                break;
            case DerbyWeapon.Gokchinees:
                AnswerOf(question!, userId).Gamble = true;
                break;
            case DerbyWeapon.Verlenging:
                AnswerOf(question!, userId).ExtraSeconds = DerbyRules.ExtensionSeconds;
                break;
            case DerbyWeapon.BengaalsVuur:
                question!.FlareUserId = userId;
                question.FlareAt = Now;
                break;
            case DerbyWeapon.Trump:
                question!.TrumpUserId = userId;
                break;
            case DerbyWeapon.Wissel:
                await SwapAsync(match, question!);
                question!.SwapUserId = userId;
                break;
        }
        me.WeaponUsedAt = Now;
        me.WeaponQuestion = question!.Number;
    }

    /// <summary>De antwoordrij van een speler bij een vraag; bestaat ze nog niet, dan wordt ze aangemaakt.</summary>
    private MatchAnswer AnswerOf(MatchQuestion question, string userId)
    {
        var answer = question.Answers.SingleOrDefault(a => a.UserId == userId);
        if (answer is null)
        {
            // Een wapen kan klaargezet worden voor de vraag start; zodra je speelt wordt StartedAt de start van de vraag.
            answer = new MatchAnswer { UserId = userId, StartedAt = question.StartsAt ?? Now };
            question.Answers.Add(answer);
        }
        return answer;
    }

    /// <summary>
    /// Legt het wapen en (bij vuur, Trump en wissel) de plaats op de vraag vast in de database zelf,
    /// zodat twee verzoeken tegelijk (dubbel tikken, of twee spelers samen) nooit allebei slagen.
    /// </summary>
    private async Task ClaimAsync(MatchPlayer me, MatchQuestion question, DerbyWeapon weapon)
    {
        var questions = db.MatchQuestions.Where(q => q.Id == question.Id);
        var slot = weapon switch
        {
            DerbyWeapon.Trump => await questions.Where(q => q.TrumpUserId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.TrumpUserId, me.UserId)),
            DerbyWeapon.BengaalsVuur => await questions.Where(q => q.FlareUserId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.FlareUserId, me.UserId)),
            DerbyWeapon.Wissel => await questions.Where(q => q.SwapUserId == null && q.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.SwapUserId, me.UserId)),
            _ => 1,
        };
        if (slot == 0) throw new QuizException("Iemand was je net voor bij deze vraag.");

        var now = Now;
        var claimed = await db.MatchPlayers.Where(p => p.Id == me.Id && p.WeaponUsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.WeaponUsedAt, now));
        if (claimed > 0) return;

        // Wapen was al op: de plaats op de vraag terug vrijgeven.
        switch (weapon)
        {
            case DerbyWeapon.Trump:
                await questions.Where(q => q.TrumpUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.TrumpUserId, (string?)null));
                break;
            case DerbyWeapon.BengaalsVuur:
                await questions.Where(q => q.FlareUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.FlareUserId, (string?)null));
                break;
            case DerbyWeapon.Wissel:
                await questions.Where(q => q.SwapUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.SwapUserId, (string?)null));
                break;
        }
        throw new QuizException("Je hebt je wapen al ingezet.");
    }

    /// <summary>
    /// De vraag waarop een wapen nu effect zou hebben, en hoe lang die al loopt (negatief = start nog).
    /// Tijdens de tussenstand is dat de volgende vraag.
    /// </summary>
    private (MatchQuestion? Question, double Elapsed) WeaponTarget(Match match)
    {
        var current = MatchService.CurrentQuestion(match);
        return MatchService.PhaseOf(match, current, Now) switch
        {
            MatchPhase.Countdown or MatchPhase.Question when current is not null => (current, MatchScoring.Elapsed(current, Now)),
            MatchPhase.Reveal => (match.Questions.SingleOrDefault(q => q.Number == current!.Number + 1), -1),
            _ => (null, 0),
        };
    }

    /// <summary>Waarom je wapen nu niet kan (null = het kan). Zelfde controle voor de knop en voor het inzetten.</summary>
    private static string? WhyNot(Match match, MatchPlayer me, MatchQuestion? question, double elapsed, MatchPlayer? target)
    {
        if (me.Weapon is not { } weapon) return "Je hebt geen wapen.";
        if (me.WeaponUsedAt is not null) return "Je hebt je wapen al ingezet.";
        if (match.Status != MatchStatus.Running || question is null) return "Nu kan je geen wapen inzetten.";
        if (question.EndedAt is not null) return "Deze vraag is al afgelopen.";
        if (DerbyRules.Ceasefire(question.Number, match.Questions.Count)) return "Wapenstilstand: op de eerste en de laatste vraag zwijgen de wapens.";
        if (weapon == DerbyWeapon.Trump && !DerbyRules.TrumpWorksOn(question.Type)) return "Trump werkt enkel bij een vraag met antwoordopties of een jaartal.";
        if (!DerbyRules.CanUse(weapon, question.Type, question.Number, match.Questions.Count, elapsed, MatchScoring.TimeLimit(question)))
            return "Daarvoor is het nu te laat.";

        return weapon switch
        {
            DerbyWeapon.BengaalsVuur when question.FlareUserId is not null => "Er brandt al bengaals vuur bij deze vraag.",
            DerbyWeapon.Wissel when question.SwapUserId is not null => "Deze vraag werd al gewisseld.",
            DerbyWeapon.Trump when question.TrumpUserId is not null => "Er is al een Trump booster actief bij deze vraag.",
            DerbyWeapon.Trump when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Zet Trump in vóór je antwoordt.",
            // Anders is het een herkansing: fout antwoorden, het zien, en dan wisselen.
            DerbyWeapon.Wissel when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Wisselen kan enkel vóór je antwoordt.",
            DerbyWeapon.Verlenging when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Een verlenging vraag je vóór je antwoordt.",
            DerbyWeapon.RodeKaart when target is not null && Hit(question, target.UserId, weapon)
                => $"{target.User.UserName} heeft al rood bij deze vraag.",
            // Je mag de vraag eerst zien, maar gokken doe je vóór je antwoordt.
            DerbyWeapon.Gokchinees when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Gokken doe je vóór je antwoordt.",
            DerbyWeapon.PropereHanden when target is not null && Hit(question, target.UserId, weapon)
                => $"Bij {target.User.UserName} wordt al gestolen bij deze vraag.",
            DerbyWeapon.RodeKaart or DerbyWeapon.PropereHanden when target is null
                    && !match.Players.Any(p => p.UserId != me.UserId && p.LeftAt == null && !Hit(question, p.UserId, weapon))
                => "Er is niemand meer om te kiezen bij deze vraag.",
            _ => null,
        };
    }

    /// <summary>Heeft deze speler bij deze vraag al een rode kaart (of: is hij al omgekocht)?</summary>
    private static bool Hit(MatchQuestion question, string userId, DerbyWeapon weapon) =>
        question.Answers.Any(a => a.UserId == userId
            && (weapon == DerbyWeapon.PropereHanden ? a.StolenByUserId != null : a.RedCardFromUserId != null));

    /// <summary>
    /// Wissel: dezelfde nieuwe vraag (van hetzelfde type) voor iedereen, met een volle timer na het wisselbord.
    /// De antwoorden, het vuur en de Trump booster van de oude vraag vervallen; rode kaarten, omkoping en verlenging blijven staan.
    /// </summary>
    private async Task SwapAsync(Match match, MatchQuestion question)
    {
        var usedPlayers = match.Questions.SelectMany(QuestionContent.PlayersOf);
        var usedItems = match.Questions.Where(q => q.QuizItemId != null).Select(q => q.QuizItemId!.Value);
        var fresh = await picker.BuildReplacementAsync(question.Type, usedPlayers, usedItems)
            ?? throw new QuizException("Er is geen vraag meer om mee te wisselen.");

        question.Player = fresh.PlayerId is { } playerId ? await db.Players.FindAsync(playerId) : null;
        question.PlayerId = fresh.PlayerId;
        question.QuizItem = fresh.QuizItemId is { } itemId ? await db.QuizItems.FindAsync(itemId) : null;
        question.QuizItemId = fresh.QuizItemId;
        question.Blanks = fresh.Blanks;
        question.CounterPlayerIds = fresh.CounterPlayerIds;
        question.CounterPlayers = [];
        await db.LoadCounterPlayersAsync([question]);

        foreach (var answer in question.Answers.ToList())
        {
            // Rode kaarten, stelen, gokken en verlenging blijven gelden voor de nieuwe vraag.
            if (answer is { RedCardFromUserId: null, StolenByUserId: null, ExtraSeconds: 0, Gamble: false })
            {
                db.MatchAnswers.Remove(answer);
                question.Answers.Remove(answer);
                continue;
            }
            (answer.WrongGuesses, answer.LastGuess, answer.Correct, answer.GaveUp, answer.AnsweredAt) = (0, null, false, false, null);
            (answer.Points, answer.Guesses, answer.Choice, answer.Found, answer.Skipped, answer.Picks) = (0, 0, null, [], [], []);
        }
        (question.TrumpUserId, question.FlareUserId, question.FlareAt) = (null, null, null);
        question.StartsAt = Now.AddSeconds(DerbyRules.SwapCountdownSeconds);
    }

    /// <summary>Wat de client van de derby ziet: je wapen, of je het nu kan inzetten, de wapens bij deze vraag en op het einde het rapport.</summary>
    public DerbyState State(Match match, MatchQuestion? question, MatchPhase phase, string userId, Dictionary<string, ResultsService.UserCard> cards)
    {
        var me = match.Players.SingleOrDefault(p => p.UserId == userId);
        var (target, elapsed) = WeaponTarget(match);
        var canUse = me is { LeftAt: null } && WhyNot(match, me, target, elapsed, null) is null;

        string Name(string id) => match.Players.Single(p => p.UserId == id).User.UserName!;
        DerbyEvent Event(MatchQuestion q, DerbyWeapon weapon, string by, string? to = null)
        {
            var player = match.Players.Single(p => p.UserId == by);
            // Klaargezet voor een vraag die nog niet gestart is: negatief.
            var since = q.StartsAt is { } start
                ? (Now - new[] { player.WeaponUsedAt ?? Now, start }.Max()).TotalSeconds
                : -1;
            int? effect = q.EndedAt is null || weapon is DerbyWeapon.BengaalsVuur or DerbyWeapon.Wissel or DerbyWeapon.Verlenging
                ? null : player.WeaponEffect;
            return new DerbyEvent(weapon, Name(by), cards.GetValueOrDefault(by)?.AvatarId, by == userId,
                to is null ? null : Name(to), to == userId, since, effect);
        }
        List<DerbyEvent> EventsOf(MatchQuestion q)
        {
            var list = new List<DerbyEvent>();
            if (q.SwapUserId is { } swap) list.Add(Event(q, DerbyWeapon.Wissel, swap));
            if (q.FlareUserId is { } flare) list.Add(Event(q, DerbyWeapon.BengaalsVuur, flare));
            if (q.TrumpUserId is { } trump) list.Add(Event(q, DerbyWeapon.Trump, trump));
            foreach (var a in q.Answers.Where(a => a.RedCardFromUserId != null))
                list.Add(Event(q, DerbyWeapon.RodeKaart, a.RedCardFromUserId!, a.UserId));
            foreach (var a in q.Answers.Where(a => a.StolenByUserId != null))
                list.Add(Event(q, DerbyWeapon.PropereHanden, a.StolenByUserId!, a.UserId));
            foreach (var a in q.Answers.Where(a => a.Gamble))
                list.Add(Event(q, DerbyWeapon.Gokchinees, a.UserId));
            foreach (var a in q.Answers.Where(a => a.ExtraSeconds > 0))
                list.Add(Event(q, DerbyWeapon.Verlenging, a.UserId));
            // Iedereen ziet alles meteen, ook wat klaargezet is voor de volgende vraag (Since < 0).
            return list;
        }

        var events = new List<DerbyEvent>();
        if (question is not null && phase is MatchPhase.Countdown or MatchPhase.Question or MatchPhase.Reveal)
        {
            events.AddRange(EventsOf(question));
            if (phase == MatchPhase.Reveal && match.Questions.SingleOrDefault(q => q.Number == question.Number + 1) is { } next)
                events.AddRange(EventsOf(next));
        }

        string? official = null, actual = null;
        if (phase == MatchPhase.Reveal && question is not null && MatchScoring.TrumpChoice(question) is not null)
        {
            official = QuestionEngine.AnswerLabel(MatchScoring.Judged(question));
            actual = QuestionEngine.AnswerLabel(question);
        }

        var report = phase == MatchPhase.Finished
            ? match.Players.Where(p => p.Weapon != null).OrderBy(p => p.WeaponQuestion ?? int.MaxValue)
                .Select(p => new DerbyReportLine(p.User.UserName!, cards.GetValueOrDefault(p.UserId)?.AvatarId, p.Weapon!.Value,
                    p.WeaponUsedAt is null ? null : p.WeaponQuestion,
                    p.WeaponTargetUserId is null ? null : Name(p.WeaponTargetUserId), p.WeaponEffect, p.UserId == userId))
                .ToList()
            : null;

        return new DerbyState(
            me?.Weapon, me?.WeaponUsedAt is not null, canUse, target?.Number ?? 0,
            target is not null && DerbyRules.Ceasefire(target.Number, match.Questions.Count),
            events, official, actual, report);
    }
}
