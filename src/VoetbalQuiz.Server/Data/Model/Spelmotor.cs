using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

/// <summary>
/// De inhoud van een vraag zoals <c>QuestionEngine</c> ze beoordeelt en toont, los van waar ze bewaard wordt:
/// een vraag van de daily, een special of de academie (<see cref="DailyQuestion"/>) of van een online partijtje
/// (<see cref="MatchQuestion"/>).
/// </summary>
public interface IQuizQuestion
{
    int Id { get; }
    int Number { get; }
    QuestionType Type { get; }
    Player? Player { get; }
    QuizItem? QuizItem { get; }
    List<int> Blanks { get; }
    List<Player> CounterPlayers { get; }

    /// <summary>Kaart: welk deel van de kaartdag (<see cref="QuestionRules.KaartClubsPart"/> of <see cref="QuestionRules.KaartStadiumsPart"/>); 0 bij de andere types.</summary>
    int Part { get; }
}

/// <summary>
/// De speltoestand van één vraag, gedeeld door <see cref="Attempt"/> (de echte daily), <see cref="TrainingAttempt"/> en
/// <see cref="MatchAnswer"/> (online), zodat <see cref="Services.QuestionEngine"/> en <see cref="Services.AttemptFlow"/>
/// ze allemaal spelen. Zelf geen tabel.
/// </summary>
public abstract class AttemptState
{
    public DateTime StartedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }

    public int WrongGuesses { get; set; }
    public string? LastGuess { get; set; }
    public bool Correct { get; set; }
    public int Points { get; set; }

    /// <summary>
    /// Alle pogingen (juist, fout of overgeslagen), begrensd door <see cref="GameRules.MaxGuessesPerQuestion"/>.
    /// Ook het concurrency token: van gokken die tegelijk binnenkomen telt er maar één.
    /// </summary>
    public int Guesses { get; set; }

    /// <summary>Meerkeuze/niet-bij: gekozen optie. Jaar: gekozen jaartal.</summary>
    public int? Choice { get; set; }

    /// <summary>
    /// Opstelling: welke plaatsen al juist ingevuld zijn. Counter: welke spelers (index) geraden zijn.
    /// Op volgorde: de gekozen volgorde, als indexen in de juiste volgorde.
    /// </summary>
    public List<int> Found { get; set; } = [];

    /// <summary>Counter: welke spelers (index) overgeslagen zijn. De Krant: de gegokte getallen, als paren (gat, gok).</summary>
    public List<int> Skipped { get; set; } = [];

    /// <summary>
    /// Kaart: wat de speler per beantwoorde speld koos, in de volgorde van de spelden ("Club Brugge|Cercle Brugge" of een
    /// stadion; leeg = overgeslagen). <see cref="Found"/> houdt per speld bij hoeveel daarvan juist was.
    /// </summary>
    public List<string> Picks { get; set; } = [];
}
