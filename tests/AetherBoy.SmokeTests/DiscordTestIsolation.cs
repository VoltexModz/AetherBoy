using AetherBoy.Runtime;
using nanoboy;

namespace AetherBoy.SmokeTests;

internal sealed class IsolatedDiscordClient : IDiscordPresenceClient
{
    public DiscordPresenceStatus Status => DiscordPresenceStatus.WaitingForDiscord;
    public bool IsClosed { get; private set; }
    public void SetActivity(DiscordActivityPayload? payload) { }
    public void Dispose() => IsClosed = true;
    internal static void Install() => frmNano.DiscordPresenceFactory = () => new DiscordPresenceService(_ => new IsolatedDiscordClient(), TimeProvider.System);
}
