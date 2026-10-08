using System.Reflection;
using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using AetherBoy.Testing;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxBarcodeBoyTests
{
    [TestMethod]
    [DataRow(BarcodeBoyInput.BattleSpaceBerserker)]
    [DataRow(BarcodeBoyInput.BattleSpaceValkyrie)]
    public void RealBattleSpaceCardArrivesThroughWaylandScannerButton(string code)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a Wayland session."); return; }
        using var f = new BattleSpaceFixture(); f.BootToScan();
        string settings = Path.Combine(f.Root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DiscordPresenceEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            using var session = new EmulationSession(f.RomPath, Path.Combine(f.Root, "ui.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            Assert.IsTrue(SpinWait.SpinUntil(() => session.State == SessionState.Running, TimeSpan.FromSeconds(5)));
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            session.RestoreStateAsync(SaveState.Capture(f.Emulator)).GetAwaiter().GetResult();
            Set(host, "session", session);
            Call(host, "ToggleControlCenter"); Call(host, "OpenSettingsDestination", LinuxSettingsDestination.BarcodeBoy);
            Call(host, "BeginTextEditing", Enum.Parse(host.GetType().GetNestedType("TextField", BindingFlags.NonPublic)!, "Barcode"));
            Call(host, "ReceiveTextInput", code); Call(host, "CommitActiveText"); Call(host, "DrawShell");
            Click(host, "SCAN CODE");
            Assert.IsTrue(session.LatestSnapshot.BarcodeBoy!.Pending); Assert.IsTrue(session.LatestSnapshot.IsPaused);
            session.SetPausedAsync(false).GetAwaiter().GetResult();
            Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.BarcodeBoy!.CompletedScans == 1 &&
                BattleSpaceFixture.SessionShowsCard(session, code), TimeSpan.FromSeconds(5)));
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            BattleSpaceFixture.AssertImage(BattleSpaceFixture.SessionImage(session), code, "wayland-ui-" + code);
            Assert.IsFalse(session.LatestSnapshot.BarcodeBoy!.Pending);
        }
        finally { SDL.Quit(); }
    }

    [TestMethod]
    public void SearchFindsScannerWithoutTreatingItAsGbaReader() =>
        Assert.IsTrue(LinuxSettingsCatalog.Search("barcode").Any(e => e.Destination == LinuxSettingsDestination.BarcodeBoy));

    [TestMethod]
    [DataRow(14)]
    [DataRow(18)]
    public void NativeScannerPageAcceptsTextAndQueuesIntoPausedSession(int textSize)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aether-linux-barcode-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string settings = Path.Combine(root, "settings.json"); LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DiscordPresenceEnabled = false, TextSize = textSize });
            SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "ToggleControlCenter"); Call(host, "OpenSettingsDestination", LinuxSettingsDestination.BarcodeBoy); Call(host, "DrawShell");
                Assert.IsTrue(Field<bool>(host, "showBarcodeBoy"));
                Click(host, "CONNECT SCANNER"); Assert.IsNull(Field<EmulationSession?>(host, "session"));
                string path = Path.Combine(root, "idle.gb"); byte[] rom = new byte[32768]; rom[0x100] = 0x18; rom[0x101] = 0xFE; File.WriteAllBytes(path, rom);
                using var session = new EmulationSession(path, Path.Combine(root, "idle.sav"), null, new EmulatorConfiguration(0, false, true, true, true, true, 44100));
                Assert.IsTrue(SpinWait.SpinUntil(() => session.State == SessionState.Running, TimeSpan.FromSeconds(5)));
                session.SetPausedAsync(true).GetAwaiter().GetResult(); Set(host, "session", session); Call(host, "DrawShell");
                Click(host, "CONNECT SCANNER"); Assert.IsNotNull(session.LatestSnapshot.BarcodeBoy);
                Call(host, "BeginTextEditing", Enum.Parse(host.GetType().GetNestedType("TextField", BindingFlags.NonPublic)!, "Barcode"));
                Call(host, "ReceiveTextInput", BarcodeBoyInput.BattleSpaceBerserker); Call(host, "CommitActiveText");
                Assert.AreEqual(BarcodeBoyInput.BattleSpaceBerserker, Field<string>(host, "barcodeInput"));
                string card = Path.Combine(root, "card.txt"); File.WriteAllText(card, BarcodeBoyInput.BattleSpaceValkyrie);
                DeliverFile(host, card);
                Assert.AreEqual(BarcodeBoyInput.BattleSpaceValkyrie, Field<string>(host, "barcodeInput"));
                Assert.AreSame(session, Field<EmulationSession>(host, "session"), "A barcode file must not be opened as a ROM.");
                DeliverFile(host, null); Assert.AreEqual(BarcodeBoyInput.BattleSpaceValkyrie, Field<string>(host, "barcodeInput"));
                File.WriteAllText(card, "bad"); DeliverFile(host, card);
                Assert.AreEqual(BarcodeBoyInput.BattleSpaceValkyrie, Field<string>(host, "barcodeInput"), "Invalid import preserves the previous code.");
                Call(host, "DrawShell"); Click(host, "SCAN CODE"); Assert.IsTrue(session.LatestSnapshot.BarcodeBoy!.Pending); Assert.IsTrue(session.LatestSnapshot.IsPaused);
                Call(host, "DrawShell");
                string? output = Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output); IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null); Assert.AreNotEqual(IntPtr.Zero, surface);
                    try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(output, textSize == 14 ? "barcode-boy-linux.png" : "barcode-boy-linux-large.png")), SDL.GetError()); } finally { SDL.DestroySurface(surface); }
                }
                Click(host, "DISCONNECT SCANNER"); Assert.IsNull(session.LatestSnapshot.BarcodeBoy);
                Call(host, "BackFromSettings"); Assert.IsFalse(Field<bool>(host, "showBarcodeBoy")); Assert.IsFalse(Field<bool>(host, "editingBarcode"));
            }
            finally { SDL.Quit(); }
        }
        finally { Directory.Delete(root, true); }
    }
    private static void DeliverFile(WaylandEmulatorHost host, string? path)
    {
        Set(host, "pickingBarcode", true); Set(host, "fileDialogOpen", 1);
        IntPtr text = path is null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(path), list = Marshal.AllocHGlobal(IntPtr.Size * 2);
        try
        {
            Marshal.WriteIntPtr(list, text); Marshal.WriteIntPtr(list, IntPtr.Size, IntPtr.Zero);
            Call(host, "OnFileDialogCompleted", IntPtr.Zero, list, 0);
            Assert.AreEqual(1, Field<int>(host, "fileDialogOpen")); Call(host, "DrainDialogSelections");
            Assert.AreEqual(0, Field<int>(host, "fileDialogOpen")); Assert.IsFalse(Field<bool>(host, "pickingBarcode"));
        }
        finally { if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); Marshal.FreeHGlobal(list); }
    }
    private static void Click(WaylandEmulatorHost host, string label)
    {
        // Pointer route also exercises enabled gates and the same callback as controller navigation.
        float x = label == "SCAN CODE" ? 400 : 450;
        Call(host, "HandleMouseClick", x, label == "SCAN CODE" ? 471f : 322f);
    }
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
    private static void Set(object o, string n, object value) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(o, value);
    private static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
}
