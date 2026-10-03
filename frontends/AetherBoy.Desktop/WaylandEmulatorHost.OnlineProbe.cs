using AetherBoy.Runtime.Netplay;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private IOnlineProbeSession? onlineProbe;
    private Task? onlineProbeStopTask;
    private bool showOnlineProbePage;
    private bool probeRecordReport;
    private string? onlineProbeActionError;
    private string ProbeReportDirectory => Path.Combine(dataPaths.State, "online-diagnostics");

    internal Func<OnlineRoomSettings, bool, string, string?, IOnlineProbeSession> OnlineProbeSessionFactory =
        (settings, host, code, directory) => new OnlineProbeSession(settings, host, code, directory);

    private void OpenOnlineProbePage()
    {
        CommitActiveText();
        showOnlineProbePage = true;
        onlineProbeActionError = null;
        probeRecordReport = options.RecordDiagnostics;
        focusedControl = -1;
    }

    private void StartOnlineProbe(bool host)
    {
        if (onlineProbe?.Snapshot.Active == true || onlineProbeStopTask is not null) return;
        onlineProbeActionError = null;
        if (IsOnlineLink || IsLoading || stateOperation is not null)
        { onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("End the active online game or pending operation before testing."); return; }
        try
        {
            CommitActiveText();
            roomSettings.Validate();
            string code = host ? "" : OnlineRoomSettings.NormalizeCode(roomCodeInput);
            onlineProbe?.Dispose();
            onlineProbe = null;
            // The factory receives no ROM, save path or emulator session.
            onlineProbe = OnlineProbeSessionFactory(roomSettings, host, code, probeRecordReport ? ProbeReportDirectory : null);
            if (!host) roomCodeInput = code[..5] + "-" + code[5..];
        }
        catch (ArgumentException)
        { onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("Check the HTTPS room server, access key and ten-character test code."); }
        catch (Exception)
        { onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("Could not start the test. Check the complete build and server settings."); }
    }

    private void StopOnlineProbe()
    {
        if (onlineProbe is not { } active || onlineProbeStopTask is not null || !active.Snapshot.Active) return;
        onlineProbeStopTask = active.StopAsync();
    }

    private void LeaveOnlineProbePage()
    {
        StopOnlineProbe();
        showOnlineProbePage = false;
        focusedControl = -1;
        if (onlineProbeStopTask is null)
        {
            onlineProbe?.Dispose();
            onlineProbe = null;
        }
    }

    private void UpdateOnlineProbe()
    {
        if (onlineProbeStopTask is not { IsCompleted: true } stopped) return;
        if (stopped.IsFaulted)
        {
            _ = stopped.Exception;
            onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("The test could not close cleanly. Review the connection report.");
        }
        onlineProbeStopTask = null;
        if (!showOnlineProbePage)
        {
            onlineProbe?.Dispose();
            onlineProbe = null;
        }
    }

    private void DrawOnlineProbePage()
    {
        UpdateOnlineProbe();
        var state = onlineProbe?.Snapshot;
        bool busy = state?.Active == true || onlineProbeStopTask is not null;
        bool canStart = !busy && !IsOnlineLink && !IsLoading && stateOperation is null && RoomServerConfigured();

        ActionButton(300, 198, 230, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO ONLINE LINK"), LeaveOnlineProbePage);
        ActionButton(900, 198, 220, 40, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN REPORT FOLDER"), () => OpenFolder(ProbeReportDirectory));
        Ink(300, 254, global::AetherBoy.Runtime.Localization.UiText.Get("CONNECTION TEST · NO GAME"), 23, Colors.Cyan, true);
        Ink(300, 287, global::AetherBoy.Runtime.Localization.UiText.Get("Use the same room server on both PCs. A test room cannot join a game room."), 14, Colors.Muted);

        var description = LinuxOnlineProbePresentation.Describe(state);
        Ink(300, 328, onlineProbeActionError ?? description.Headline, 17,
            onlineProbeActionError is not null || state?.Phase == OnlineProbePhase.Failed ? Colors.Danger : Colors.Text, true);
        Ink(300, 356, description.Detail, 14, Colors.Muted);

        Ink(300, 395, global::AetherBoy.Runtime.Localization.UiText.Get("TEST ROOM CODE"), 13, Colors.Muted);
        if (state is { Connection.RoomAdmitted: true })
        {
            Ink(300, 423, state.Connection.DisplayCode, 30, Colors.Text, true);
            ActionButton(840, 412, 280, 44, global::AetherBoy.Runtime.Localization.UiText.Get("COPY TEST CODE"), () =>
            {
                if (!SDL.SetClipboardText(state.Connection.DisplayCode))
                    onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("Could not copy the test code. Read it aloud instead.");
            }, enabled: busy);
        }
        else
            DrawTextEntry(TextField.RoomCode, 300, 412, 410, 44, "ABCDE-FGHJK", enabled: !busy);

        ActionButton(300, 471, 255, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CREATE TEST ROOM"), () => StartOnlineProbe(true), enabled: canStart);
        ActionButton(570, 471, 255, 44, global::AetherBoy.Runtime.Localization.UiText.Get("JOIN WITH CODE"), () => StartOnlineProbe(false), enabled: canStart && roomCodeInput.Trim().Length >= 10);
        ActionButton(840, 471, 280, 44, state?.Phase == OnlineProbePhase.Passed ? global::AetherBoy.Runtime.Localization.UiText.Get("END TEST") : global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL TEST"),
            StopOnlineProbe, enabled: state?.Active == true && onlineProbeStopTask is null);

        string mark(bool done) => done ? "OK" : global::AetherBoy.Runtime.Localization.UiText.Get("WAIT");
        Ink(300, 539, global::AetherBoy.Runtime.Localization.UiText.Format("ROOM {0}     PEER {1}", mark(state?.Connection.RoomAdmitted == true), mark(state?.Connection.PeerPresent == true)), 14, Colors.Text);
        Ink(730, 539, global::AetherBoy.Runtime.Localization.UiText.Format("CHANNEL {0}     DATA {1}", mark(state?.Connection.Connected == true || state?.Phase is OnlineProbePhase.Testing or OnlineProbePhase.Passed), mark(state?.Phase == OnlineProbePhase.Passed)), 14, Colors.Text);
        if (state is null)
            Ink(300, 565, global::AetherBoy.Runtime.Localization.UiText.Get("Checks message order, content and response time; no ROM or save is used."), 13, Colors.Muted);
        else
            Ink(300, 565, global::AetherBoy.Runtime.Localization.UiText.Format("Own echoes {0}/{1}  ·  Peer requests answered {2}/{3}", state.Progress.VerifiedEchoes, state.Progress.ExpectedEchoes, state.Progress.PeerRequestsEchoed, state.Progress.ExpectedEchoes),
                13, Colors.Muted);

        ActionButton(300, 591, 330, 36, probeRecordReport ? global::AetherBoy.Runtime.Localization.UiText.Get("SAVE LOCAL REPORT: ON") : global::AetherBoy.Runtime.Localization.UiText.Get("SAVE LOCAL REPORT: OFF"),
            () => probeRecordReport = !probeRecordReport, probeRecordReport, enabled: !busy);
        ActionButton(650, 591, 230, 36, global::AetherBoy.Runtime.Localization.UiText.Get("COPY SAFE REPORT"), () =>
        {
            if (onlineProbe is null || !SDL.SetClipboardText(onlineProbe.GetDiagnosticReport()))
                onlineProbeActionError = global::AetherBoy.Runtime.Localization.UiText.Get("Could not copy the report.");
        }, enabled: onlineProbe is not null);
        Ink(300, 631, state?.ReportWriteFailed == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Local report could not be saved; copy it instead.")
            : state?.DiagnosticPath is { } path ? global::AetherBoy.Runtime.Localization.UiText.Get("Local report: ") + Path.GetFileName(path)
            : global::AetherBoy.Runtime.Localization.UiText.Get("Reports stay local. A pass does not verify Pokémon trading."), 12, Colors.Muted);
    }
}
