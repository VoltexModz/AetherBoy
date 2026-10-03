using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy;
using nanoboy.Controls;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsUpdateTests
{
    private Dictionary<string, object> saved = null!;
    private Func<ReleaseUpdateService> factory = null!;
    private string root = null!;
    [TestInitialize] public void Setup()
    {
        var settings = nanoboy.Properties.Settings.Default;
        saved = settings.Properties.Cast<SettingsProperty>().ToDictionary(p => p.Name, p => settings[p.Name]);
        settings.UpdateCheckOnStartup = false; settings.UiScalePercent = 100;
        factory = frmNano.UpdateServiceFactory;
        root = Path.Combine(Path.GetTempPath(), "aetherboy-update-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    }
    [TestCleanup] public void Cleanup()
    {
        frmNano.UpdateServiceFactory = factory;
        foreach (var pair in saved) nanoboy.Properties.Settings.Default[pair.Key] = pair.Value;
        nanoboy.Properties.Settings.Default.Save(); Directory.Delete(root, true);
    }

    [STATestMethod]
    public void ThemedPageChecksDownloadsAndExplainsThatInstallationIsManual()
    {
        using var handler = new FakeHandler();
        frmNano.UpdateServiceFactory = () => new("4.8.0-alpha.1", "win-x64", true, root, new HttpClient(handler));
        using var main = new frmNano(); main.Show();
        Call(main, "OpenControlCenter"); var center = Field<frmControlCenter>(main, "controlCenter"); Application.DoEvents();
        Call(center, "ShowPage", "updates"); Application.DoEvents();
        Assert.AreEqual(0, handler.Calls);
        Assert.IsTrue(WindowsSettingsCatalog.Search("update").Any(e => e.Page == "updates"));
        Assert.IsFalse(Find<AetherButton>(center, "updateDownload").Enabled);
        Assert.IsTrue(Find<AetherButton>(center, "updateFolder").Enabled); // Older downloads remain discoverable after restarting.
        Find<AetherButton>(center, "updateCheck").PerformClick();
        var service = Field<ReleaseUpdateService>(main, "releaseUpdates");
        PumpUntil(() => service.Snapshot.State == ReleaseUpdateState.Available); Call(center, "RefreshAll");
        Assert.IsTrue(Find<AetherButton>(center, "updateDownload").Enabled);
        Find<AetherButton>(center, "updateDownload").PerformClick();
        PumpUntil(() => service.Snapshot.State == ReleaseUpdateState.Downloaded); Call(center, "RefreshAll");
        StringAssert.Contains(Find<Label>(center, "updateStatus").Text, "Noch nicht installiert");
        Assert.IsTrue(Find<AetherButton>(center, "updateFolder").Enabled);
        Assert.IsTrue(File.Exists(service.Snapshot.DownloadPath));
        var page = Find<Panel>(center, "controlCenterPageUpdates");
        foreach (Control control in page.Controls)
        {
            Assert.IsTrue(control.Right <= page.ClientSize.Width, control.Name);
            if (control is Label label && label.Width >= 700)
            {
                Size needed = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
                Assert.IsTrue(needed.Height <= label.Height, $"Clipped text: {label.Text}");
            }
        }
        using (var image = new Bitmap(center.Width, center.Height))
        { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppContext.BaseDirectory, "update-settings.png")); }
        center.Close(); main.Close();
    }

    [STATestMethod]
    public void StartupCheckNeedsGlobalOptInAndRunsOnlyOncePerLaunch()
    {
        using var handler = new FakeHandler();
        frmNano.UpdateServiceFactory = () => new("4.8.0-alpha.1", "win-x64", true, root, new HttpClient(handler));
        using (var main = new frmNano())
        { Call(main, "PollStartupUpdateCheck"); Assert.AreEqual(0, handler.Calls); }
        nanoboy.Properties.Settings.Default.UpdateCheckOnStartup = true;
        using var secondHandler = new FakeHandler();
        frmNano.UpdateServiceFactory = () => new("4.8.0-alpha.1", "win-x64", true, root, new HttpClient(secondHandler));
        using (var main = new frmNano())
        {
            Call(main, "PollStartupUpdateCheck");
            PumpUntil(() => Field<ReleaseUpdateService>(main, "releaseUpdates").Snapshot.State == ReleaseUpdateState.Available);
            Call(main, "PollStartupUpdateCheck"); Assert.AreEqual(1, secondHandler.Calls);
        }
    }

    [TestMethod]
    public void PreferenceIsOffByDefaultGlobalAndPersisted()
    {
        Assert.AreEqual("False", nanoboy.Properties.Settings.Default.Properties["UpdateCheckOnStartup"].DefaultValue);
        using var settings = new NanoboySettings(); settings.UpdateCheckOnStartup = true;
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult(); nanoboy.Properties.Settings.Default.Reload();
        Assert.IsTrue(settings.UpdateCheckOnStartup);
    }

    [TestMethod]
    public void RecoveryDoesNotRestoreAutomaticNetworkOptIn()
    {
        string file = Path.Combine(root, "settings.json"); File.WriteAllText(file, "{");
        File.WriteAllText(file + ".bak", "{\"UpdateCheckOnStartup\":\"True\"}");
        var provider = new nanoboy.Storage.WindowsSettingsProvider(file);
        var values = provider.GetPropertyValues(new SettingsContext(), nanoboy.Properties.Settings.Default.Properties);
        Assert.AreEqual("False", values["UpdateCheckOnStartup"].SerializedValue);
        StringAssert.Contains(frmControlCenter.DescribeUpdate(new(ReleaseUpdateState.NoReleases)), "noch kein");
        StringAssert.Contains(frmControlCenter.DescribeUpdate(new(ReleaseUpdateState.Failed, Error: ReleaseUpdateError.Network)), "unbekannt");
    }

    private static T Find<T>(Control root, string name) where T : Control => (T)root.Controls.Find(name, true).Single();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void PumpUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(5); }
        Assert.IsTrue(condition(), "Update did not complete in time.");
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        private int calls; public int Calls => Volatile.Read(ref calls);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            byte[] bytes = [1, 2, 3]; const string name = "AetherBoy-5.0.0-win-x64-self-contained.zip";
            HttpContent content = request.RequestUri!.Host == "api.github.com"
                ? new StringContent(JsonSerializer.Serialize(new[] { new { tag_name = "v5.0.0", draft = false, prerelease = false, assets = new[] { new
                    { name, state = "uploaded", size = 3, digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)), browser_download_url = $"{ReleaseUpdateService.ReleasesPage}/download/v5.0.0/{name}" } } } }))
                : new ByteArrayContent(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
