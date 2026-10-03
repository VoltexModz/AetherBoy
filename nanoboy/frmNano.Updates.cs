using System;
using AetherBoy.Runtime;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    internal static Func<ReleaseUpdateService> UpdateServiceFactory { get; set; } = () =>
        new(ProductInfo.Version, ProductInfo.IsDevelopmentBuild, WindowsDataPaths.Default.Updates);
    private readonly ReleaseUpdateService releaseUpdates = UpdateServiceFactory();
    private bool startupUpdateChecked;

    private void PollStartupUpdateCheck()
    {
        if (startupUpdateChecked) return;
        startupUpdateChecked = true;
        if (settings.UpdateCheckOnStartup) _ = releaseUpdates.CheckAsync();
    }
}
