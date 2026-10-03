using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    internal static Func<DiscordPresenceService> DiscordPresenceFactory { get; set; } = () => new();
    private readonly DiscordPresenceService discordPresence = DiscordPresenceFactory();
    private long nextDiscordPoll;
    private string discordApplicationIdInput = "";
    private bool editingDiscordId;
    private bool discordSetup;
    private string discordFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Application IDs are public identifiers, not passwords or tokens.");

    private void PollDiscordPresence()
    {
        if (Environment.TickCount64 < nextDiscordPoll) return;
        nextDiscordPoll = Environment.TickCount64 + 1000;
        UpdateDiscordPresence();
    }

    private void UpdateDiscordPresence() => discordPresence.Update(
        new(options.DiscordPresenceEnabled, options.DiscordShareGameTitle, options.DiscordApplicationId),
        DiscordGameActivity.FromSnapshot(session?.LatestSnapshot, IsOnlineLink || localLinkSession is not null));

    private void OpenDiscordSettings()
    {
        CommitActiveText();
        systemSection = SystemSection.Discord;
        discordApplicationIdInput = options.DiscordApplicationId;
        discordSetup = false;
        focusedControl = -1;
    }

    private void ApplyDiscordId()
    {
        if (textEditor.IsComposing) return;
        CommitActiveText();
        string id = discordApplicationIdInput.Trim();
        if (id.Length != 0 && !DiscordPresenceOptions.IsValidApplicationId(id))
        { discordFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Enter an application ID with 17 to 20 digits. Do not paste a token."); return; }
        if (id.Length == 0 || options.DiscordApplicationId.Length > 0 && id != options.DiscordApplicationId)
            options.DiscordPresenceEnabled = false;
        options.DiscordApplicationId = id;
        discordApplicationIdInput = id;
        discordFeedback = id.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("ID removed. Discord activity is off.")
            : options.DiscordPresenceEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("ID saved. AetherBoy is trying to connect to Discord.") : global::AetherBoy.Runtime.Localization.UiText.Get("ID saved. You can now enable Discord activity.");
        MarkSettingsChanged(); UpdateDiscordPresence();
    }

    private void ToggleDiscordPresence()
    {
        if (!options.DiscordPresenceEnabled && !DiscordPresenceOptions.IsValidApplicationId(options.DiscordApplicationId))
        { discordFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Enter a valid application ID and choose Apply first."); discordSetup = true; return; }
        options.DiscordPresenceEnabled = !options.DiscordPresenceEnabled;
        MarkSettingsChanged(); UpdateDiscordPresence();
    }

    private void DrawDiscordSettings()
    {
        Ink(510, 203, global::AetherBoy.Runtime.Localization.UiText.Get("Discord activity"), 22, bold: true);
        ActionButton(910, 194, 200, 40, discordSetup ? global::AetherBoy.Runtime.Localization.UiText.Get("Privacy") : global::AetherBoy.Runtime.Localization.UiText.Get("Setup"), () =>
        { CommitActiveText(); discordSetup = !discordSetup; }, focusId: "discord:setup");
        if (discordSetup)
        {
            DrawSettingsParagraph(300, 261, global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy's public application ID is already set. Only developers need to change it. Do not enter a login, bot token or password. Discord's desktop app must run as the same user."), 805, 16);
            Ink(300, 353, global::AetherBoy.Runtime.Localization.UiText.Get("Application ID"), 18, bold: true);
            DrawTextEntry(TextField.DiscordApplicationId, 300, 390, 560, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Application ID"));
            ActionButton(884, 390, 226, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Apply"), ApplyDiscordId, focusId: "discord:apply");
            DrawSettingsParagraph(300, 451, discordFeedback, 805, 16);
            DrawSettingsParagraph(300, 528, global::AetherBoy.Runtime.Localization.UiText.Get("Activity starts enabled. Replacing a saved ID switches it off. Discord checks the ID when connecting. Sandboxed Discord installations may not expose the local IPC socket."), 805, 14);
            return;
        }
        ActionButton(300, 257, 390, 44, options.DiscordPresenceEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Discord activity: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Discord activity: off"),
            ToggleDiscordPresence, options.DiscordPresenceEnabled, focusId: "discord:enable");
        ActionButton(710, 257, 400, 44, options.DiscordShareGameTitle ? global::AetherBoy.Runtime.Localization.UiText.Get("Share game title: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Share game title: off"), () =>
        { options.DiscordShareGameTitle = !options.DiscordShareGameTitle; MarkSettingsChanged(); UpdateDiscordPresence(); }, options.DiscordShareGameTitle, focusId: "discord:title");
        DrawSettingsParagraph(300, 315, global::AetherBoy.Runtime.Localization.UiText.Get("Shares the system and play/pause status. Titles need separate permission. No ROM paths, saves or room codes. Hidden without a single-player game and during Link sessions."), 805, 14);
        string state = discordPresence.Status switch
        {
            DiscordPresenceStatus.NeedsApplicationId => global::AetherBoy.Runtime.Localization.UiText.Get("Enter a valid application ID above."),
            DiscordPresenceStatus.WaitingForDiscord => global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for Discord. Open its desktop app as the same user."),
            DiscordPresenceStatus.Connected => global::AetherBoy.Runtime.Localization.UiText.Get("Connected to the local Discord app."),
            DiscordPresenceStatus.Error => global::AetherBoy.Runtime.Localization.UiText.Get("Discord rejected the connection. Check the ID, then switch activity off and on."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("Off. AetherBoy is not sending Discord activity.")
        };
        DrawSettingsParagraph(300, 409, state, 805, 14, Colors.Text);
        var preview = discordPresence.Preview;
        Ink(300, 475, preview is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Preview: no activity shared.") : global::AetherBoy.Runtime.Localization.UiText.Get("Preview of allowed content"), 16, bold: true);
        if (preview is not null)
        {
            Ink(300, 507, textRenderer.Fit(preview.Details, 805, 16), 16);
            Ink(300, 537, preview.State, 16, Colors.Cyan);
        }
        DrawSettingsParagraph(300, 577, global::AetherBoy.Runtime.Localization.UiText.Get("Discord's activity settings may hide this status. A connection is not proof of public visibility."), 805, 14);
    }
}
