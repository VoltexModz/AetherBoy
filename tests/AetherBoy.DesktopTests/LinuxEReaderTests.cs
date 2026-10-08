using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxEReaderTests
{
    [TestMethod]
    [DataRow("de", 14, false)]
    [DataRow("en", 18, true)]
    public void ReaderPickerRoutesCardsWithoutReloadingRomAndPreservesPause(string language, int textSize, bool light)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aether-linux-reader-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string oldLanguage = UiText.Language;
        try
        {
            string settings = Path.Combine(root, "settings.json");
            var options = new LinuxFrontendOptions { AudioEnabled = false, DiscordPresenceEnabled = false, TextSize = textSize };
            if (light) { options.UiPrimaryColor = "#FF8000"; options.UiSecondaryColor = "#0066CC"; options.UiBackgroundColor = "#F7F7F7"; }
            LinuxSettingsStore.Save(settings, options);
            SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true); UiText.Initialize(language);
                Call(host, "ToggleControlCenter"); Call(host, "OpenSettingsDestination", LinuxSettingsDestination.EReader); Call(host, "DrawShell");
                Assert.IsTrue(Field<bool>(host, "showEReader"));
                Assert.IsTrue(LinuxSettingsCatalog.Search("e-Reader").Any(e => e.Destination == LinuxSettingsDestination.EReader));
                Call(host, "HandleMouseClick", 900f, 387f); Assert.IsNull(Field<EmulationSession?>(host, "session"));
                string rom = Path.Combine(root, "reader.gba"), card = Path.Combine(root, "card.raw");
                byte[] bytes = new byte[512]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xEAFFFFFE); Encoding.ASCII.GetBytes("PSAE").CopyTo(bytes, 0xAC);
                File.WriteAllBytes(rom, bytes); File.WriteAllBytes(card, new byte[2912]);
                using var session = new EmulationSession(rom, Path.Combine(root, "reader.sav"), null, new(0, false, true, true, true, true, 44100));
                Assert.IsTrue(SpinWait.SpinUntil(() => session.State == SessionState.Running, TimeSpan.FromSeconds(5)));
                session.SetPausedAsync(true).GetAwaiter().GetResult(); Set(host, "session", session);
                DeliverFile(host, session, card); Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards);
                Capture(host, language + "-queued");
                DeliverFile(host, session, null); Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards);
                File.WriteAllBytes(card, new byte[13]); DeliverFile(host, session, card);
                Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards); Assert.IsTrue(session.LatestSnapshot.IsPaused); Assert.IsNull(session.Fault);
                Assert.AreSame(session, Field<EmulationSession>(host, "session"));
                Assert.AreEqual(UiText.Get("Karte nicht übernommen. Prüfe Dateiformat, freie Warteschlangenplätze und die geöffnete e-Reader-ROM."), Field<string>(host, "eReaderFeedback"));
                Call(host, "DrawShell");
                Capture(host, language);
                Call(host, "HandleMouseClick", 900f, 387f); Assert.AreEqual(0, session.LatestSnapshot.EReader!.QueuedCards);
                Assert.IsTrue(session.LatestSnapshot.IsPaused);
                File.WriteAllBytes(card, new byte[2912]);
                var paths = Field<LinuxDataPaths>(host, "dataPaths");
                var cardLibrary = new EReaderLibrary(Path.Combine(paths.Data, "ereader"));
                cardLibrary.SetFirmware(rom);
                var first = cardLibrary.Import(card); cardLibrary.Rename(first.Id, "Donkey Kong-e · Test");
                var second = cardLibrary.Import(card); cardLibrary.Rename(second.Id, "Balloon Fight-e · Test");
                string firstPath = cardLibrary.PrepareLaunch(first.Id), secondPath = cardLibrary.PrepareLaunch(second.Id);
                using var firstStorage = LinuxRomStorage.OpenIdentified(paths, firstPath, cardLibrary.IdentifyLaunch(firstPath, true)!, default);
                using var secondStorage = LinuxRomStorage.OpenIdentified(paths, secondPath, cardLibrary.IdentifyLaunch(secondPath, true)!, default);
                Assert.AreNotEqual(firstStorage.SavePath, secondStorage.SavePath);
                Assert.AreNotEqual(firstStorage.StateBasePath, secondStorage.StateBasePath);
                LinuxStateGallery.Write(firstStorage.StateBasePath, 0, session.CaptureStateAsync().GetAwaiter().GetResult(), 240, 160, Enumerable.Repeat(unchecked((int)0xFF8B38FF), 240 * 160).ToArray());
                Assert.IsFalse(LinuxStateGallery.ReadPreview(secondStorage.StateBasePath).Exists);
                Call(host, "ReloadEReaderLibrary", first.Id); Set(host, "showEReaderLibrary", true);
                Call(host, "DrawShell");
                DateTime until = DateTime.UtcNow.AddSeconds(5);
                while (Field<object?>(host, "libraryPreviewLoad") is not null && DateTime.UtcNow < until)
                { Thread.Sleep(5); Call(host, "DrawShell"); }
                Assert.IsNotNull(LinuxStateGallery.ReadPreview(firstStorage.StateBasePath).Pixels);
                Capture(host, "library-" + language);
                // Existing theme tests validate every preset; this path checks dark/light and large text.
                Call(host, "SelectEReaderSet", 0); Call(host, "DrawShell");
                Assert.AreEqual(2, Field<IReadOnlyList<EReaderLibraryEntry>>(host, "eReaderSets").Count);
                Call(host, "BackFromSettings"); Assert.IsFalse(Field<bool>(host, "showEReader"));
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(oldLanguage); Directory.Delete(root, true); }
    }

    private static void Capture(WaylandEmulatorHost host, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR") is not { Length: > 0 } output) return;
        Call(host, "DrawShell");
        Directory.CreateDirectory(output); IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null); Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(output, "ereader-linux-" + name + ".png")), SDL.GetError()); } finally { SDL.DestroySurface(surface); }
    }

    private static void DeliverFile(WaylandEmulatorHost host, EmulationSession session, string? path)
    {
        Set(host, "pickingEReaderSession", session); Set(host, "fileDialogOpen", 1);
        IntPtr text = path is null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(path), list = Marshal.AllocHGlobal(IntPtr.Size * 2);
        try
        {
            Marshal.WriteIntPtr(list, text); Marshal.WriteIntPtr(list, IntPtr.Size, IntPtr.Zero);
            Call(host, "OnFileDialogCompleted", IntPtr.Zero, list, 0); Call(host, "DrainDialogSelections");
            Assert.AreEqual(0, Field<int>(host, "fileDialogOpen")); Assert.IsNull(Field<EmulationSession?>(host, "pickingEReaderSession"));
        }
        finally { if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); Marshal.FreeHGlobal(list); }
    }
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
    private static void Set(object o, string n, object value) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(o, value);
    private static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
}
