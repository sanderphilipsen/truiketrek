using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>"Laatst online" voor het admin-paneel: wanneer er geschreven wordt, en hoe de ledenlijst eruitziet.</summary>
public sealed class LastSeenTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Schrijft_per_speler_hooguit_om_de_vijf_minuten()
    {
        var tracker = new LastSeenTracker(_db.Clock);
        var t0 = TestDb.DefaultNow.UtcDateTime;

        Assert.True(tracker.ShouldRecord("a", t0));
        Assert.False(tracker.ShouldRecord("a", t0.AddMinutes(4)));
        Assert.True(tracker.ShouldRecord("b", t0.AddMinutes(4))); // een andere speler staat los van de eerste
        Assert.True(tracker.ShouldRecord("a", t0.AddMinutes(5)));
        Assert.False(tracker.ShouldRecord("a", t0.AddMinutes(9)));
    }

    [Fact]
    public async Task Touch_zet_laatst_online_op_nu_en_wacht_daarna()
    {
        var id = _db.AddUser("sander");
        var tracker = new LastSeenTracker(_db.Clock);

        await tracker.TouchAsync(id, _db.Db);
        Assert.Equal(TestDb.DefaultNow.UtcDateTime, await LastSeenAsync(id));

        _db.Clock.Advance(TimeSpan.FromMinutes(3));
        await tracker.TouchAsync(id, _db.Db);
        Assert.Equal(TestDb.DefaultNow.UtcDateTime, await LastSeenAsync(id)); // nog niet opnieuw geschreven

        _db.Clock.Advance(TimeSpan.FromMinutes(3));
        await tracker.TouchAsync(id, _db.Db);
        Assert.Equal(TestDb.DefaultNow.UtcDateTime.AddMinutes(6), await LastSeenAsync(id));
    }

    [Fact]
    public async Task Ledenlijst_laatst_online_eerst_en_nooit_gezien_onderaan()
    {
        var now = TestDb.DefaultNow.UtcDateTime;
        var oud = _db.AddUser("oud");
        var vers = _db.AddUser("vers");
        _db.AddUser("nooit");
        await SetAsync(oud, now.AddDays(-10));
        await SetAsync(vers, now.AddMinutes(-2));

        var users = await _db.Admin.UsersAsync();

        Assert.Equal(["vers", "oud", "nooit"], users.Select(u => u.UserName));
        var counts = await _db.Admin.UserCountsAsync();
        Assert.Equal(new UserCounts(3, 1, 1), counts);
    }

    [Fact]
    public void Hoe_lang_geleden_in_gewone_woorden()
    {
        var now = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("zonet", DutchDates.Ago(now.AddSeconds(-30), now));
        Assert.Equal("12 min geleden", DutchDates.Ago(now.AddMinutes(-12), now));
        Assert.Equal("1 uur geleden", DutchDates.Ago(now.AddMinutes(-90), now));
        Assert.Equal("3 uur geleden", DutchDates.Ago(now.AddHours(-3), now));
        Assert.Equal("gisteren", DutchDates.Ago(now.AddHours(-30), now));
        Assert.Equal("5 dagen geleden", DutchDates.Ago(now.AddDays(-5), now));
        Assert.Equal("1 september 2026", DutchDates.Ago(now.AddDays(-46), now));
    }

    [Fact]
    public void Aftelling_naar_de_volgende_daily()
    {
        Assert.Equal("1 min", DutchDates.Countdown(TimeSpan.FromSeconds(20)));
        Assert.Equal("34 min", DutchDates.Countdown(TimeSpan.FromMinutes(33.5)));
        Assert.Equal("1u 00m", DutchDates.Countdown(TimeSpan.FromMinutes(60)));
        Assert.Equal("5u 12m", DutchDates.Countdown(new TimeSpan(5, 11, 30)));
    }

    private Task SetAsync(string id, DateTime at) =>
        _db.Db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, at));

    private async Task<DateTime?> LastSeenAsync(string id) =>
        await _db.Db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.LastSeenAt).SingleAsync();
}
