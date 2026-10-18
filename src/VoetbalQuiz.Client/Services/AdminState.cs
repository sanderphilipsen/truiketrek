using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Client.Services;

/// <summary>Onthoudt de filters van de spelerslijst, zodat je na het bewerken terugkeert naar dezelfde lijst.</summary>
public class AdminState
{
    public PlayerStatus? Status { get; set; }
    public string Search { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Sort { get; set; } = "name";

    /// <summary>Volgorde van de laatst getoonde lijst, voor "vorige/volgende" in de editor.</summary>
    public List<int> LastListIds { get; set; } = [];
}
