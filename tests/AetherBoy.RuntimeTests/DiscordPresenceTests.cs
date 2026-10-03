using System.Text;
using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class DiscordPresenceTests
{
    private const string Id = "123456789012345678";
    private static readonly DiscordPresenceOptions Enabled = new(true, false, Id);
    private static readonly DiscordGameActivity Game = new(DiscordGameSystem.GameBoyAdvance, "POKEMON FIRE", false);

    [TestMethod]
    public void ProductDefaultsEnableActivityButNotGameTitles()
    {
        var options = new DiscordPresenceOptions();
        Assert.IsTrue(options.Enabled); Assert.IsFalse(options.ShareGameTitle);
        Assert.AreEqual("1555427237908586616", options.ApplicationId);
        Assert.IsTrue(DiscordPresenceOptions.IsValidApplicationId(options.ApplicationId));
    }

    [TestMethod]
    public void DefaultsAndInvalidIdsNeverOpenIpc()
    {
        using var service = new DiscordPresenceService(_ => throw new AssertFailedException("Must not open Discord."), TimeProvider.System);
        service.Update(new(ApplicationId: ""), Game); Assert.AreEqual(DiscordPresenceStatus.NeedsApplicationId, service.Status);
        service.Update(new(false), Game); Assert.AreEqual(DiscordPresenceStatus.Disabled, service.Status);
        foreach (string id in new[] { "", "token-secret", "0", "123", "+123456789012345678", "18446744073709551616" })
        { service.Update(Enabled with { ApplicationId = id }, Game); Assert.AreEqual(DiscordPresenceStatus.NeedsApplicationId, service.Status); Assert.IsNull(service.Preview); }
    }

    [TestMethod]
    public void OptInSharesNoTitleUnlessSeparatelyAllowed()
    {
        var hidden = DiscordActivityPayload.Create(Enabled, Game)!;
        Assert.IsFalse(hidden.Details.Contains("POKEMON")); Assert.AreEqual("GBA · Playing", hidden.State);
        var visible = DiscordActivityPayload.Create(Enabled with { ShareGameTitle = true }, Game with { Paused = true })!;
        Assert.AreEqual(Game.HeaderTitle, visible.Details); Assert.AreEqual("GBA · Paused", visible.State);
        Assert.IsNull(DiscordActivityPayload.Create(new(false), Game)); Assert.IsNull(DiscordActivityPayload.Create(Enabled, null));
    }

    [TestMethod]
    [DataRow("C:\\Users\\Private\\game.gba")]
    [DataRow("/home/private/game.gbc")]
    [DataRow("https://secret.example/room")]
    public void PathsAndUrlsAreNotAcceptedAsTitles(string title) =>
        Assert.AreEqual(DiscordActivityPayload.Create(Enabled, Game), DiscordActivityPayload.Create(Enabled with { ShareGameTitle = true }, Game with { HeaderTitle = title }));

    [TestMethod]
    public void TitlesAreBoundedInUtf8AndStripControls()
    {
        var allowed = Enabled with { ShareGameTitle = true };
        Assert.AreEqual("POKEMON", DiscordActivityPayload.Create(allowed, Game with { HeaderTitle = "\0POKE\nMON\u202e" })!.Details);
        var payload = DiscordActivityPayload.Create(allowed, Game with { HeaderTitle = string.Concat(Enumerable.Repeat("🎮", 100)) })!;
        Assert.IsTrue(Encoding.UTF8.GetByteCount(payload.Details) <= 120);
        Assert.IsFalse(payload.Details.Contains('\uFFFD'));
    }

    [TestMethod]
    public void CoalescesNormalUpdatesButClearsAndRevokesTitleImmediately()
    {
        var clock = new TestClock(); var fake = new FakeClient();
        using var service = new DiscordPresenceService(_ => fake, clock);
        var allowed = Enabled with { ShareGameTitle = true };
        service.Update(allowed, Game); service.Update(allowed, Game);
        Assert.HasCount(1, fake.Sent);
        service.Update(allowed, Game with { Paused = true }); Assert.HasCount(1, fake.Sent);
        clock.Advance(5); service.Update(allowed, Game with { Paused = true }); Assert.HasCount(2, fake.Sent);
        service.Update(Enabled, Game); Assert.HasCount(3, fake.Sent); Assert.IsFalse(fake.Sent[^1]!.Details.Contains("POKEMON"));
        service.Update(Enabled, null); Assert.IsNull(fake.Sent[^1]); Assert.HasCount(4, fake.Sent);
        service.Update(Enabled, null); Assert.HasCount(4, fake.Sent);
        service.Update(new(false), Game); Assert.IsTrue(fake.Disposed); Assert.AreEqual(DiscordPresenceStatus.Disabled, service.Status);
        Assert.IsNull(service.Preview);
    }

    [TestMethod]
    public void ReplacementAndShutdownDisposeOldClientsAndNeverSendAfterDispose()
    {
        var clients = new List<FakeClient>();
        var service = new DiscordPresenceService(_ => { var client = new FakeClient(); clients.Add(client); return client; }, TimeProvider.System);
        service.Update(Enabled, Game);
        service.Update(Enabled with { ApplicationId = "223456789012345678" }, Game);
        Assert.IsTrue(clients[0].Disposed); Assert.HasCount(2, clients); Assert.HasCount(1, clients[1].Sent);
        service.Dispose(); service.Dispose(); service.Update(Enabled, Game);
        Assert.IsTrue(clients[1].Disposed); Assert.HasCount(2, clients); Assert.IsNull(service.Preview);
    }

    [TestMethod]
    public void RapidReenableWaitsForOldClearBeforeStartingNewClient()
    {
        var old = new FakeClient { CloseOnDispose = false }; int created = 0;
        using var service = new DiscordPresenceService(_ => { created++; return created == 1 ? old : new FakeClient(); }, TimeProvider.System);
        service.Update(Enabled, Game); service.Update(new(false), Game); Assert.IsTrue(old.Disposed);
        service.Update(Enabled, Game); Assert.AreEqual(1, created);
        old.IsClosed = true; service.Update(Enabled, Game); Assert.AreEqual(2, created);
    }

    [TestMethod]
    public void MissingHeaderDoesNotFallBackToPrivateFilename()
    {
        var rom = new RomSnapshot("My private filename", "GBA", 32768, 0, true, false, false, "hash", new(false, 0, 0, false));
        var snapshot = new EmulationSnapshot(SessionState.Running, false, false, 1, 1, rom, null, []);
        var activity = DiscordActivityPayload.Create(Enabled with { ShareGameTitle = true }, DiscordGameActivity.FromSnapshot(snapshot, false));
        Assert.IsNotNull(activity); Assert.IsFalse(activity.Details.Contains("private"));
    }

    [TestMethod]
    public void IpcFailureCannotStopEmulationAndRetriesAreBounded()
    {
        var clock = new TestClock(); int attempts = 0;
        using var service = new DiscordPresenceService(_ => { attempts++; throw new IOException("synthetic"); }, clock);
        service.Update(Enabled, Game); Assert.AreEqual(DiscordPresenceStatus.Error, service.Status);
        for (int i = 0; i < 100; i++) service.Update(Enabled, Game);
        Assert.AreEqual(1, attempts); clock.Advance(30); service.Update(Enabled, Game); Assert.AreEqual(2, attempts);
        service.Update(new(false), Game); Assert.AreEqual(DiscordPresenceStatus.Disabled, service.Status);
    }

    [TestMethod]
    public void ConnectedMeansLocalIpcNotProofOfPublishedActivity()
    {
        var fake = new FakeClient(); using var service = new DiscordPresenceService(_ => fake, TimeProvider.System);
        service.Update(Enabled, null); Assert.AreEqual(DiscordPresenceStatus.WaitingForDiscord, service.Status); Assert.IsNull(service.Preview);
        fake.Status = DiscordPresenceStatus.Connected; Assert.AreEqual(DiscordPresenceStatus.Connected, service.Status);
        fake.Status = DiscordPresenceStatus.WaitingForDiscord; Assert.AreEqual(DiscordPresenceStatus.WaitingForDiscord, service.Status);
    }

    [TestMethod]
    [DataRow("ROM", false, DiscordGameSystem.GameBoy)]
    [DataRow("ROM", true, DiscordGameSystem.GameBoyColor)]
    [DataRow("GBA Flash", false, DiscordGameSystem.GameBoyAdvance)]
    public void AllThreeSystemsUseHeaderAndLinkOrStoppedSessionsAreHidden(string type, bool color, DiscordGameSystem system)
    {
        var rom = new RomSnapshot("private-filename", type, 32768, 0, color, false, false, "private-hash", new(false, 0, 0, false)) { CartridgeHeaderTitle = "HEADER" };
        var snapshot = new EmulationSnapshot(SessionState.Running, false, false, 1, 1, rom, null, []);
        var game = DiscordGameActivity.FromSnapshot(snapshot, false)!;
        Assert.AreEqual(system, game.System); Assert.AreEqual("HEADER", game.HeaderTitle);
        Assert.IsNull(DiscordGameActivity.FromSnapshot(snapshot, true));
        foreach (var state in new[] { SessionState.Starting, SessionState.Stopping, SessionState.Stopped, SessionState.Faulted })
            Assert.IsNull(DiscordGameActivity.FromSnapshot(snapshot.WithState(state, false), false));
        Assert.IsTrue(DiscordGameActivity.FromSnapshot(snapshot.WithState(SessionState.Paused, true), false)!.Paused);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }
    private sealed class FakeClient : IDiscordPresenceClient
    {
        public DiscordPresenceStatus Status { get; set; } = DiscordPresenceStatus.WaitingForDiscord;
        public readonly List<DiscordActivityPayload?> Sent = new();
        public bool Disposed;
        public bool IsClosed { get; set; }
        public bool CloseOnDispose = true;
        public void SetActivity(DiscordActivityPayload? payload) { Assert.IsFalse(Disposed); Sent.Add(payload); }
        public void Dispose() { Disposed = true; IsClosed = CloseOnDispose; }
    }
}
