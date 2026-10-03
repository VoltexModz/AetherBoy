using AetherBoy.Runtime.Netplay;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showOnlineSaveRecovery;
    private string? onlineRecoveryTargetSave, onlineRecoveryRomPath;
    private OnlineSaveRecoveryInfo[] onlineRecoveryCopies = [];
    private string? pendingOnlineSavePromotion;
    private int onlineRecoveryPage;

    private void OpenOnlineSaveRecovery()
    {
        if (storage is not null && romPath is not null)
        {
            onlineRecoveryTargetSave = storage.SavePath;
            onlineRecoveryRomPath = romPath;
        }
        if (onlineRecoveryTargetSave is null || onlineRecoveryRomPath is null)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Select your own cartridge first so only its session saves can be reviewed."); return; }
        showOnlineSaveRecovery = true;
        pendingOnlineSavePromotion = null; onlineRecoveryPage = 0;
        RefreshOnlineRecoveryCopies();
    }

    private void RefreshOnlineRecoveryCopies()
    {
        string root = Path.Combine(dataPaths.State, "online-link");
        try
        {
            onlineRecoveryCopies = Directory.Exists(root) ? Directory.EnumerateDirectories(root)
                .OrderByDescending(Path.GetFileName).Take(256)
                .Select(path => { try { return OnlineSaveRecovery.Inspect(path); } catch (IOException) { return null; } })
                .Where(info => info is not null && string.Equals(info.OriginalSavePath, onlineRecoveryTargetSave, StringComparison.Ordinal))
                .Select(info => info!).ToArray() : [];
            onlineRecoveryPage = Math.Clamp(onlineRecoveryPage, 0, Math.Max(0, (onlineRecoveryCopies.Length - 1) / 2));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { onlineRecoveryCopies = []; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not inspect session copies: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
    }

    internal bool PromoteOnlineSaveCopy(string directory)
    {
        if (session is not null || IsLoading || stateOperation is not null || onlineRecoveryTargetSave is null || onlineRecoveryRomPath is null)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Stop the selected game before explicitly adopting its session copy."); return false; }
        string? resumeArchive = null;
        try
        {
            // Normal Linux sessions own a cartridge lock as well as the Runtime's
            // save lease. Take the same central lock while replacing its battery family.
            using var ownership = LinuxRomStorage.Open(dataPaths, onlineRecoveryRomPath);
            if (ownership.SavePath != onlineRecoveryTargetSave)
                throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("The selected cartridge changed; select it again before adopting a save."));
            var info = OnlineSaveRecovery.Inspect(directory);
            if (info.OriginalSavePath != onlineRecoveryTargetSave)
                throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("The session copy belongs to a different cartridge."));
            if (!info.CanPromote) throw new InvalidOperationException(info.Reason);
            string backup = OnlineSaveRecovery.Promote(directory, onlineRecoveryTargetSave,
                () => resumeArchive = ArchiveOnlineResume(directory, ownership.StateBasePath));
            pendingResumeIdentity = null;
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Session copy adopted; backup: ") + backup + global::AetherBoy.Runtime.Localization.UiText.Get(". Old manual states can undo the imported progress.");
            pendingOnlineSavePromotion = null;
            RefreshOnlineRecoveryCopies();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Session copy was NOT adopted: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message) + (resumeArchive is null ? "" : global::AetherBoy.Runtime.Localization.UiText.Get(". Resume archive: ") + resumeArchive); return false; }
    }

    private static string? ArchiveOnlineResume(string directory, string stateBasePath)
    {
        string resume = LinuxStateGallery.PathFor(stateBasePath, 0);
        string[] files = new[] { resume, resume + ".preview" }.Where(File.Exists).ToArray();
        if (files.Length == 0) return null;
        string archive = Path.Combine(directory, "resume-before-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(archive);
        try { foreach (string file in files) File.Move(file, Path.Combine(archive, Path.GetFileName(file))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Resume could not be fully archived. Retained files: ") + archive, ex); }
        return archive;
    }

    private void DrawOnlineSaveRecoveryPage()
    {
        ActionButton(300, 198, 200, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO ONLINE"), () => { showOnlineSaveRecovery = false; pendingOnlineSavePromotion = null; });
        Ink(300, 258, global::AetherBoy.Runtime.Localization.UiText.Get("ONLINE ARCHIVE · SELECTED GAME"), 20, Colors.Cyan, true);
        Ink(300, 292, textRenderer.Fit(onlineRecoveryTargetSave ?? global::AetherBoy.Runtime.Localization.UiText.Get("No trusted game selected"), 825, 13), 13, Colors.Muted);
        if (session is not null)
        {
            Ink(300, 345, global::AetherBoy.Runtime.Localization.UiText.Get("Stop the running game safely before reviewing its final working save."), 15, Colors.Cyan);
            Ink(300, 378, global::AetherBoy.Runtime.Localization.UiText.Get("Nothing is adopted now. A separate confirmation is required afterwards."), 14, Colors.Muted);
            ActionButton(300, 432, 320, 44, global::AetherBoy.Runtime.Localization.UiText.Get("STOP GAME AND REVIEW COPIES"), () =>
            {
                CloseSession(); romPath = null;
                RefreshOnlineRecoveryCopies();
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Game stopped. Inspect copies; a clean shutdown does not prove a successful trade.");
            }, enabled: !IsLoading && stateOperation is null);
            return;
        }
        if (pendingOnlineSavePromotion is string selected)
        {
            Ink(300, 342, global::AetherBoy.Runtime.Localization.UiText.Get("Have BOTH players checked separate COPIES of their saved games?"), 16, Colors.Cyan);
            Ink(300, 376, global::AetherBoy.Runtime.Localization.UiText.Get("This replaces the selected game's original with the selected clean session copy."), 14, Colors.Muted);
            Ink(300, 406, global::AetherBoy.Runtime.Localization.UiText.Get("Old resume is archived; manual states remain and can undo the import. No trade proof."), 14, Colors.Muted);
            Ink(300, 436, textRenderer.Fit(Path.GetFileName(selected), 810, 14), 14, Colors.Cyan);
            ActionButton(300, 492, 320, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CONFIRM REPLACE WITH COPY"), () => PromoteOnlineSaveCopy(selected));
            ActionButton(640, 492, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), () => pendingOnlineSavePromotion = null);
            return;
        }
        Ink(300, 326, global::AetherBoy.Runtime.Localization.UiText.Get("Verify COPIES, leave journaled files unchanged. Interrupted / faulted copies stay locked."), 14, Colors.Muted);
        if (onlineRecoveryCopies.Length == 0)
            Ink(300, 384, global::AetherBoy.Runtime.Localization.UiText.Get("No journaled session copies for this game. Older copies are still in the online folder."), 14, Colors.Muted);
        foreach (var (copy, index) in onlineRecoveryCopies.Skip(onlineRecoveryPage * 2).Take(2).Select((copy, index) => (copy, index)))
        {
            float y = 370 + index * 95;
            Ink(300, y, textRenderer.Fit(Path.GetFileName(copy.SessionDirectory) + " · " + global::AetherBoy.Runtime.Localization.UiLabels.Recovery(copy.State), 555, 14), 14, Colors.Cyan);
            Ink(300, y + 27, textRenderer.Fit(global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(copy.Reason), 550, 13), 13, Colors.Muted);
            ActionButton(865, y, 110, 44, global::AetherBoy.Runtime.Localization.UiText.Get("FOLDER"), () => OpenFolder(copy.SessionDirectory));
            ActionButton(985, y, 130, 44, global::AetherBoy.Runtime.Localization.UiText.Get("ADOPT…"), () => pendingOnlineSavePromotion = copy.SessionDirectory, enabled: copy.CanPromote);
        }
        ActionButton(300, 580, 150, 40, global::AetherBoy.Runtime.Localization.UiText.Get("PREVIOUS"), () => onlineRecoveryPage--, enabled: onlineRecoveryPage > 0);
        ActionButton(465, 580, 150, 40, global::AetherBoy.Runtime.Localization.UiText.Get("NEXT"), () => onlineRecoveryPage++, enabled: (onlineRecoveryPage + 1) * 2 < onlineRecoveryCopies.Length);
        ActionButton(635, 580, 160, 40, global::AetherBoy.Runtime.Localization.UiText.Get("REFRESH"), RefreshOnlineRecoveryCopies);
        ActionButton(810, 580, 260, 40, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN ONLINE FOLDER"), () => OpenFolder(Path.Combine(dataPaths.State, "online-link")));
    }
}
