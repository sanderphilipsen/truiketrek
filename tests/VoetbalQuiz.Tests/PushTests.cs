using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>Pushmeldingen: toestellen aan- en afmelden, en de daily-herinnering om 19u voor wie nog niet speelde.</summary>
public sealed class PushTests : IDisposable
{
    private readonly TestDb _t = new(); // zaterdag 17 oktober, 12:00 in België
    private readonly FakeSender _sender = new();
    private readonly PushService _push;
    private readonly string _me, _tom;

    public PushTests()
    {
        for (var i = 1; i <= 30; i++)
        {
            _t.Db.Players.Add(new Player
            {
                Name = $"Speler {i}",
                Status = PlayerStatus.Approved,
                Tags = ["halloween"], // genoeg voor een special
                Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")],
                Sources = ["https://example.org"],
            });
        }
        _me = _t.AddUser("sander");
        _tom = _t.AddUser("tom");
        _push = new PushService(_t.Db, _t.Clock, _sender, NullLogger<PushService>.Instance);
    }

    public void Dispose() => _t.Dispose();

    private static PushSubscribeRequest Device(string id) => new($"https://fcm.googleapis.com/fcm/send/{id}", $"p256-{id}", $"auth-{id}");

    /// <summary>Beantwoordt de eerste vraag van de daily van vandaag.</summary>
    private async Task PlayAsync(string userId)
    {
        var challenge = await _t.Quiz.GetOrCreateChallengeAsync(_t.Quiz.Today);
        var first = challenge.Questions.OrderBy(q => q.Number).First();
        await _t.Quiz.StartAsync(userId, first.Number);
        await _t.Quiz.GiveUpAsync(userId, first.Number);
    }

    /// <summary>Zet de klok vooruit naar het eerstvolgende moment met dit uur (Belgische tijd).</summary>
    private void At(int hour, int minute = 0)
    {
        var now = QuizDay.ToBrussels(_t.Clock.GetUtcNow().UtcDateTime);
        var target = now.Date.AddHours(hour).AddMinutes(minute);
        if (target <= now) target = target.AddDays(1);
        _t.Clock.Advance(target - now);
    }

    [Fact]
    public void De_herinnering_valt_tussen_19u_en_23u()
    {
        var day = new DateTime(2026, 10, 17);
        Assert.False(PushRules.DailyReminderDue(day.AddHours(18).AddMinutes(59)));
        Assert.True(PushRules.DailyReminderDue(day.AddHours(19)));
        Assert.True(PushRules.DailyReminderDue(day.AddHours(22).AddMinutes(59)));
        Assert.False(PushRules.DailyReminderDue(day.AddHours(23)));
    }

    [Fact]
    public void De_tekst_noemt_je_reeks_vanaf_twee_dagen()
    {
        Assert.DoesNotContain("op rij", PushRules.DailyReminderText(0));
        Assert.DoesNotContain("op rij", PushRules.DailyReminderText(1));
        Assert.Contains("5 dagen op rij", PushRules.DailyReminderText(5));
    }

