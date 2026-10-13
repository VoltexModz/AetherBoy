using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LocalLinkSession? messageLocalLinkOwner;
    private bool messageResumeLocalLink;

    private void PauseLocalLinkForMessageDetails()
    {
        messageLocalLinkOwner = localLinkSession;
        messageResumeLocalLink = false;
        if (localLinkSession is not { State: SessionState.Running } active) return;
        messageResumeLocalLink = true;
        active.SetPausedAsync(true).GetAwaiter().GetResult();
        localLinkAudioMixer.Clear();
        audioOutput?.Clear();
    }

    private void RestoreLocalLinkFromMessageDetails()
    {
        if (messageResumeLocalLink && ReferenceEquals(localLinkSession, messageLocalLinkOwner)
            && localLinkSession is { State: SessionState.Paused } active)
        {
            if (!windowFocused) resumeLocalLinkAfterFocus = true;
            else if (controlCenterVisible) resumeLocalLinkAfterSettings = true;
            else _ = active.SetPausedAsync(false);
        }
        messageResumeLocalLink = false;
        messageLocalLinkOwner = null;
    }
}
