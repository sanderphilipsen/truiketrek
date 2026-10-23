namespace VoetbalQuiz.Shared;

/// <summary>Waar je na het aanmelden heen gaat (de parameter "terug", bv. online/12). Gedeeld door client en server.</summary>
public static class ReturnPath
{
    /// <summary>
    /// Enkel een relatief pad binnen de app, nooit een andere site; anders "". Geen "/" vooraan ("//site" is een ander
    /// domein), geen ":" of "\", en enkel zichtbare ASCII-tekens: browsers schrappen tabs en regeleindes uit een URL,
    /// waardoor "\t/site" alsnog "//site" wordt, en een spatie vooraan valt bij het oplossen van een URI weg.
    /// </summary>
    public static string Safe(string? path) =>
        path is { Length: > 0 } && path[0] != '/' && path.All(c => c is > ' ' and < (char)127 and not ':' and not '\\')
            ? path : "";
}
