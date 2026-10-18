using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class AvatarTests
{
    [Fact]
    public void Custom_avatar_survives_encode_and_parse()
    {
        var original = new AvatarDef("", "", 3, HairStyle.Mohawk, 5, FacialHair.Goatee, 12, 1, 23,
            ShirtPattern.Hoops, Glasses.Sunglasses, 0, Earring.GoldHoop);

        var id = Avatars.Encode(original);
        Assert.True(Avatars.IsValid(id));

        var parsed = Avatars.Get(id);
        Assert.Equal(original with { Id = id, Name = "Eigen avatar" }, parsed);
    }

    [Theory]
    [InlineData("c1:9.0.0.0.0.0.7.0.0.-1")]        // huidskleur buiten het palet
    [InlineData("c1:0.0.0.0.0.0.100.0.0.-1")]      // nummer > 99
    [InlineData("c1:0.0.0.0.0.0.7.0.0")]           // te weinig onderdelen
    [InlineData("c1:0.0.0.0.0.0.7.0.0.-1\"/><script>")]
    [InlineData("onbekend")]
    public void Invalid_avatar_ids_are_rejected(string id) =>
        Assert.False(Avatars.IsValid(id));

    [Fact]
    public void Older_avatars_without_earring_still_work()
    {
        var old = Avatars.Get("c1:1.2.4.1.0.1.7.0.0.-1");
        Assert.Equal(Earring.None, old.Earring);
        Assert.Equal(7, old.Number);
    }

    [Fact]
    public void Presets_and_random_avatars_are_valid()
    {
        Assert.All(Avatars.All, a => Assert.True(Avatars.IsValid(a.Id)));
        var rng = new Random(42);
        for (var i = 0; i < 200; i++)
            Assert.True(Avatars.IsValid(Avatars.Encode(Avatars.Random(rng))));
    }

    [Fact]
    public void A_preset_can_be_tweaked_into_a_custom_avatar()
    {
        var tweaked = Avatars.Get("debruyne") with { Glasses = Glasses.Round };
        Assert.Equal(Glasses.Round, Avatars.Get(Avatars.Encode(tweaked)).Glasses);
    }
}
