using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private AetherBootIntro? bootIntro;
    internal static BootIntroStore BootIntroPreferences => new(Path.Combine(WindowsDataPaths.Default.Root, "BootIntro"));

    private async Task PlayGameIntroAsync(CancellationToken cancellationToken, System.Windows.Forms.Control? previewParent = null)
    {
        if (!Visible || IsOnlineLink || bootIntro is not null || (previewParent is null && !BootIntroPreferences.Load().Enabled)) return;
        var previous = session;
        bool resume = previous is not null && !previous.LatestSnapshot.IsPaused;
        input.Clear(); turboPressed = false;
        if (previous is not null)
        {
            await previous.SetTurboAsync(false);
            await previous.SetPausedAsync(true);
        }
        Volatile.Read(ref audioOutput)?.SetSuspended(true);
        try
        {
            await AetherBootIntro.PlayAsync(previewParent ?? this, BootIntroPreferences, settings.AudioEnable ? settings.AudioVolume/100f : 0,
                cancellationToken, overlay => bootIntro = overlay);
        }
        finally
        {
            gamepadAwaitNeutral = true; input.Clear();
            if (resume && ReferenceEquals(previous, session) && !IsDisposed && !Disposing)
            {
                if (settings.PauseOnFocusLoss && System.Windows.Forms.Form.ActiveForm != this) focusPausedSession = previous;
                else await previous!.SetPausedAsync(false);
            }
        }
    }
}
