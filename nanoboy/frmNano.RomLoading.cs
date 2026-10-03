using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AetherBoy.Runtime.Cartridges;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private CancellationTokenSource? romPreparation;
    private int romPreparationGeneration;

    internal Task PrepareStartupRomAsync(string path) => PrepareRomLoadAsync(path);

    private void OpenWindowsPatchLab()
    {
        using var patcher = new frmRomPatcher(currentRomPath);
        patcher.ShowDialog(controlCenter ?? (System.Windows.Forms.IWin32Window)this);
        if (!patcher.LaunchRequested || patcher.ImportedRomPath is not string path) return;
        if (IsOnlineLink)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Beende Online Link, bevor du das gepatchte Spiel startest. Es bleibt in der Bibliothek."), true); return; }
        controlCenter?.Close();
        _ = PrepareRomLoadAsync(path);
    }

    private async Task PrepareRomLoadAsync(string sourcePath, bool resume = false)
    {
        if (stateOperationInProgress)
        {
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Warte, bis die Speicheraktion beendet ist, und öffne das Spiel dann erneut."), true);
            return;
        }

        CancelRomPreparation();
        var source = new CancellationTokenSource();
        romPreparation = source;
        int generation = ++romPreparationGeneration;
        SetRomPreparationButtons(true);
        SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel wird vorbereitet. Das bisherige Spiel läuft weiter."), false);
        testerSession?.RecordRomLoadRequested(sourcePath);
        try
        {
            string originalPath = Path.GetFullPath(sourcePath);
            string importedPath;
            RomArchiveChoice? choice = null;
            while (true)
            {
                try
                {
                    importedPath = await Task.Run(() =>
                    {
                        using var prepared = RomArchiveSource.Open(originalPath, WindowsDataPaths.Default.Roms, source.Token, choice);
                        string managed = WindowsRomLibrary.Default.Import(prepared.Path, source.Token);
                        ValidatePreparedRom(prepared.Path, managed, source.Token);
                        return managed;
                    }, source.Token);
                    break;
                }
                catch (RomArchiveSelectionRequiredException selection)
                {
                    if (source.IsCancellationRequested || generation != romPreparationGeneration || IsDisposed) return;
                    using var picker = new frmArchiveChoice(selection.Choices);
                    if (picker.ShowDialog(this) != System.Windows.Forms.DialogResult.OK || picker.SelectedChoice is null)
                    { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Keine ROM ausgewählt. Das bisherige Spiel bleibt geöffnet."), false); return; }
                    choice = picker.SelectedChoice;
                    source.Token.ThrowIfCancellationRequested();
                }
            }
            if (source.IsCancellationRequested || generation != romPreparationGeneration || IsDisposed)
                return;

            await PlayGameIntroAsync(source.Token);
            if (source.IsCancellationRequested || generation != romPreparationGeneration || IsDisposed) return;
            settings.RecentFiles.RemoveAll(candidate =>
                string.Equals(candidate, originalPath, StringComparison.OrdinalIgnoreCase));
            // Only the UI owner may stop the old session or install the newly prepared one.
            StartImportedRomFile(importedPath, resume);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (generation == romPreparationGeneration && !IsDisposed)
                SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Laden abgebrochen. Das bisherige Spiel läuft weiter."), false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or NotSupportedException or SharpCompress.Common.ArchiveException)
        {
            if (generation != romPreparationGeneration || IsDisposed) return;
            testerSession?.RecordException("rom.import_failed", exception);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel konnte nicht vorbereitet werden. Das bisherige Spiel läuft weiter."), true);
            string archiveHelp = RomArchiveSource.IsArchive(sourcePath)
                ? global::AetherBoy.Runtime.Localization.UiText.Get("\nZIP und 7z werden unterstützt. Bei einem Passwort, einem beschädigten oder geänderten Archiv entpacke die gewünschte ROM oder öffne das Archiv erneut.\n\nTechnische Details: ")
                : "\n\n";
            AetherSignal.Show(this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel konnte nicht vorbereitet werden. Das bisherige Spiel läuft weiter.") + archiveHelp + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message),
                global::AetherBoy.Runtime.Localization.UiText.Get("Spiel nicht geöffnet"), System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
        finally
        {
            if (ReferenceEquals(romPreparation, source))
            {
                romPreparation = null;
                if (!IsDisposed) SetRomPreparationButtons(false);
            }
            source.Dispose();
        }
    }

    private void CancelRomPreparation()
    {
        if (romPreparation is null) return;
        romPreparation.Cancel();
        romPreparation = null;
        ++romPreparationGeneration;
        if (!IsDisposed)
        {
            SetRomPreparationButtons(false);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Laden abgebrochen. Das bisherige Spiel läuft weiter."), false);
        }
    }

    private void SetRomPreparationButtons(bool preparing)
    {
        if (!aetherShellInitialized) return;
        string label = preparing ? global::AetherBoy.Runtime.Localization.UiText.Get("Laden abbrechen") : global::AetherBoy.Runtime.Localization.UiText.Get("Spiel öffnen");
        aetherOpenButton.Text = label;
        aetherDeckOpenButton.Text = label;
    }

    private bool TryRecoverPreviousRom(string previousPath)
    {
        StartImportedRomFile(previousPath, resume: false, allowRecovery: false, showFailure: false);
        return session != null && string.Equals(currentRomPath, previousPath, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ValidatePreparedRom(string sourcePath, string managedPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool sourceAdvance = Path.GetExtension(sourcePath).Equals(".gba", StringComparison.OrdinalIgnoreCase);
        bool managedAdvance = Path.GetExtension(managedPath).Equals(".gba", StringComparison.OrdinalIgnoreCase);
        if (sourceAdvance != managedAdvance)
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Diese ROM liegt in der Bibliothek bereits mit einer anderen System-Endung. Prüfe die vorhandene ROM-Datei."));

        // Metadata validation only: no session owner, save lease or battery writer is started.
        if (managedAdvance)
            _ = GbaRomInfo.Read(managedPath);
        else
        {
            var cartridge = new ROM(managedPath, string.Empty);
            cartridge.MBC.Dispose();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
