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
            statusMessage = "Room server saved. Create a room or enter your friend's code.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        { statusMessage = ex.Message; }
    }

    private void StartOnlineRoom(bool host)
    {
        try
        {
            CommitActiveText(); roomSettings.Validate();
            if (!host) roomCodeInput = OnlineRoomSettings.NormalizeCode(roomCodeInput);
            if (!acceptRoomSaveCopy) { statusMessage = "Accept the protected save copy before starting."; return; }
            startNativeRoom = true;
            if (StartOnlineLink(host, acceptGbaDevelopment: true))
            { lastRoomTransport = onlineRoomTransport; statusMessage = "Connecting to your room server…"; }
        }
        catch (ArgumentException ex) { statusMessage = ex.Message; }
        finally { startNativeRoom = false; }
    }

    private void DrawOnlineRoomPage()
    {
        ActionButton(300, 198, 190, 40, "BACK TO TOOLS", () => { CommitActiveText(); showOnlineLinkPage = false; OpenControlPage(ControlCenterPage.Tools); });
        ActionButton(900, 198, 220, 40, showRoomSetup ? "BACK TO ROOMS" : "SERVER SETTINGS", () => { CommitActiveText(); showRoomSetup = !showRoomSetup; }, enabled: !IsOnlineLink);
        Ink(300, 262, "ONLINE LINK", 28, Colors.Cyan, true);
        Ink(300, 305, "One room. Two players. Play here in the emulator.", 16, Colors.Text);
        ActionButton(820, 260, 300, 36, "CONNECTION REPORTS", () => OpenFolder(Path.Combine(dataPaths.State, "online-diagnostics")));
        if (showRoomSetup)
        {
            Ink(300, 355, "Set up once on each computer", 18, Colors.Text, true);
            Ink(300, 391, "ROOM SERVER · HTTPS ADDRESS", 13, Colors.Muted);
            DrawTextEntry(TextField.RoomServer, 300, 416, 820, 44, "https://rooms.example.com");
            Ink(300, 482, "SERVER ACCESS KEY", 13, Colors.Muted);
            DrawTextEntry(TextField.RoomAccessKey, 300, 507, 820, 44, "Key from the room server administrator");
            ActionButton(300, 580, 230, 46, "SAVE SERVER", SaveRoomSettings);
            Ink(555, 587, "The server supplies the TURN settings automatically.", 13, Colors.Muted);
            return;
        }
        if (onlineRoomTransport is { } active)
        {
            Ink(300, 357, "ROOM CODE · SHARE WITH YOUR FRIEND", 13, Colors.Muted);
            Ink(300, 390, active.DisplayCode.Length == 0 ? "Creating room…" : active.DisplayCode, 36, Colors.Text, true);
            ActionButton(830, 388, 290, 48, "COPY ROOM CODE", () =>
            { if (!SDL.SetClipboardText(active.DisplayCode)) statusMessage = "Could not copy the room code."; }, enabled: active.RoomCode.Length == 10);
            Ink(300, 465, textRenderer.Fit(active.Status, 820, 16), 16, Colors.Cyan);
            Ink(300, 499, "Your original save is safe. This session uses its own copy.", 14, Colors.Muted);
            ActionButton(300, 548, 260, 48, "RETURN TO GAME", CloseControlCenter, enabled: active.Connected);
            ActionButton(580, 548, 220, 48, "DISCONNECT", StopOnlineLink);
            ActionButton(820, 548, 300, 48, "CHECK SAVE COPIES", OpenOnlineSaveRecovery);
            Ink(300, 612, "Waiting rooms expire after 10 minutes. Keep this emulator open.", 13, Colors.Muted);
            return;
        }
        bool canStart = session?.LatestSnapshot.Rom is { } rom && (!rom.IsGameBoyAdvance || onlineGbaPreview?.IsDevelopmentCandidate == true)
            && !IsOnlineLink && !IsLoading && stateOperation is null && acceptRoomSaveCopy && RoomServerConfigured();
        Ink(300, 356, "INVITE A FRIEND", 15, Colors.Text, true);
        Ink(725, 356, "HAVE A ROOM CODE?", 15, Colors.Text, true);
        Ink(300, 394, "Create a code and share it.", 14, Colors.Muted);
        DrawTextEntry(TextField.RoomCode, 725, 384, 395, 48, "ABCDE-FGHJK");
        ActionButton(300, 451, 385, 52, "CREATE ROOM", () => StartOnlineRoom(true), enabled: canStart);
        ActionButton(725, 451, 395, 52, "JOIN ROOM", () => StartOnlineRoom(false), enabled: canStart && roomCodeInput.Trim().Length >= 10);
        ActionButton(300, 514, 820, 40, (acceptRoomSaveCopy ? "[X] " : "[ ] ") + "USE A PROTECTED SAVE COPY · EXPERIMENTAL ONLINE LINK",
            () => acceptRoomSaveCopy = !acceptRoomSaveCopy, acceptRoomSaveCopy);
        Ink(300, 564, textRenderer.Fit(lastRoomTransport?.Fault?.Message ?? onlineProfileMessage, 820, 13), 13, Colors.Muted);
        ActionButton(300, 590, 310, 38, "MANUAL BROWSER CONNECTION", () => { CommitActiveText(); showLegacyOnlineLink = true; });
        ActionButton(810, 590, 310, 38, "CHECK SAVE COPIES", OpenOnlineSaveRecovery);
    }
}
