using System.Buffers.Binary;
using System.Reflection;
using AetherBoy.Desktop;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.DesktopTests;

[TestClass]
[DoNotParallelize]
public class LinuxCheatReviewTests
{
    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void NativeReviewUsesSharedDiagnosticsAndRequiresExplicitValueConfirmation(string language)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires native Wayland test session."); return; }
        string previous = UiText.Language; UiText.Initialize(language);
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-cheat-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DisplayLanguage = language });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            Assert.AreEqual(language, UiText.Language);
            byte[] rom = new byte[512]; BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE);
            string path = Path.Combine(root, "test.gba"); File.WriteAllBytes(path, rom);
            using var session = new EmulationSession(path, Path.Combine(root, "test.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Set(host, "session", session); Set(host, "controlCenterVisible", true);
            Type page = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
            Call(host, "SelectControlCenterPage", Enum.Parse(page, "Tools"));
            Set(host, "cheatCode", "C12BBBE1 D1ED426C"); Call(host, "OpenCheatReview");
            var review = (CheatInputReview)Call(host, "GetCheatReview")!;
            Assert.HasCount(2, review.Candidates); Assert.IsTrue(review.HasErrors);
            Set(host, "cheatFormat", CheatCodeFormat.ActionReplayV3);
            Assert.IsFalse(((CheatInputReview)Call(host, "GetCheatReview")!).HasErrors);
            Set(host, "cheatFormat", CheatCodeFormat.Automatic);
            Set(host, "cheatCode", "82025840 AAAA");
            Assert.IsTrue(((CheatInputReview)Call(host, "GetCheatReview")!).NeedsValueConfirmation);
            Call(host, "AddReviewedCheat"); Assert.IsEmpty(session.LatestSnapshot.Cheats);
            Set(host, "confirmCheatValues", true); Call(host, "AddReviewedCheat");
            Assert.HasCount(1, session.LatestSnapshot.Cheats);
            Set(host, "cheatCode", "Cheat code: + 82025840 0044svg"); Call(host, "OpenCheatReview");
            Assert.IsTrue(((CheatInputReview)Call(host, "GetCheatReview")!).HasErrors);
            Assert.IsFalse(Field<bool>(host, "confirmCheatValues"));
            Call(host, "AddReviewedCheat"); Assert.HasCount(1, session.LatestSnapshot.Cheats);
            Call(host, "DrawShell");
            if (Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR") is { } capture)
            {
                Directory.CreateDirectory(capture);
                IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
                Assert.AreNotEqual(IntPtr.Zero, surface);
                try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(capture, $"cheat-review-linux-{language}.png"))); }
                finally { SDL.DestroySurface(surface); }
            }
            Set(host, "session", null);
        }
        finally { SDL.Quit(); Directory.Delete(root, true); UiText.Initialize(previous); }
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags)!.GetValue(target)!;
}
