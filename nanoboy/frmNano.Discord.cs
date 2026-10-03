using System;
using AetherBoy.Runtime;

namespace nanoboy;

public partial class frmNano
{
    internal static Func<DiscordPresenceService> DiscordPresenceFactory { get; set; } = () => new();
    private readonly DiscordPresenceService discordPresence = DiscordPresenceFactory();
    private long nextDiscordPoll;
    private bool discordLocalLinkOpen;

    private void PollDiscordPresence()
    {
        if (Environment.TickCount64 < nextDiscordPoll) return;
        nextDiscordPoll = Environment.TickCount64 + 1000;
        UpdateDiscordPresence();
    }

    private void UpdateDiscordPresence() => discordPresence.Update(
        new(settings.DiscordPresenceEnabled, settings.DiscordShareGameTitle, settings.DiscordApplicationId),
        DiscordGameActivity.FromSnapshot(session?.LatestSnapshot, IsOnlineLink || discordLocalLinkOpen));
}
