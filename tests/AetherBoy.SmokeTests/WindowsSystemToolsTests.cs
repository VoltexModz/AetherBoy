using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsSystemToolsTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aetherboy-system-tools-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    [DataRow("False")]
    [DataRow("True")]
    public void GeneralSettingsResetPreservesRecordingChoiceAtomically(string choice)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["DiagnosticsRecording"] = choice, ["AudioVolume"] = "25"
        }));
        var provider = new WindowsSettingsProvider(path);
        provider.Initialize("test", new NameValueCollection());
        provider.Reset(new SettingsContext());
        var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        Assert.AreEqual(choice, saved["DiagnosticsRecording"]);
        Assert.AreEqual(1, saved.Count);
    }

    [TestMethod]
    public void ResetOfCorruptSettingsDoesNotSilentlyEnableDiagnostics()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, "{ broken");
        var provider = new WindowsSettingsProvider(path);
        provider.Initialize("test", new NameValueCollection());
        provider.Reset(new SettingsContext());
        var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        Assert.AreEqual("False", saved["DiagnosticsRecording"]);
    }

    [STATestMethod]
    public void DiagnosticsTogglePersistsNextStartWithoutPretendingCurrentRecordingChanged()
    {
        var settings = nanoboy.Properties.Settings.Default;
        string previous = settings.DiagnosticsRecording;
        try
        {
            settings.DiagnosticsRecording = "False";
            using var main = new frmNano();
            main.Show();
            var center = OpenCenter(main, "Diagnostics");
            var toggle = Find<AetherButton>(center, "controlCenterRecordNextSessionButton");
            Assert.IsFalse(toggle.Selected);
            Assert.IsFalse(Find<AetherButton>(center, "controlCenterExportTesterReportButton").Enabled);
            Assert.IsTrue(Find<AetherButton>(center, "controlCenterOpenTesterFolderButton").Enabled);
            Assert.IsFalse(Find<AetherButton>(center, "controlCenterMarkProblemButton").Enabled);
            toggle.PerformClick();
            Assert.IsTrue(toggle.Selected);
            Assert.AreEqual("True", settings.DiagnosticsRecording);
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(WindowsDataPaths.Default.SettingsFile))!;
            Assert.AreEqual("True", saved["DiagnosticsRecording"]);
            StringAssert.Contains(Find<Label>(center, "controlCenterRecordingStatus").Text, "KEINE SITZUNGSAUFZEICHNUNG");
            Assert.IsFalse(Find<AetherButton>(center, "controlCenterExportTesterReportButton").Enabled);
            Capture(center, "windows-diagnostics.png");
            center.Close();
            main.Close();
        }
        finally { settings.DiagnosticsRecording = previous; settings.Save(); }
    }

    [STATestMethod]
    public void MainWindowFirmwareEntryOpensCustomManager()
    {
        using var main = new frmNano();
        main.Show();
        var tools = Field<ToolStripMenuItem>(main, "menuItem21");
        Assert.IsTrue(tools.DropDownItems.ContainsKey("menuFirmwareManager"));
        var center = OpenCenter(main, "System");
        bool seen = false;
        Exception? failure = null;
        using var driver = new System.Windows.Forms.Timer { Interval = 20 };
        driver.Tick += (_, _) =>
        {
            if (Application.OpenForms.OfType<frmFirmwareManager>().FirstOrDefault() is not { } manager) return;
            driver.Stop();
            try
            {
                Assert.AreEqual(FormBorderStyle.None, manager.FormBorderStyle);
                foreach (string kind in new[] { "Dmg", "Cgb", "Gba" })
                    Assert.IsTrue(Find<AetherButton>(manager, "firmwareImport" + kind).Enabled);
                seen = true;
            }
            catch (Exception ex) { failure = ex; }
            finally { manager.Close(); }
        };
        driver.Start();
        Find<AetherButton>(center, "controlCenterFirmwareManagerButton").PerformClick();
        if (failure is not null) throw failure;
        Assert.IsTrue(seen);
        Capture(center, "windows-system.png");
        center.Close();
        main.Close();
    }

    [STATestMethod]
    public void MainLoaderRejectsInvalidFirmwareWithoutThrowingAndHonorsBypass()
    {
        WindowsDataPaths previousPaths = WindowsDataPaths.Default;
        var defaults = nanoboy.Properties.Settings.Default;
        bool previousBoot = defaults.BootRomEnable;
        try
        {
            WindowsDataPaths.Default = new WindowsDataPaths(root);
            var store = new WindowsFirmwareStore(WindowsDataPaths.Default);
            Directory.CreateDirectory(store.DirectoryPath);
            File.WriteAllBytes(store.PathFor(WindowsFirmwareKind.Dmg), [1, 2, 3]);
            defaults.BootRomEnable = true;
            using var main = new frmNano();
            Assert.IsNull(Call(main, "LoadBootROM", false, false));
            StringAssert.Contains(Field<string>(main, "firmwareLoadWarning"), "integrierter Start");
            byte[] firmware = new byte[256]; firmware[0] = 42;
            File.WriteAllBytes(store.PathFor(WindowsFirmwareKind.Dmg), firmware);
            CollectionAssert.AreEqual(firmware, (byte[])Call(main, "LoadBootROM", false, false)!);
            defaults.BootRomEnable = false;
            Assert.IsNull(Call(main, "LoadBootROM", false, false));
        }
        finally { WindowsDataPaths.Default = previousPaths; defaults.BootRomEnable = previousBoot; }
    }

    [STATestMethod]
    public void CopyableDiagnosticsExcludeRomFilenamePathAndFreeformFeedback()
    {
        Directory.CreateDirectory(root);
        string rom = Path.Combine(root, "PRIVATE_FILENAME_9241.gba");
        byte[] bytes = new byte[0x400];
        // Synthetic ARM self-loop with a blank title: runtime deliberately falls back to filename.
        new byte[] { 0xFE, 0xFF, 0xFF, 0xEA }.CopyTo(bytes, 0);
        File.WriteAllBytes(rom, bytes);
        var defaults = nanoboy.Properties.Settings.Default;
        bool audio = defaults.AudioEnable, boot = defaults.BootRomEnable;
        try
        {
            defaults.AudioEnable = defaults.BootRomEnable = false;
            using var main = new frmNano();
            main.Show(); main.LoadRomFile(rom);
            var session = Field<EmulationSession>(main, "session");
            PumpUntil(() => session.LatestSnapshot.Rom is not null);
            Call(main, "SetSaveFeedback", "PRIVATE_FEEDBACK_693 " + rom, false);
            var center = OpenCenter(main, "Diagnostics");
            string text = Find<RichTextBox>(center, "controlCenterDiagnosticsText").Text;
            StringAssert.Contains(text, "ROM SHA-256");
            Assert.IsFalse(text.Contains("PRIVATE_FILENAME_9241", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("PRIVATE_FEEDBACK_693", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains(root, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("ROM PATH", StringComparison.Ordinal));
            var preview = Find<RichTextBox>(center, "controlCenterDiagnosticsText");
            Assert.AreEqual(BorderStyle.None, preview.BorderStyle);
            Assert.AreEqual(RichTextBoxScrollBars.None, preview.ScrollBars);
            Call(center, "ScrollDiagnostics", 8);
            int line = preview.GetLineFromCharIndex(preview.GetCharIndexFromPosition(Point.Empty));
            Assert.IsTrue(line > 0, "Custom scroll actions must expose the remaining report.");
            Call(center, "RefreshDiagnostics");
            Assert.AreEqual(line, preview.GetLineFromCharIndex(preview.GetCharIndexFromPosition(Point.Empty)),
                "Live refresh must not jump away from the section being read.");
            center.Close(); main.Close();
        }
        finally { defaults.AudioEnable = audio; defaults.BootRomEnable = boot; }
    }

    private static frmControlCenter OpenCenter(frmNano main, string page)
    {
        Call(main, "OpenControlCenter"); Application.DoEvents();
        var center = Field<frmControlCenter>(main, "controlCenter");
        Find<AetherButton>(center, "controlCenterNav" + page).PerformClick();
        Application.DoEvents();
        return center;
    }
    private static T Find<T>(Control parent, string name) where T : Control => (T)parent.Controls.Find(name, true).Single();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] arguments) => owner.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, arguments);
    private static void PumpUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed.TotalSeconds > 10) Assert.Fail("UI did not reach the expected state.");
            Application.DoEvents(); Thread.Sleep(1);
        }
    }
    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        form.BringToFront(); form.Refresh(); Application.DoEvents();
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
        bitmap.Save(Path.Combine(directory, name));
    }
}
