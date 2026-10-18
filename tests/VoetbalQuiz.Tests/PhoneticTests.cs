using VoetbalQuiz.Server.Services;

namespace VoetbalQuiz.Tests;

public class PhoneticTests
{
    [Theory]
    [InlineData("kurtwa", "Thibaut Courtois", "Courtois")]
    [InlineData("kourtoi", "Thibaut Courtois", "Courtois")]
    [InlineData("lukakoe", "Romelu Lukaku", "Lukaku")]
    [InlineData("debruine", "Kevin De Bruyne", "De Bruyne")]
    [InlineData("de bruijne", "Kevin De Bruyne", "De Bruyne")]
    [InlineData("eden azar", "Eden Hazard", "Eden")]
    [InlineData("kastanje", "Timothy Castagne", "Castagne")]
    [InlineData("vertongen", "Jan Vertonghen", "Vertonghen")]
    [InlineData("mbape", "Kylian Mbappé", "Mbappé")]
    [InlineData("fellainie", "Marouane Fellaini", "Fellaini")]
    [InlineData("vinsent kompanie", "Vincent Kompany", "Kompany")]
    [InlineData("zinedine zidan", "Zinédine Zidane", "Zidane")]
    [InlineData("sjadli", "Nacer Chadli", "Chadli")]
    [InlineData("sjweinsteiger", "Bastian Schweinsteiger", "Schweinsteiger")]
    [InlineData("azar", "Eden Hazard", "Eden")]           // de achternaam telt altijd, ook al is ze dubbelzinnig
    public void Accepts_phonetically_correct_answers(string guess, string name, string alias) =>
        Assert.True(AnswerMatcher.IsMatch(guess, name, [alias]));

    [Theory]
    [InlineData("lukebakio", "Romelu Lukaku", "Lukaku")]
    [InlineData("mertens", "Jan Vertonghen", "Vertonghen")]
    [InlineData("witsel", "Marc Wilmots", "Wilmots")]
    [InlineData("de ketelaere", "Kevin De Bruyne", "De Bruyne")]
    [InlineData("kursa", "Thibaut Courtois", "Courtois")]
    public void Does_not_accept_other_players(string guess, string name, string alias) =>
        Assert.False(AnswerMatcher.IsMatch(guess, name, [alias]));
}
