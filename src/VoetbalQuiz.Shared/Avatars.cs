namespace VoetbalQuiz.Shared;

public enum HairStyle { Bald, Buzz, Short, Slick, Curly, Afro, BigAfro, Long, Bun, Mohawk, Dreads }
public enum FacialHair { None, Stubble, Moustache, Goatee, Beard }
public enum ShirtPattern { Plain, Stripes, Hoops, Halves, Sash }
public enum Glasses { None, Round, Square, Sunglasses, Sport }
public enum Earring { None, GoldStud, SilverStud, Diamond, GoldHoop, SilverHoop }

/// <summary>
/// Een avatar, opgebouwd uit onderdelen. Kleuren zijn indexen in de vaste paletten hieronder,
/// zodat zelfgemaakte avatars enkel bekende waarden kunnen bevatten.
/// </summary>
public record AvatarDef(
    string Id, string Name,
    int Skin, HairStyle Style, int HairColor, FacialHair Facial,
    int Shirt, int Accent, int Number,
    ShirtPattern Pattern = ShirtPattern.Plain, Glasses Glasses = Glasses.None, int Headband = -1, Earring Earring = Earring.None)
{
    public string SkinHex => Avatars.Skins[Skin];
    public string HairHex => Avatars.HairColors[HairColor];
    public string ShirtHex => Avatars.Colors[Shirt];
    public string AccentHex => Avatars.Colors[Accent];
    public string? HeadbandHex => Headband >= 0 ? Avatars.Colors[Headband] : null;
}

public static class Avatars
{
    public static readonly string[] Skins = ["#f6d3b8", "#f1c7a5", "#d9a47a", "#b77b52", "#7a4a2c", "#5a3520"];

    public static readonly string[] HairColors =
        ["#1f1a17", "#3b2614", "#5b3a1f", "#8a5a2b", "#c4622d", "#e3c16f", "#9a9a9a", "#f2f2f2", "#2f6fd6", "#e04f9a"];

    public static readonly string[] Colors =
    [
        "#d81f26", // 0 rood
        "#f5c400", // 1 geel/goud
        "#ffffff", // 2 wit
        "#1f1a17", // 3 zwart
        "#2fbf71", // 4 keepersgroen
        "#75aadb", // 5 hemelsblauw
        "#c8102e", // 6 donkerrood
        "#046a38", // 7 donkergroen
        "#1d3b8b", // 8 marineblauw
        "#6cabdd", // 9 lichtblauw
        "#f36c21", // 10 oranje
        "#009c3b", // 11 groen
        "#1f4fa3", // 12 blauw
        "#0f5132", // 13 grasgroen
        "#5b2a86", // 14 paars
        "#7a1f3d", // 15 bordeaux
        "#f49ac2", // 16 roze
        "#9aa0a6", // 17 grijs
    ];

    // Huid: 0 bleek … 5 donker. Haar: 0 zwart, 2 bruin, 4 rood, 5 blond, 6 grijs.
    public static readonly List<AvatarDef> All =
    [
        // Rode Duivels
        new("debruyne", "Kevin De Bruyne", 1, HairStyle.Short, 4, FacialHair.Stubble, 0, 1, 7),
        new("hazard", "Eden Hazard", 1, HairStyle.Short, 2, FacialHair.Beard, 0, 1, 10),
        new("lukaku", "Romelu Lukaku", 4, HairStyle.Buzz, 0, FacialHair.None, 0, 1, 9),
        new("courtois", "Thibaut Courtois", 1, HairStyle.Slick, 2, FacialHair.None, 4, 3, 1),
        new("kompany", "Vincent Kompany", 3, HairStyle.Bald, 0, FacialHair.Beard, 0, 1, 4),
        new("witsel", "Axel Witsel", 3, HairStyle.BigAfro, 0, FacialHair.None, 0, 1, 6),
        new("fellaini", "Marouane Fellaini", 2, HairStyle.BigAfro, 0, FacialHair.Beard, 0, 1, 8),
        new("mertens", "Dries Mertens", 1, HairStyle.Short, 5, FacialHair.None, 0, 1, 14),
        new("vertonghen", "Jan Vertonghen", 1, HairStyle.Short, 2, FacialHair.Beard, 0, 1, 5),
        new("doku", "Jérémy Doku", 5, HairStyle.Short, 0, FacialHair.None, 0, 1, 11),
        new("tielemans", "Youri Tielemans", 1, HairStyle.Curly, 2, FacialHair.None, 0, 1, 8),
        new("scifo", "Enzo Scifo", 1, HairStyle.Curly, 0, FacialHair.None, 0, 2, 10),
        new("ceulemans", "Jan Ceulemans", 1, HairStyle.Long, 2, FacialHair.Moustache, 0, 2, 11),
        new("preudhomme", "Michel Preud'homme", 1, HairStyle.Short, 2, FacialHair.Moustache, 4, 3, 1),

        // Wereldsterren
        new("messi", "Lionel Messi", 1, HairStyle.Short, 2, FacialHair.Beard, 5, 2, 10, ShirtPattern.Stripes),
        new("ronaldo", "Cristiano Ronaldo", 2, HairStyle.Slick, 0, FacialHair.None, 6, 7, 7),
        new("mbappe", "Kylian Mbappé", 4, HairStyle.Bald, 0, FacialHair.None, 8, 2, 10),
        new("haaland", "Erling Haaland", 1, HairStyle.Bun, 5, FacialHair.None, 9, 2, 9),
        new("salah", "Mohamed Salah", 3, HairStyle.Afro, 0, FacialHair.Beard, 6, 2, 11),
        new("vandijk", "Virgil van Dijk", 3, HairStyle.Bun, 0, FacialHair.Beard, 10, 3, 4),
        new("zidane", "Zinédine Zidane", 1, HairStyle.Bald, 6, FacialHair.None, 8, 2, 10),
        new("ronaldinho", "Ronaldinho", 3, HairStyle.Long, 0, FacialHair.None, 1, 11, 10, Headband: 2),
        new("neymar", "Neymar Jr.", 2, HairStyle.Mohawk, 0, FacialHair.Goatee, 1, 11, 10),
        new("maradona", "Diego Maradona", 2, HairStyle.Curly, 0, FacialHair.None, 5, 2, 10, ShirtPattern.Stripes),
        new("cruijff", "Johan Cruijff", 1, HairStyle.Long, 2, FacialHair.None, 10, 3, 14),
        new("pirlo", "Andrea Pirlo", 1, HairStyle.Long, 2, FacialHair.Beard, 12, 2, 21),
        new("davids", "Edgar Davids", 4, HairStyle.Dreads, 0, FacialHair.None, 10, 3, 8, Glasses: Glasses.Sport),
    ];

