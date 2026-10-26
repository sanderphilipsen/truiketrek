using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class SfeerKalenderTests
{
    [Theory]
    [InlineData(2026, 10, 17, null)]
    [InlineData(2026, 10, 18, "verjaardag")]
    [InlineData(2026, 10, 19, null)]
    [InlineData(2026, 10, 31, "halloween")]
    [InlineData(2027, 10, 31, "halloween")]
    [InlineData(2026, 10, 30, null)]
    [InlineData(2026, 11, 1, null)]
    [InlineData(2026, 12, 4, null)]
    [InlineData(2026, 12, 5, "sinterklaas")]
    [InlineData(2026, 12, 6, "sinterklaas")]
    [InlineData(2026, 12, 7, null)]
    [InlineData(2026, 12, 21, null)]
    [InlineData(2026, 12, 22, "kerst")]
    [InlineData(2026, 12, 25, "kerst")]
    [InlineData(2026, 12, 30, "kerst")]
    [InlineData(2026, 12, 31, "nieuwjaar")]
    [InlineData(2027, 1, 1, "nieuwjaar")]
    [InlineData(2027, 1, 2, "nieuwjaar")]
    [InlineData(2027, 1, 3, null)]
    public void Each_sfeer_has_its_own_days(int year, int month, int day, string? sfeer) =>
        Assert.Equal(sfeer, SfeerKalender.Op(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2029, 4, 1)]
    [InlineData(2030, 4, 21)]
    [InlineData(2031, 4, 13)]
    [InlineData(2032, 3, 28)]
    [InlineData(2033, 4, 17)]
    public void Easter_is_sunday_and_monday(int year, int month, int day)
    {
        var sunday = new DateOnly(year, month, day);
        Assert.Equal(sunday, SfeerKalender.PaasZondag(year));
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
        Assert.Equal("pasen", SfeerKalender.Op(sunday));
        Assert.Equal("pasen", SfeerKalender.Op(sunday.AddDays(1)));
        Assert.Null(SfeerKalender.Op(sunday.AddDays(-1)));
        Assert.Null(SfeerKalender.Op(sunday.AddDays(2)));
    }

    [Fact]
    public void Status_follows_the_belgian_quiz_day()
    {
        // 30/10 om 23.30 UTC is in België al 31/10 (wintertijd, UTC+1): dan is het Halloween.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 23, 30, 0, TimeSpan.Zero));
        var notice = new MaintenanceNotice(null, clock);
        Assert.Equal("halloween", notice.Status().Sfeer);

        clock.Advance(TimeSpan.FromHours(24)); // 1/11 in België
        Assert.Null(notice.Status().Sfeer);
    }
}
