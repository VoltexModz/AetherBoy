using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Diagnostics;
using nanoboy.Input;
using System.Windows.Forms;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsTesterSessionTests
{
    [TestMethod]
    public void TesterSession_RecordsPrivacySafeEventsAndExportsManualBundle()
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-tester-{Guid.NewGuid():N}");
        string reportPath = rootDirectory + ".zip";
        string logPath;

        try
        {
            using (var session = new WindowsTesterSession(rootDirectory))
            {
                logPath = session.LogFilePath;
                session.RecordRomLoadRequested(
                    @"C:\Users\PrivateName\Games\Pokemon Rocket Secret.gba");
                session.RecordRomStarted(
                    new RomSnapshot(
                        "POKEMON ROCKET",
                        "GBA Flash128",
                        16 * 1024 * 1024,
                        128 * 1024,
                        HasColorFeatures: false,
                        HasSuperGameBoyFeatures: false,
                        IsJapanese: false,
                        RomSha256: new string('A', 64),
                        new BatterySaveSnapshot(
                            IsEnabled: true,
                            ExpectedLength: 128 * 1024,
                            LoadedGeneration: 0,
                            InvalidPrimaryDetected: false)),
                    externalBootRom: false);
                session.RecordGamepadIfChanged(new HostGamepadState(
                    HostGamepadButtons.South,
                    deviceName: "Battletron Controller",
                    source: GamepadInputSource.WindowsGamingInput,
                    vendorId: 0x1234,
                    productId: 0x5678));
                session.RecordGamepadIfChanged(new HostGamepadState(
                    HostGamepadButtons.East,
                    deviceName: "Battletron Controller",
                    source: GamepadInputSource.WindowsGamingInput,
                    vendorId: 0x1234,
                    productId: 0x5678));
                session.RecordOperation("quick_save", 2, succeeded: true);
                session.RecordException(
                    "test.failure",
                    new InvalidOperationException(
                        @"Do not capture C:\Users\PrivateName\Games\Pokemon Rocket Secret.gba",
                        new UnauthorizedAccessException("PrivateName secret-token")));

                File.WriteAllText(Path.Combine(session.SessionDirectory, "private-notes.txt"), "Do not export");
                Assert.AreEqual(Path.GetFullPath(reportPath), session.CreateBundle(reportPath));
            }

            string[] lines = File.ReadAllLines(logPath);
            Assert.IsTrue(lines.Length >= 8);
            foreach (string line in lines)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                Assert.AreEqual(1, document.RootElement.GetProperty("schema_version").GetInt32());
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    document.RootElement.GetProperty("event").GetString()));
            }

            string log = string.Join('\n', lines);
            using JsonDocument startupEvent = JsonDocument.Parse(lines[0]);
            JsonElement startupDetails = startupEvent.RootElement.GetProperty("details");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                startupDetails.GetProperty("product_build").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                startupDetails.GetProperty("runtime_build").GetString()));
            Assert.AreNotEqual(
                Guid.Empty,
                startupDetails.GetProperty("product_binary_id").GetGuid());
            Assert.AreNotEqual(
                Guid.Empty,
                startupDetails.GetProperty("runtime_binary_id").GetGuid());
            StringAssert.Contains(log, "rom.load_requested");
            StringAssert.Contains(log, "rom.started");
            Assert.IsFalse(log.Contains("POKEMON ROCKET", StringComparison.Ordinal),
                "ROM titles can be filename fallbacks and must not enter shared reports.");
            StringAssert.Contains(log, new string('A', 64));
            StringAssert.Contains(log, "Battletron Controller");
            StringAssert.Contains(log, "application.closed");
            Assert.IsFalse(log.Contains("PrivateName", StringComparison.Ordinal));
            Assert.IsFalse(log.Contains("Pokemon Rocket Secret.gba", StringComparison.Ordinal));
            Assert.IsFalse(log.Contains("Do not capture", StringComparison.Ordinal));
            Assert.IsFalse(log.Contains("secret-token", StringComparison.Ordinal));
            StringAssert.Contains(log, "UnauthorizedAccessException");
            StringAssert.Contains(log, "causes");

            int gamepadEvents = lines.Count(line => line.Contains(
                "input.gamepad_changed",
                StringComparison.Ordinal));
            Assert.AreEqual(1, gamepadEvents, "Button changes must not spam device events.");

            using ZipArchive archive = ZipFile.OpenRead(reportPath);
            CollectionAssert.AreEquivalent(
                new[] { "README.txt", "session.jsonl" },
                archive.Entries.Select(entry => entry.FullName).ToArray());
            using var readme = new StreamReader(archive.GetEntry("README.txt")!.Open());
            StringAssert.Contains(readme.ReadToEnd(), "controller device names/IDs");
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
            File.Delete(reportPath);
        }
    }

    [TestMethod]
    public void TesterSession_RejectsArchiveInsideLiveSessionFolder()
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-tester-{Guid.NewGuid():N}");
        try
        {
            using var session = new WindowsTesterSession(rootDirectory);
            string invalidDestination = Path.Combine(session.SessionDirectory, "report.zip");
            Assert.ThrowsExactly<InvalidOperationException>(
                () => session.CreateBundle(invalidDestination));
            Assert.IsFalse(File.Exists(invalidDestination));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TesterModeArgument_CanBeCombinedWithStartupRom()
    {
        Assert.IsTrue(Program.IsTesterModeRequested(new[] { "--TESTER-MODE" }));
        Assert.AreEqual(ProductInfo.IsDevelopmentBuild, Program.IsTesterModeRequested(Array.Empty<string>()));

        MethodInfo startupMethod = typeof(Program).GetMethod(
            "TryGetStartupRom",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Missing startup ROM validator.");
        object?[] arguments =
        {
            new[] { "--tester-mode", @"C:\roms\tester.gba" },
            null
        };
        Assert.AreEqual(true, startupMethod.Invoke(null, arguments));
        Assert.AreEqual(Path.GetFullPath(@"C:\roms\tester.gba"), arguments[1]);
    }

    [STATestMethod]
    public void TesterMode_EnablesControlCenterReportActions()
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-tester-{Guid.NewGuid():N}");
        try
        {
            using var session = new WindowsTesterSession(rootDirectory);
            using var main = new frmNano(session);
            main.Show();
            try
            {
                MethodInfo open = typeof(frmNano).GetMethod(
                    "OpenControlCenter",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new AssertFailedException("Control Center entry point is missing.");
                open.Invoke(main, null);
                Application.DoEvents();

                Form center = typeof(frmNano)
                    .GetField("controlCenter", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(main) as Form
                    ?? throw new AssertFailedException("Control Center was not created.");
                try
                {
                    Button diagnosticsNav = center.Controls
                        .Find("controlCenterNavDiagnostics", true)
                        .Single() as Button
                        ?? throw new AssertFailedException("Diagnostics navigation is missing.");
                    diagnosticsNav.PerformClick();
                    Application.DoEvents();

                    Button export = center.Controls
                        .Find("controlCenterExportTesterReportButton", true)
                        .Single() as Button
                        ?? throw new AssertFailedException("Tester export action is missing.");
                    Button openFolder = center.Controls
                        .Find("controlCenterOpenTesterFolderButton", true)
                        .Single() as Button
                        ?? throw new AssertFailedException("Tester folder action is missing.");
                    nanoboy.Controls.AetherTextBox diagnostics = center.Controls
                        .Find("controlCenterDiagnosticsText", true)
                        .Single() as nanoboy.Controls.AetherTextBox
                        ?? throw new AssertFailedException("Diagnostics text is missing.");

                    Assert.IsTrue(export.Enabled);
                    Assert.IsTrue(openFolder.Enabled);
                    StringAssert.Contains(diagnostics.Text, "RECORDING LOCALLY");
                    StringAssert.Contains(diagnostics.Text, "session.jsonl");
                }
                finally
                {
                    center.Close();
                }
            }
            finally
            {
                main.Close();
            }
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
