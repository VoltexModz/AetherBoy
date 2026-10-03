using AetherBoy.Runtime;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class DiscordTestIsolation
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        // Existing English-copy tests must not depend on the developer's desktop.
        // Explicit language-page tests override this; resolver tests cover regions.
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        AetherBoy.Runtime.Localization.UiText.Initialize("en");
        WaylandEmulatorHost.DiscordPresenceFactory =
            () => new DiscordPresenceService(_ => new IsolatedDiscordClient(), TimeProvider.System);
    }

    private sealed class IsolatedDiscordClient : IDiscordPresenceClient
    {
        public DiscordPresenceStatus Status => DiscordPresenceStatus.WaitingForDiscord;
        public bool IsClosed { get; private set; }
        public void SetActivity(DiscordActivityPayload? payload) { }
        public void Dispose() => IsClosed = true;
    }
}