    [Fact]
    public async Task Voor_19u_gaat_er_niets_weg()
    {
        await _push.SubscribeAsync(_me, Device("a"));
        At(18, 55);

        Assert.Equal(0, await _push.SendDailyRemindersAsync());
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Om_19u_enkel_wie_vandaag_nog_niet_speelde_en_een_toestel_heeft()
    {
        var uit = _t.AddUser("uit");
        _t.AddUser("zonder"); // de herinnering staat aan, maar zonder toestel is er niemand om ze naar te sturen
        await _push.SubscribeAsync(_me, Device("a"));
        await _push.SubscribeAsync(_tom, Device("b"));
        await _push.SubscribeAsync(uit, Device("c"));
        await _push.SetDailyReminderAsync(uit, false);
        await PlayAsync(_tom);
        At(19);

        Assert.Equal(1, await _push.SendDailyRemindersAsync());

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(Device("a").Endpoint, sent.Endpoint);
        Assert.Equal("daily", sent.Payload.Url);
        Assert.Equal(PushRules.DailyReminderTitle, sent.Payload.Title);
    }

    [Fact]
    public async Task Elk_toestel_krijgt_ze_maar_een_keer_per_dag()
    {
        await _push.SubscribeAsync(_me, Device("gsm"));
        await _push.SubscribeAsync(_me, Device("laptop"));
        At(19);

        Assert.Equal(1, await _push.SendDailyRemindersAsync());
        Assert.Equal(2, _sender.Sent.Count); // één speler, twee toestellen

        At(21);
        Assert.Equal(0, await _push.SendDailyRemindersAsync());
        Assert.Equal(2, _sender.Sent.Count);

        At(19); // de volgende dag opnieuw
        Assert.Equal(1, await _push.SendDailyRemindersAsync());
        Assert.Equal(4, _sender.Sent.Count);
    }

    [Fact]
    public async Task Een_special_spelen_telt_niet_als_de_daily_spelen()
    {
        await _push.SubscribeAsync(_me, Device("a"));
        var id = await _t.Admin.SaveSpecialAsync(
            new SpecialEdit(0, "halloween", "Halloween", "🎃", "", _t.Quiz.Today, _t.Quiz.Today, "halloween", []));
        await _t.Quiz.GetOrCreateSpecialChallengeAsync(id);
        await _t.Quiz.StartAsync(_me, 1, id);
        await _t.Quiz.GiveUpAsync(_me, 1, id);
        At(19);

        Assert.Equal(1, await _push.SendDailyRemindersAsync());
    }

    [Fact]
    public async Task De_tekst_noemt_je_reeks_tot_gisteren()
    {
        await _push.SubscribeAsync(_me, Device("a"));
        await PlayAsync(_me);
        At(12); // de volgende dag
        await PlayAsync(_me);
        At(12); // nog een dag later: twee dagen op rij, vandaag nog niet
        At(19);

        await _push.SendDailyRemindersAsync();

        Assert.Equal(PushRules.DailyReminderText(2), Assert.Single(_sender.Sent).Payload.Body);
    }

    [Fact]
    public async Task Een_verlopen_toestel_wordt_opgeruimd()
    {
        await _push.SubscribeAsync(_me, Device("weg"));
        await _push.SubscribeAsync(_me, Device("stuk"));
        _sender.Results[Device("weg").Endpoint] = PushResult.Gone;
        _sender.Results[Device("stuk").Endpoint] = PushResult.Failed;
        At(19);

        await _push.SendDailyRemindersAsync();

        // Gone: de pushdienst kent het toestel niet meer. Failed: misschien tijdelijk, dus blijven.
        Assert.Equal([Device("stuk").Endpoint], await _t.Db.PushSubscriptions.Select(p => p.Endpoint).ToListAsync());
    }

    [Fact]
    public async Task Zonder_sleutels_gebeurt_er_niets()
    {
        var push = new PushService(_t.Db, _t.Clock, new FakeSender { PublicKey = null }, NullLogger<PushService>.Instance);
        await push.SubscribeAsync(_me, Device("a"));
        At(19);

        Assert.Equal(0, await push.SendDailyRemindersAsync());
        Assert.Null((await push.GetConfigAsync(_me)).PublicKey);
    }

    [Fact]
    public async Task De_herinnering_staat_standaard_aan()
    {
        Assert.True((await _push.GetConfigAsync(_me)).DailyReminder);

        await _push.SetDailyReminderAsync(_me, false);

        Assert.False((await _push.GetConfigAsync(_me)).DailyReminder);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/x")]       // geen https
    [InlineData("https://example.org/push")]                   // geen pushdienst
    [InlineData("https://fcm.googleapis.com.evil.org/x")]      // lijkt erop, maar is het niet
    [InlineData("https://fcm.googleapis.com:8443/x")]          // andere poort
    [InlineData("https://localhost/x")]
    public async Task Enkel_de_pushdiensten_van_de_browsers(string endpoint)
    {
        await Assert.ThrowsAsync<QuizException>(() => _push.SubscribeAsync(_me, new PushSubscribeRequest(endpoint, "p", "a")));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x")]
    [InlineData("https://web.push.apple.com/x")]
    [InlineData("https://wns2-am3p.notify.windows.com/w/?token=x")]
    public async Task De_pushdiensten_van_de_browsers_mogen(string endpoint)
    {
        await _push.SubscribeAsync(_me, new PushSubscribeRequest(endpoint, "p", "a"));

        Assert.Equal(1, await _t.Db.PushSubscriptions.CountAsync());
    }

    [Fact]
    public async Task Hetzelfde_toestel_gaat_over_naar_wie_zich_aanmeldt()
    {
        await _push.SubscribeAsync(_me, Device("gedeeld"));
        await _push.SubscribeAsync(_tom, Device("gedeeld"));

        var device = await _t.Db.PushSubscriptions.SingleAsync();
        Assert.Equal(_tom, device.UserId);
    }

    [Fact]
    public async Task Afmelden_kan_enkel_je_eigen_toestel()
    {
        await _push.SubscribeAsync(_me, Device("a"));

        await _push.UnsubscribeAsync(_tom, Device("a").Endpoint);
        Assert.Equal(1, await _t.Db.PushSubscriptions.CountAsync());

        await _push.UnsubscribeAsync(_me, Device("a").Endpoint);
        Assert.Equal(0, await _t.Db.PushSubscriptions.CountAsync());
    }

    [Fact]
    public async Task Een_toestel_te_veel_verdringt_het_oudste()
    {
        for (var i = 0; i <= PushRules.MaxDevices; i++)
        {
            await _push.SubscribeAsync(_me, Device($"t{i}"));
            _t.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var endpoints = await _t.Db.PushSubscriptions.Select(p => p.Endpoint).ToListAsync();
        Assert.Equal(PushRules.MaxDevices, endpoints.Count);
        Assert.DoesNotContain(Device("t0").Endpoint, endpoints);
    }

    private sealed class FakeSender : IPushSender
    {
        public string? PublicKey { get; init; } = "publieke-sleutel";
        public List<(string Endpoint, PushPayload Payload)> Sent { get; } = [];
        public Dictionary<string, PushResult> Results { get; } = [];

        public Task<PushResult> SendAsync(PushSubscription device, PushPayload payload, CancellationToken cancellationToken = default)
        {
            Sent.Add((device.Endpoint, payload));
            return Task.FromResult(Results.GetValueOrDefault(device.Endpoint, PushResult.Sent));
        }
    }
}