    public static readonly AvatarDef Default = new("default", "Voetballer", 2, HairStyle.Short, 2, FacialHair.None, 13, 2, 0);

    private const string CustomPrefix = "c1:";

    /// <summary>De avatar bij een id: een voorgedefinieerde voetballer of een zelfgemaakte ("c1:…").</summary>
    public static AvatarDef Get(string? id) =>
        TryParseCustom(id, out var custom) ? custom : All.FirstOrDefault(a => a.Id == id) ?? Default;

    public static bool IsValid(string? id) => All.Any(a => a.Id == id) || TryParseCustom(id, out _);

    /// <summary>Zet een samengestelde avatar om naar een korte id, bv. "c1:1.2.4.1.0.1.7.0.0.-1.0" (oorbel als laatste).</summary>
    public static string Encode(AvatarDef a) =>
        CustomPrefix + string.Join('.', a.Skin, (int)a.Style, a.HairColor, (int)a.Facial, a.Shirt, a.Accent,
            a.Number, (int)a.Pattern, (int)a.Glasses, a.Headband, (int)a.Earring);

    public static bool TryParseCustom(string? id, out AvatarDef avatar)
    {
        avatar = Default;
        if (id is null || !id.StartsWith(CustomPrefix)) return false;

        var parts = id[CustomPrefix.Length..].Split('.');
        // 10 onderdelen = oudere avatar zonder oorbel; 11 = met oorbel.
        if (parts.Length is not (10 or 11)) return false;
        var v = new int[11];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out v[i])) return false;

        bool In(int value, int count) => value >= 0 && value < count;
        if (!In(v[0], Skins.Length) || !Enum.IsDefined((HairStyle)v[1]) || !In(v[2], HairColors.Length)
            || !Enum.IsDefined((FacialHair)v[3]) || !In(v[4], Colors.Length) || !In(v[5], Colors.Length)
            || !In(v[6], 100) || !Enum.IsDefined((ShirtPattern)v[7]) || !Enum.IsDefined((Glasses)v[8])
            || !(v[9] == -1 || In(v[9], Colors.Length)) || !Enum.IsDefined((Earring)v[10]))
            return false;

        avatar = new AvatarDef(id, "Eigen avatar", v[0], (HairStyle)v[1], v[2], (FacialHair)v[3], v[4], v[5], v[6],
            (ShirtPattern)v[7], (Glasses)v[8], v[9], (Earring)v[10]);
        return true;
    }

    public static AvatarDef Random(Random rng) => new("", "Eigen avatar",
        rng.Next(Skins.Length), (HairStyle)rng.Next(Enum.GetValues<HairStyle>().Length), rng.Next(HairColors.Length - 2),
        (FacialHair)rng.Next(Enum.GetValues<FacialHair>().Length), rng.Next(Colors.Length), rng.Next(Colors.Length),
        rng.Next(1, 100), (ShirtPattern)rng.Next(Enum.GetValues<ShirtPattern>().Length),
        rng.Next(4) == 0 ? (Glasses)rng.Next(1, Enum.GetValues<Glasses>().Length) : Glasses.None,
        rng.Next(5) == 0 ? rng.Next(Colors.Length) : -1,
        rng.Next(4) == 0 ? (Earring)rng.Next(1, Enum.GetValues<Earring>().Length) : Earring.None);
}
