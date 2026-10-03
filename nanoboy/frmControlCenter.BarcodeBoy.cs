using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private AetherButton barcodeConnect = null!, barcodeScan = null!;
    private AetherTextBox barcodeInput = null!;
    private Label barcodeStatus = null!, barcodeFeedback = null!;
    private bool barcodeBusy;

    private void BuildBarcodeBoyPage()
    {
        var page = NewSection("barcode", "Barcode Boy", global::AetherBoy.Runtime.Localization.UiText.Get("Namcot-Kartenscanner für unterstützte Game-Boy-Spiele. Kein GBA e-Reader und kein Bardigun."));
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Zurück zu den Werkzeugen"), 0, 82, 380, () => ShowPage("tools"));
        barcodeConnect = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Scanner anschließen"), 0, 150, 380, () => { });
        barcodeConnect.Name = "barcodeConnect";
        barcodeConnect.Click += async (_, _) => await RunBarcodeAction(async () =>
        {
            bool enabled = bridge.SnapshotProvider()?.BarcodeBoy is null;
            if (bridge.SetBarcodeBoyEnabled is not { } set) throw new NotSupportedException();
            await set(enabled);
            barcodeFeedback.Text = enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Scanner angeschlossen. Öffne im Spiel die Barcode-Funktion.") : global::AetherBoy.Runtime.Localization.UiText.Get("Scanner getrennt. Ein wartender Scan wurde verworfen.");
        });
        barcodeStatus = CreateSmallLabel("", 0, 218, 780, 75); barcodeStatus.Name = "barcodeStatus"; page.Controls.Add(barcodeStatus);
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Kartencode: genau 13 Ziffern. Der Code wird unverändert gesendet; das Spiel entscheidet, ob es ihn akzeptiert."), 0, 304, 780, 60));
        barcodeInput = new AetherTextBox { Name = "barcodeInput", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Barcode-Boy-Kartencode"), PlaceholderText = global::AetherBoy.Runtime.Localization.UiText.Get("13 Ziffern"), MaxLength = 80, Bounds = new Rectangle(0, 375, 380, 38) };
        page.Controls.Add(barcodeInput);
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Code aus Textdatei laden"), 400, 370, 380, ImportBarcode).Name = "barcodeImport";
        barcodeScan = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Code scannen"), 0, 436, 380, () => { }); barcodeScan.Name = "barcodeScan";
        barcodeScan.Click += async (_, _) => await RunBarcodeAction(async () =>
        {
            string code = BarcodeBoyInput.Normalize(barcodeInput.Text);
            if (bridge.ScanBarcodeBoy is not { } scan) throw new NotSupportedException();
            await scan(code);
            barcodeFeedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Scan vorgemerkt. Schließe das Menü und setze das Spiel fort. Der Scanner wartet auf die Empfangsbereitschaft des Spiels.");
        });
        barcodeFeedback = CreateSmallLabel("", 0, 501, 780, 90); barcodeFeedback.Name = "barcodeFeedback"; page.Controls.Add(barcodeFeedback);
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Battle Space: dokumentierte Beispielkarten. Die Buttons tragen nur den Code ein. Der echte Spieltest steht noch aus."), 0, 608, 780, 65));
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Berserker-Code einsetzen"), 0, 683, 380, () => barcodeInput.Text = BarcodeBoyInput.BattleSpaceBerserker);
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Valkyrie-Code einsetzen"), 400, 683, 380, () => barcodeInput.Text = BarcodeBoyInput.BattleSpaceValkyrie);
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Spiele: Battle Space, Monster Maker: Barcode Saga, Kattobi Road, Family Jockey 2 und Famista 3. Nur im Einzelspiel; der Anschluss kann nicht gleichzeitig als Link-Kabel dienen."), 0, 754, 780, 95));
        RefreshBarcodeBoy();
    }

    private async Task RunBarcodeAction(Func<Task> action)
    {
        if (barcodeBusy) return;
        barcodeBusy = true; RefreshBarcodeBoy();
        try { await action(); }
        catch (ArgumentException) { if (!IsDisposed) barcodeFeedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ungültiger Code. Gib genau 13 Ziffern von 0 bis 9 ein, ohne Leerzeichen dazwischen."); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or OperationCanceledException)
        { if (!IsDisposed) barcodeFeedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Aktion ist nicht verfügbar. Öffne ein GB-Spiel im Einzelmodus, schließe den Scanner an und warte auf den Abschluss eines laufenden Scans."); }
        finally { barcodeBusy = false; if (!IsDisposed) RefreshBarcodeBoy(); }
    }

    private void ImportBarcode()
    {
        using var picker = new AetherFileDialog { Title = global::AetherBoy.Runtime.Localization.UiText.Get("Barcode-Textdatei auswählen"), Filter = "Textdatei|*.txt", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try { barcodeInput.Text = BarcodeBoyInput.ReadFile(picker.FileName); barcodeFeedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Code geladen. Mit „Code scannen“ an das Spiel senden."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { barcodeFeedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei konnte nicht gelesen werden. Verwende eine UTF-8-Textdatei bis 128 Byte mit genau einem 13-stelligen Code."); }
    }

    private void RefreshBarcodeBoy()
    {
        if (barcodeConnect is null) return;
        var snapshot = bridge.SnapshotProvider();
        bool supported = snapshot?.Supports(EmulationFeature.BarcodeBoy) == true;
        var scanner = snapshot?.BarcodeBoy;
        barcodeConnect.Enabled = supported && !barcodeBusy && bridge.SetBarcodeBoyEnabled is not null;
        barcodeConnect.Text = scanner is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Scanner anschließen") : global::AetherBoy.Runtime.Localization.UiText.Get("Scanner trennen");
        barcodeConnect.Selected = scanner is not null;
        barcodeScan.Enabled = supported && scanner is { Pending: false } && !barcodeBusy && bridge.ScanBarcodeBoy is not null;
        barcodeStatus.Text = !supported ? global::AetherBoy.Runtime.Localization.UiText.Get("Öffne ein GB-Spiel im Einzelmodus. Barcode Boy ist nicht für GBA oder Link-Sitzungen verfügbar.")
            : scanner is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Scanner getrennt. Schließe ihn an, bevor das Spiel nach dem Gerät sucht.")
            : scanner.Pending ? global::AetherBoy.Runtime.Localization.UiText.Format("Scan wartet oder wird übertragen: {0}/30 Bytes. Setze das Spiel fort.", scanner.BytesSent)
            : scanner.Ready ? global::AetherBoy.Runtime.Localization.UiText.Format("Spiel hat den Scanner erkannt. Abgeschlossene Übertragungen: {0}.", scanner.CompletedScans)
            : global::AetherBoy.Runtime.Localization.UiText.Format("Warte auf die Geräteerkennung im Spiel. Abgeschlossene Übertragungen: {0}.", scanner.CompletedScans);
    }
}
