using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class SfeerKalenderTests
{
    [Theory]
    [InlineData(2026, 10, 31, "halloween")]
    [InlineData(2027, 10, 31, "halloween")]
    [InlineData(2026, 10, 30, null)]
    [InlineData(2026, 11, 1, null)]
    public void Halloween_is_only_on_31_october(int year, int month, int day, string? sfeer) =>
        Assert.Equal(sfeer, SfeerKalender.Op(new DateOnly(year, month, day)));

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
