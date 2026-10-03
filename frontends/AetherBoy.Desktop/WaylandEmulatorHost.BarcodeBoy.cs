using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showBarcodeBoy, editingBarcode, pickingBarcode;
    private string barcodeInput = "", barcodeFeedback = "";
    private bool CanUseBarcodeBoy => !IsLoading && !IsOnlineLink && localLinkSession is null &&
        session?.LatestSnapshot.Supports(EmulationFeature.BarcodeBoy) == true;

    private void DrawBarcodeBoyPage()
    {
        Ink(300, 198, "Barcode Boy", 20, Colors.Cyan, true);
        ActionButton(860, 192, 250, 38, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO TOOLS"), () => OpenSettingsDestination(LinuxSettingsDestination.Tools));
        DrawSettingsParagraph(300, 244, global::AetherBoy.Runtime.Localization.UiText.Get("Namcot scanner for GB games, not Bardigun or GBA e-Reader. Battle Space example cards below; actual game test pending."), 810);
        var scanner = session?.LatestSnapshot.BarcodeBoy;
        ActionButton(300, 302, 310, 42, scanner is null ? global::AetherBoy.Runtime.Localization.UiText.Get("CONNECT SCANNER") : global::AetherBoy.Runtime.Localization.UiText.Get("DISCONNECT SCANNER"), () => BarcodeAction(() =>
        {
            bool enabled = session!.LatestSnapshot.BarcodeBoy is null;
            session.SetBarcodeBoyEnabledAsync(enabled).GetAwaiter().GetResult();
            barcodeFeedback = enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Scanner connected. Open the barcode feature in your game.") : global::AetherBoy.Runtime.Localization.UiText.Get("Scanner disconnected. A pending scan was discarded.");
        }), scanner is not null, CanUseBarcodeBoy);
        DrawSettingsParagraph(635, 300, !CanUseBarcodeBoy ? global::AetherBoy.Runtime.Localization.UiText.Get("Open a GB game in single-player mode. Unavailable for GBA and link sessions.")
            : scanner is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Connect before the game checks for its scanner.")
            : scanner.Pending ? global::AetherBoy.Runtime.Localization.UiText.Format("Scan waiting or sending: {0}/30 bytes. Resume the game.", scanner.BytesSent)
            : scanner.Ready ? global::AetherBoy.Runtime.Localization.UiText.Format("Scanner recognized. Transfers completed: {0}.", scanner.CompletedScans)
            : global::AetherBoy.Runtime.Localization.UiText.Format("Waiting for the game to detect the scanner. Transfers completed: {0}.", scanner.CompletedScans), 475);
        DrawTextEntry(TextField.Barcode, 300, 390, 380, 42, global::AetherBoy.Runtime.Localization.UiText.Get("13-digit card code"));
        ActionButton(700, 390, 410, 42, global::AetherBoy.Runtime.Localization.UiText.Get("LOAD CODE FROM TEXT FILE"), () =>
        {
            if (IsLoading || Volatile.Read(ref fileDialogOpen) != 0) return;
            CommitActiveText(); pickingBarcode = true; ShowRomDialog();
        });
        ActionButton(300, 451, 310, 42, global::AetherBoy.Runtime.Localization.UiText.Get("SCAN CODE"), () => BarcodeAction(() =>
        {
            string code = BarcodeBoyInput.Normalize(barcodeInput);
            session!.ScanBarcodeBoyAsync(code).GetAwaiter().GetResult();
            CommitActiveText();
            barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Scan queued. Close this menu and resume the game. The scanner waits until the game is ready to receive.");
        }), enabled: CanUseBarcodeBoy && scanner is { Pending: false });
        DrawSettingsParagraph(635, 451, global::AetherBoy.Runtime.Localization.UiText.Get("Exactly 13 digits, sent unchanged. The game decides whether the card is valid."), 475);
        DrawSettingsParagraph(300, 513, barcodeFeedback, 810);
        ActionButton(300, 580, 390, 40, global::AetherBoy.Runtime.Localization.UiText.Get("USE BERSERKER CODE"), () => SetBarcodeExample(BarcodeBoyInput.BattleSpaceBerserker));
        ActionButton(710, 580, 400, 40, global::AetherBoy.Runtime.Localization.UiText.Get("USE VALKYRIE CODE"), () => SetBarcodeExample(BarcodeBoyInput.BattleSpaceValkyrie));
    }

    private void SetBarcodeExample(string code) { CommitActiveText(); barcodeInput = code; }

    private void BarcodeAction(Action action)
    {
        if (!CanUseBarcodeBoy) { barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Open a GB game in single-player mode first."); return; }
        try { action(); }
        catch (ArgumentException) { barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Invalid code. Enter exactly 13 digits from 0 to 9, with no spaces between them."); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or OperationCanceledException)
        { barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Action unavailable. Connect the scanner in a GB single-player session and wait for any pending scan to finish."); }
    }

    private void ImportBarcode(string path)
    {
        try { barcodeInput = BarcodeBoyInput.ReadFile(path); barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Code loaded. Select SCAN CODE to send it to the game."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { barcodeFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("Could not read the file. Choose UTF-8 text up to 128 bytes containing one 13-digit code."); }
    }
}
