using AetherBoy.Runtime.Netplay;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private OnlineRoomTransport? onlineRoomTransport;
    private OnlineRoomTransport? lastRoomTransport;
    private OnlineRoomSettings roomSettings = new();
    private string roomServerInput = "", roomAccessKeyInput = "", roomCodeInput = "";
    private bool startNativeRoom, showRoomSetup, showLegacyOnlineLink, acceptRoomSaveCopy;
    private string RoomSettingsPath => Path.Combine(dataPaths.Config, "online-room.json");

    private void LoadRoomSettings()
    {
        onlineEditingField = TextField.None;
        roomSettings = OnlineRoomSettings.Load(RoomSettingsPath);
        roomServerInput = roomSettings.ServerUrl; roomAccessKeyInput = roomSettings.AccessKey;
        showRoomSetup = !RoomServerConfigured();
        if (!IsOnlineLink) { acceptRoomSaveCopy = false; showLegacyOnlineLink = false; }
    }

    private bool RoomServerConfigured()
    {
        try { roomSettings.Validate(); return true; } catch (ArgumentException) { return false; }
    }

    private void SaveRoomSettings()
    {
        try
        {
            CommitActiveText();
            var candidate = new OnlineRoomSettings(roomServerInput.Trim(), roomAccessKeyInput.Trim());
            candidate.Save(RoomSettingsPath); roomSettings = candidate; showRoomSetup = false;
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Server settings saved. Create a room or enter a room code.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
    }

    private void StartOnlineRoom(bool host)
    {
        try
        {
            CommitActiveText(); roomSettings.Validate();
            if (!host) roomCodeInput = OnlineRoomSettings.NormalizeCode(roomCodeInput);
            if (!acceptRoomSaveCopy) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose to use a copy of your save before starting."); return; }
            startNativeRoom = true;
            if (StartOnlineLink(host, acceptGbaDevelopment: true))
            { lastRoomTransport = onlineRoomTransport; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Connecting to the room server…"); }
        }
        catch (ArgumentException ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
        finally { startNativeRoom = false; }
    }

    private void DrawOnlineRoomPage()
    {
        if (showOnlineProbePage) { DrawOnlineProbePage(); return; }
        ActionButton(300, 198, 190, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO TOOLS"), () => { CommitActiveText(); showOnlineLinkPage = false; OpenControlPage(ControlCenterPage.Tools); });
        ActionButton(900, 198, 220, 40, showRoomSetup ? global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO ROOMS") : global::AetherBoy.Runtime.Localization.UiText.Get("SERVER SETTINGS"), () => { CommitActiveText(); showRoomSetup = !showRoomSetup; }, enabled: !IsOnlineLink);
        Ink(300, 262, "ONLINE LINK", 28, Colors.Cyan, true);
        Ink(300, 305, global::AetherBoy.Runtime.Localization.UiText.Get("Connect with a room code, then play in the emulator."), 16, Colors.Text);
        ActionButton(820, 260, 300, 36, global::AetherBoy.Runtime.Localization.UiText.Get("CONNECTION REPORTS"), () => OpenFolder(Path.Combine(dataPaths.State, "online-diagnostics")));
        if (showRoomSetup)
        {
            Ink(300, 355, global::AetherBoy.Runtime.Localization.UiText.Get("Enter the server details on both computers"), 18, Colors.Text, true);
            Ink(300, 391, global::AetherBoy.Runtime.Localization.UiText.Get("ROOM SERVER · HTTPS ADDRESS"), 13, Colors.Muted);
            DrawTextEntry(TextField.RoomServer, 300, 416, 820, 44, "https://rooms.example.com");
            Ink(300, 482, global::AetherBoy.Runtime.Localization.UiText.Get("SERVER ACCESS KEY"), 13, Colors.Muted);
            DrawTextEntry(TextField.RoomAccessKey, 300, 507, 820, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Key from the room server administrator"));
            ActionButton(300, 580, 230, 46, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE SERVER"), SaveRoomSettings);
            Ink(555, 587, global::AetherBoy.Runtime.Localization.UiText.Get("The room server provides the connection settings."), 13, Colors.Muted);
            return;
        }
        if (onlineRoomTransport is { } active)
        {
            Ink(300, 357, global::AetherBoy.Runtime.Localization.UiText.Get("ROOM CODE · SHARE WITH YOUR FRIEND"), 13, Colors.Muted);
            Ink(300, 390, active.DisplayCode.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Creating room…") : active.DisplayCode, 36, Colors.Text, true);
            ActionButton(830, 388, 290, 48, global::AetherBoy.Runtime.Localization.UiText.Get("COPY ROOM CODE"), () =>
            { if (!SDL.SetClipboardText(active.DisplayCode)) statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not copy the room code."); }, enabled: active.RoomCode.Length == 10);
            Ink(300, 465, textRenderer.Fit(active.Status, 820, 16), 16, Colors.Cyan);
            Ink(300, 499, global::AetherBoy.Runtime.Localization.UiText.Get("This session uses a copy of your save. Your original stays untouched."), 14, Colors.Muted);
            ActionButton(300, 548, 260, 48, global::AetherBoy.Runtime.Localization.UiText.Get("RETURN TO GAME"), CloseControlCenter, enabled: active.Connected);
            ActionButton(580, 548, 220, 48, global::AetherBoy.Runtime.Localization.UiText.Get("DISCONNECT"), StopOnlineLink);
            ActionButton(820, 548, 300, 48, global::AetherBoy.Runtime.Localization.UiText.Get("CHECK SAVE COPIES"), OpenOnlineSaveRecovery);
            Ink(300, 612, global::AetherBoy.Runtime.Localization.UiText.Get("The room expires after 10 minutes if no one joins. Keep AetherBoy open."), 13, Colors.Muted);
            return;
        }
        bool canStart = session?.LatestSnapshot.Rom is { } rom && (!rom.IsGameBoyAdvance || onlineGbaPreview?.IsDevelopmentCandidate == true)
            && !IsOnlineLink && !IsLoading && stateOperation is null && acceptRoomSaveCopy && RoomServerConfigured();
        Ink(300, 356, global::AetherBoy.Runtime.Localization.UiText.Get("CREATE A ROOM"), 15, Colors.Text, true);
        Ink(725, 356, global::AetherBoy.Runtime.Localization.UiText.Get("JOIN A ROOM"), 15, Colors.Text, true);
        Ink(300, 394, global::AetherBoy.Runtime.Localization.UiText.Get("Share the room code with your friend."), 14, Colors.Muted);
        DrawTextEntry(TextField.RoomCode, 725, 384, 395, 48, "ABCDE-FGHJK");
        ActionButton(300, 451, 385, 52, global::AetherBoy.Runtime.Localization.UiText.Get("CREATE ROOM"), () => StartOnlineRoom(true), enabled: canStart);
        ActionButton(725, 451, 395, 52, global::AetherBoy.Runtime.Localization.UiText.Get("JOIN ROOM"), () => StartOnlineRoom(false), enabled: canStart && roomCodeInput.Trim().Length >= 10);
        ActionButton(300, 514, 820, 40, (acceptRoomSaveCopy ? "[X] " : "[ ] ") + global::AetherBoy.Runtime.Localization.UiText.Get("TRY EXPERIMENTAL ONLINE LINK WITH A COPY OF MY SAVE"),
            () => acceptRoomSaveCopy = !acceptRoomSaveCopy, acceptRoomSaveCopy);
        Ink(300, 564, textRenderer.Fit(lastRoomTransport?.Fault?.Message ?? onlineProfileMessage, 820, 13), 13, Colors.Muted);
        ActionButton(300, 590, 260, 38, global::AetherBoy.Runtime.Localization.UiText.Get("TEST WITHOUT A GAME"), OpenOnlineProbePage, enabled: !IsOnlineLink);
        ActionButton(580, 590, 260, 38, global::AetherBoy.Runtime.Localization.UiText.Get("CONNECT IN BROWSER"), () => { CommitActiveText(); showLegacyOnlineLink = true; });
        ActionButton(860, 590, 260, 38, global::AetherBoy.Runtime.Localization.UiText.Get("CHECK SAVE COPIES"), OpenOnlineSaveRecovery);
    }
}
