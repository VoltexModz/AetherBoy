using System.Diagnostics;
using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    internal static Func<string, ReleaseUpdateService> UpdateServiceFactory { get; set; } = directory =>
        new(LinuxBuildInfo.Version, LinuxBuildInfo.Channel == "development", directory);
    private readonly ReleaseUpdateService releaseUpdates;
    private bool startupUpdateChecked;

    private void PollStartupUpdateCheck()
    {
        if (startupUpdateChecked) return;
        startupUpdateChecked = true;
        if (options.UpdateCheckOnStartup) _ = releaseUpdates.CheckAsync();
    }

    private void DrawUpdateSettings()
    {
        var status = releaseUpdates.Snapshot;
        Ink(510, 203, "Updates", 22, bold: true);
        ActionButton(780, 194, 330, 40, options.UpdateCheckOnStartup ? global::AetherBoy.Runtime.Localization.UiText.Get("Check on startup: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Check on startup: off"), () =>
        { options.UpdateCheckOnStartup = !options.UpdateCheckOnStartup; MarkSettingsChanged(); }, options.UpdateCheckOnStartup, focusId: "updates:startup");
        string version = LinuxBuildInfo.Version.Split('+')[0];
        Ink(300, 255, textRenderer.Fit(global::AetherBoy.Runtime.Localization.UiText.Format("Installed: {0} · ", version) + (releaseUpdates.IncludePrereleases ? global::AetherBoy.Runtime.Localization.UiText.Get("Includes preview releases") : global::AetherBoy.Runtime.Localization.UiText.Get("Stable releases only")), 805, 16), 16);
        DrawSettingsParagraph(300, 286, global::AetherBoy.Runtime.Localization.UiText.Get("Checking contacts GitHub, which can see your IP address. No ROMs, saves or account data are sent. Downloads only start when you choose Download."), 805, 14);
        ActionButton(300, 357, 240, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Check now"), () => { _ = releaseUpdates.CheckAsync(); }, enabled: !status.Busy, focusId: "updates:check");
        ActionButton(563, 357, 310, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Download package"), () => { _ = releaseUpdates.DownloadAsync(); }, enabled: status.State == ReleaseUpdateState.Available, focusId: "updates:download");
        ActionButton(896, 357, 214, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Cancel"), releaseUpdates.Cancel, enabled: status.Busy, focusId: "updates:cancel");
        DrawSettingsParagraph(300, 416, DescribeUpdate(status), 805, 14, Colors.Text);
        string package = status.Version is null ? global::AetherBoy.Runtime.Localization.UiText.Get("No comparison of unpublished Git commits.")
            : $"Release: {status.Version}" + (status.PackageSize > 0 ? $" ({status.PackageSize / 1048576d:0.0} MiB)" : "");
        Ink(300, 481, textRenderer.Fit(package, 805, 14), 14, Colors.Cyan);
        ActionButton(300, 516, 390, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Open download folder"), () =>
        {
            OpenFolder(releaseUpdates.Snapshot.DownloadPath is { } path ? Path.GetDirectoryName(path)! : releaseUpdates.DownloadDirectory);
        }, focusId: "updates:folder");
        ActionButton(710, 516, 400, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Open releases on GitHub"), () => TryUiAction(() =>
            Process.Start(new ProcessStartInfo(ReleaseUpdateService.ReleasesPage) { UseShellExecute = true }), global::AetherBoy.Runtime.Localization.UiText.Get("Browser launch requested.")), focusId: "updates:releases");
        DrawSettingsParagraph(300, 572, global::AetherBoy.Runtime.Localization.UiText.Get("SHA-256 checks GitHub's digest, not a publisher signature. No automatic install: extract into a new folder. Keep the old folder and portable game data."), 805, 14);
    }

    internal static string DescribeUpdate(ReleaseUpdateSnapshot status) => status.State switch
    {
        ReleaseUpdateState.Checking => global::AetherBoy.Runtime.Localization.UiText.Get("Checking published releases..."),
        ReleaseUpdateState.NoReleases => global::AetherBoy.Runtime.Localization.UiText.Get("No usable release is published for this channel yet. New Git commits are not compared."),
        ReleaseUpdateState.NoNewerRelease => global::AetherBoy.Runtime.Localization.UiText.Get("No release with a higher version number found. Local changes and Git commits are not compared."),
        ReleaseUpdateState.NoPackage => global::AetherBoy.Runtime.Localization.UiText.Get("A newer release exists, but no unique package with a valid SHA-256 digest is available for this system."),
        ReleaseUpdateState.Available => global::AetherBoy.Runtime.Localization.UiText.Get("A newer version is available. Choose Download package to continue."),
        ReleaseUpdateState.Downloading => global::AetherBoy.Runtime.Localization.UiText.Format("Downloading: {0:0.0} of {1:0.0} MiB...", status.ReceivedBytes / 1048576d, status.PackageSize / 1048576d),
        ReleaseUpdateState.Downloaded => global::AetherBoy.Runtime.Localization.UiText.Get("Download complete. Size and SHA-256 match GitHub's metadata. Not installed yet."),
        ReleaseUpdateState.Cancelled => global::AetherBoy.Runtime.Localization.UiText.Get("Cancelled. Nothing was installed. Check again to retry."),
        ReleaseUpdateState.Failed => status.Error switch
        {
            ReleaseUpdateError.RateLimit => global::AetherBoy.Runtime.Localization.UiText.Get("GitHub is limiting requests. Please check again later."),
            ReleaseUpdateError.Timeout => global::AetherBoy.Runtime.Localization.UiText.Get("The request took too long. Check your connection and retry."),
            ReleaseUpdateError.Integrity => global::AetherBoy.Runtime.Localization.UiText.Get("Size or checksum mismatch. This package cannot be used. Check again or report it to the team."),
            ReleaseUpdateError.UnsafeRedirect => global::AetherBoy.Runtime.Localization.UiText.Get("Download stopped: GitHub redirected to an unapproved destination. Please report this to the team."),
            ReleaseUpdateError.UnsupportedRuntime => global::AetherBoy.Runtime.Localization.UiText.Get("Updates do not support this operating system and process architecture yet."),
            ReleaseUpdateError.UnknownVersion => global::AetherBoy.Runtime.Localization.UiText.Get("The installed version number cannot be compared. Please report this to the team."),
            ReleaseUpdateError.InvalidCatalog or ReleaseUpdateError.CatalogLimit => global::AetherBoy.Runtime.Localization.UiText.Get("The release list could not be safely evaluated. Please check the releases on GitHub."),
            ReleaseUpdateError.Disk => global::AetherBoy.Runtime.Localization.UiText.Get("Download or file write failed. Check your connection, free disk space and write permissions."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("GitHub could not be read. Check your connection and retry. The update status is unknown.")
        },
        _ => global::AetherBoy.Runtime.Localization.UiText.Get("Not checked yet. Choose Check now.")
    };
}
