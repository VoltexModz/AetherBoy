using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;

namespace nanoboy;

public partial class frmNano
{
    private bool sofaMode, sofaLibraryOpen, fullscreenBeforeSofa;
    private Controls.AetherButton? sofaGameMenu;

    private async Task OpenSofaLibraryAsync()
    {
        if (IsOnlineLink || stateOperationInProgress || romPreparation is not null || quickMenuOpen || sofaLibraryOpen || IsDisposed)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Beende die laufende Link-Sitzung oder warte auf die aktuelle Aktion, bevor du den Sofa-Modus öffnest."), true); return; }
        controlCenter?.Close();
        if (!sofaMode) { fullscreenBeforeSofa = immersiveFullscreen; sofaMode = true; SetImmersiveFullscreen(true); }
        if (sofaGameMenu != null) { sofaGameMenu.Visible = true; sofaGameMenu.BringToFront(); }
        var previousSession = session;
        bool wasPaused = previousSession?.LatestSnapshot.IsPaused ?? true;
        sofaLibraryOpen = true; quickMenuOpen = true; gamepadAwaitNeutral = true;
        ReleaseGamepadInput();
        string? selected = null;
        bool exit = false;
        try
        {
            if (previousSession is not null)
            {
                await previousSession.SetTurboAsync(false);
                await previousSession.SetPausedAsync(true);
            }
            if (IsDisposed) return;
            using var library = new frmSofaLibrary(previousSession is not null);
            library.ShowDialog(this);
            selected = library.SelectedRom; exit = library.ExitRequested || selected is null && previousSession is null;
        }
        catch (InvalidOperationException)
        { if (!IsDisposed) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Der Sofa-Modus konnte nicht geöffnet werden. Prüfe den Zustand des Spiels."), true); exit = true; }
        finally
        {
            if (!IsDisposed && ReferenceEquals(previousSession, session) && previousSession?.State == SessionState.Paused)
            {
                try { await previousSession.SetPausedAsync(wasPaused); } catch (InvalidOperationException) { }
            }
            sofaLibraryOpen = false; quickMenuOpen = false; gamepadAwaitNeutral = true;
        }
        if (IsDisposed) return;
        if (exit) ExitSofaMode();
        if (selected is not null && !string.Equals(selected, currentRomPath, StringComparison.OrdinalIgnoreCase))
            await PrepareRomLoadAsync(selected);
        if (sofaMode && session is null) ExitSofaMode();
        gameView.Focus();
    }

    private void ExitSofaMode()
    {
        if (!sofaMode) return;
        sofaMode = false;
        if (sofaGameMenu != null) sofaGameMenu.Visible = false;
        SetImmersiveFullscreen(fullscreenBeforeSofa);
        SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Sofa-Modus beendet. Deine vorherige Fensteransicht ist wiederhergestellt."), false);
    }
}
