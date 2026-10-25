using VoetbalQuiz.Client.Shared;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// De iconen zijn namen van lijniconen in de client (Shared/Icon.razor). Een naam die daar niet staat, verschijnt als
/// losse tekst in plaats van een icoon: elke naam die de app gebruikt, moet er dus in staan.
/// </summary>
public sealed class IconTests : IDisposable
{
    private readonly TestDb _t = new();

    [Fact]
    public void VraagtypesEnDerbyWapensHebbenEenBestaandIcoon()
    {
        Assert.All(QuestionTypes.All, t => Assert.True(Icon.Exists(t.Icon), $"{t.Name}: {t.Icon}"));
        Assert.True(Icon.Exists(QuestionTypes.MixIcon));
        Assert.All(DerbyRules.All, w => Assert.True(Icon.Exists(w.Icon), $"{w.Name}: {w.Icon}"));
    }

    [Fact]
    public void SpecialsKiezenUitBestaandeIconenEnElkSjabloonStaatInDeKeuze()
    {
        Assert.All(SpecialIcons.All, i => Assert.True(Icon.Exists(i), i));
        Assert.All(SpecialTemplates.All, t => Assert.Contains(t.Icon, SpecialIcons.All));
        Assert.Equal("flag", SpecialIcons.Normalize(" flag "));
        Assert.Equal(SpecialIcons.Default, SpecialIcons.Normalize("🎄"));
        Assert.Equal(SpecialIcons.Default, SpecialIcons.Normalize(null));
    }

    [Fact]
    public async Task ElkeBadgeHeeftEenBestaandIcoon()
    {
        var me = _t.AddUser("icoontje");
        var badges = (await _t.Progress.GetProfileAsync(me, "icoontje")).Badges;
        Assert.NotEmpty(badges);
        Assert.All(badges, b => Assert.True(Icon.Exists(b.Icon), $"{b.Name}: {b.Icon}"));
    }

    public void Dispose() => _t.Dispose();
}
