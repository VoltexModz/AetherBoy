using System.Globalization;
using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxMessageAndSaveUiTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void SaveInformationDistinguishesNoGameLoadingEmptySavedAndUnreadable()
    {
        var saved = new LinuxStateCard(1, true, DateTime.Now, null);
        var empty = new LinuxStateCard(1, false, default, null);
        var broken = new LinuxStateCard(1, true, default, "invalid state");
        Assert.AreEqual(LinuxSaveInformationState.NoGame, LinuxSaveInformation.Resolve(false, true, saved, "old failure"));
        Assert.AreEqual(LinuxSaveInformationState.Loading, LinuxSaveInformation.Resolve(true, true, saved, null));
        Assert.AreEqual(LinuxSaveInformationState.Unavailable, LinuxSaveInformation.Resolve(true, false, null, null));
        Assert.AreEqual(LinuxSaveInformationState.Unavailable, LinuxSaveInformation.Resolve(true, false, saved, "disk failure"));
        Assert.AreEqual(LinuxSaveInformationState.Unavailable, LinuxSaveInformation.Resolve(true, false, broken, null));
        Assert.AreEqual(LinuxSaveInformationState.Empty, LinuxSaveInformation.Resolve(true, false, empty, null));
        Assert.AreEqual(LinuxSaveInformationState.Saved, LinuxSaveInformation.Resolve(true, false, saved, null));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(12)]
    [DataRow(35)]
    public void MessageWrapPreservesEveryPathCharacterAndWholeGraphemes(int width)
    {
        string text = "/home/test/" + new string('x', 170) + "/Pokémon-👩🏽‍💻-e\u0301.gb";
        float Measure(string value) => StringInfo.ParseCombiningCharacters(value).Length;
        var lines = LinuxMessageLayout.Wrap(text, width, Measure);
        Assert.AreEqual(text, string.Concat(lines.Select(line => line.Read(text))));
        var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToHashSet();
        foreach (var line in lines)
        {
            Assert.IsTrue(Measure(line.Read(text)) <= width);
            Assert.IsTrue(boundaries.Contains(line.Start));
            Assert.IsTrue(boundaries.Contains(line.Start + line.Length));
        }
    }

    [TestMethod]
    public void MessageWrapHandlesBlankLinesSpacesAndEmptyMessageWithoutDiscardingOriginalRanges()
    {
        Assert.HasCount(1, LinuxMessageLayout.Wrap("", 20, value => value.Length));
        string text = "first line\r\n\r\n   indented long/path/without/a/space\nlast line";
        var lines = LinuxMessageLayout.Wrap(text, 10, value => value.Length);
        Assert.AreEqual("first line   indented long/path/without/a/spacelast line", string.Concat(lines.Select(line => line.Read(text))));
        Assert.IsTrue(lines.Any(line => line.Length == 0));
        Assert.IsTrue(lines.All(line => line.Length <= 10));
        Assert.AreEqual(text.Length, lines[^1].Start + lines[^1].Length);
    }

    [TestMethod]
    public void NativeLongErrorRemainsSelectableAndScrollableAndSavePageShowsActualStates()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-message-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DisplayLanguage = "en", TextSize = 18 });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            var clipboard = new MemoryClipboard(); Set(host, "textClipboard", clipboard);
            var options = Field<LinuxFrontendOptions>(host, "options");
            Call(host, "ToggleControlCenter");
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Saves);
            Call(host, "DrawShell");
            Assert.AreEqual(UiText.Get("Open a game to inspect its save states."), CallResult<string>(host, "SaveInformationMessage", 1));
            Assert.IsFalse(Property<bool>(host, "HasSelectedState"));
            Capture(host, "saves-no-game-large-en");
            Set(host, "showGallery", true); Call(host, "DrawShell");
            Capture(host, "gallery-no-game-large-en"); Set(host, "showGallery", false);

            string fullPath = "/home/test/" + new string('z', 650) + "/unchanged.gb";
            var exception = new IOException("Native message with a long path: " + fullPath + "\n" + string.Join("\n", Enumerable.Range(1, 90).Select(i => $"Diagnostic line {i}: unchanged data.")));
            Call(host, "ReportError", exception);
            Call(host, "DrawShell");
            Capture(host, "long-error-footer-en");
            LinuxUiTestActions.Click(host, ":message:details");
            Assert.IsTrue(Field<bool>(host, "showMessageDetails"));
            Call(host, "DrawShell");
            string complete = Field<string>(host, "messageDetailsText");
            var cachedLines = Field<LinuxWrappedLine[]>(host, "messageLines");
            Call(host, "DrawShell");
            Assert.AreSame(cachedLines, Field<LinuxWrappedLine[]>(host, "messageLines"), "Unchanged diagnostic text must reuse its layout between frames.");
            options.TextSize = 14; Call(host, "DrawShell");
            Assert.AreNotSame(cachedLines, Field<LinuxWrappedLine[]>(host, "messageLines"), "Text size changes must invalidate the memo layout.");
            options.TextSize = 18; Call(host, "DrawShell");
            StringAssert.Contains(complete, fullPath);
            StringAssert.Contains(complete, "Diagnostic line 90");
            Capture(host, "long-error-details-top-en");
            Key(host, SDL.Scancode.End, SDL.Keymod.Ctrl);
            Call(host, "DrawShell");
            Assert.IsGreaterThan(0, Field<int>(host, "messageScrollLine"));
            Capture(host, "long-error-details-end-en");
            Key(host, SDL.Scancode.A, SDL.Keymod.Ctrl);
            Key(host, SDL.Scancode.C, SDL.Keymod.Ctrl);
            Assert.AreEqual(complete, clipboard.Value, "Copy must preserve unwrapped paths, newlines and the final diagnostic line.");
            Key(host, SDL.Scancode.F5);
            Assert.IsNull(Field<Task?>(host, "stateOperation"), "Game shortcuts cannot cross the details surface.");
            Call(host, "HandleMessageDetailsGamepad", SDL.GamepadButton.DPadUp, true);
            Call(host, "HandleMessageDetailsGamepad", SDL.GamepadButton.East, true);
            Assert.IsFalse(Field<bool>(host, "showMessageDetails"));

            string rom = Path.Combine(root, "generated.gb"); File.WriteAllBytes(rom, Rom());
            Call(host, "TryLoadRom", rom);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<EmulationSession?>(host, "session") is not null; }, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollDiskRefresh"); return Field<Task?>(host, "diskRefresh") is null; }, TimeSpan.FromSeconds(10)));
            Assert.AreEqual(LinuxSaveInformationState.Empty, CallResult<LinuxSaveInformationState>(host, "SaveInformationState", 1));
            Call(host, "QueueLoadState", 1);
            Assert.IsNull(Field<Task?>(host, "stateOperation"), "Empty slots must not begin an attempted load.");
            Capture(host, "saves-empty-large-en");
            Call(host, "QueueSaveState", 1);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompleteStateOperation"); return Field<Task?>(host, "stateOperation") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollDiskRefresh"); return Field<Task?>(host, "diskRefresh") is null; }, TimeSpan.FromSeconds(10)));
            Assert.AreEqual(LinuxSaveInformationState.Saved, CallResult<LinuxSaveInformationState>(host, "SaveInformationState", 1));
            Capture(host, "saves-saved-large-en");
            string stateBase = Field<LinuxRomStorage>(host, "storage").StateBasePath;
            File.WriteAllBytes(LinuxSaveStateStore.GetPath(stateBase, 1), []);
            Call(host, "RequestDiskRefresh");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollDiskRefresh"); return Field<Task?>(host, "diskRefresh") is null; }, TimeSpan.FromSeconds(10)));
            Assert.AreEqual(LinuxSaveInformationState.Unavailable, CallResult<LinuxSaveInformationState>(host, "SaveInformationState", 1));
            Call(host, "QueueLoadState", 1);
            Assert.IsNull(Field<Task?>(host, "stateOperation"));
            // Continue must restore a real saved session even while its metadata refresh is still pending.
            var originalSession = Field<EmulationSession>(host, "session");
            originalSession.SetPausedAsync(false).GetAwaiter().GetResult();
            Assert.IsTrue(SpinWait.SpinUntil(() => originalSession.LatestSnapshot.EmulatedFrameCount >= 7, TimeSpan.FromSeconds(10)));
            originalSession.SetPausedAsync(true).GetAwaiter().GetResult();
            string identity = Field<LinuxRomStorage>(host, "storage").Identity;
            Call(host, "CloseSession");
            byte[] resume = LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(stateBase, 0));
            Set(host, "lastResumeEntry", new LinuxLibraryEntry(identity, rom, "Generated resume game", DateTime.Now));
            Call(host, "ContinueLastSession");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<EmulationSession?>(host, "session") is not null; }, TimeSpan.FromSeconds(10)));
            Assert.IsNotNull(Field<Task?>(host, "stateOperation"), "Continue must queue the background resume before gallery inspection finishes.");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompleteStateOperation"); return Field<Task?>(host, "stateOperation") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(Field<EmulationSession>(host, "session").LatestSnapshot.IsPaused,
                "Continue is inspected while the real Control Center pause owner remains active.");
            CollectionAssert.AreEqual(resume, Field<EmulationSession>(host, "session").CaptureStateAsync().GetAwaiter().GetResult(), "Continue must restore the saved CPU, memory and frame state.");
            Assert.IsNotNull(Field<byte[]?>(host, "undoState"));
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollDiskRefresh"); return Field<Task?>(host, "diskRefresh") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsFalse(CallResult<string>(host, "SaveInformationMessage", 0).Contains("Slot 0", StringComparison.Ordinal));

            // Opening error details never commits a title/tags draft, even after a save error.
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Library);
            Call(host, "BeginLibraryEdit", new LinuxLibraryEntry(identity, rom, "Original title", DateTime.Now));
            Call(host, "ReceiveTextInput", " unsaved draft");
            string draft = Field<string>(host, "titleInput");
            Call(host, "OpenMessageDetails", "Synthetic metadata failure details");
            Assert.IsNull(Field<Task?>(host, "libraryMutation"));
            Assert.AreEqual(identity, Field<string>(host, "editingTitleIdentity"));
            Call(host, "CloseMessageDetails");
            Assert.AreEqual(draft, Field<string>(host, "titleInput"));
            Assert.IsTrue(Field<bool>(host, "editorHasFocus"));
            Call(host, "EndTitleEdit", false);

            // Physical polling must be suppressed too, not just key/button event routing.
            Call(host, "CloseControlCenter"); Set(host, "windowFocused", true);
            var resumedSession = Field<EmulationSession>(host, "session");
            resumedSession.SetPausedAsync(false).GetAwaiter().GetResult();
            resumedSession.SetTurboAsync(true).GetAwaiter().GetResult();
            var heldKeys = Field<HashSet<SDL.Scancode>>(host, "pressedKeys");
            heldKeys.Add(options.Keys[LinuxInputAction.A]); heldKeys.Add(options.Keys[LinuxInputAction.L]);
            IntPtr previousPad = Field<IntPtr>(host, "gamepad");
            var previousProfile = Field<LinuxGamepadProfile>(host, "gamepadProfile");
            var descriptor = new SDL.VirtualJoystickDesc(); SDL.InitInterface(ref descriptor);
            descriptor.Type = SDL.JoystickType.Gamepad; descriptor.NAxes = (ushort)SDL.GamepadAxis.Count;
            descriptor.NButtons = (ushort)SDL.GamepadButton.Count;
            descriptor.AxisMask = (1u << (int)SDL.GamepadAxis.Count) - 1;
            descriptor.ButtonMask = (1u << (int)SDL.GamepadButton.Count) - 1;
            uint virtualId = SDL.AttachVirtualJoystick(in descriptor);
            Assert.AreNotEqual(0u, virtualId, SDL.GetError());
            IntPtr virtualPad = SDL.OpenGamepad(virtualId);
            Assert.AreNotEqual(IntPtr.Zero, virtualPad, SDL.GetError());
            try
            {
                Set(host, "gamepad", virtualPad); Set(host, "gamepadProfile", new LinuxGamepadProfile());
                IntPtr joystick = SDL.GetGamepadJoystick(virtualPad);
                Assert.IsTrue(SDL.SetJoystickVirtualButton(joystick, (int)SDL.GamepadButton.DPadDown, true), SDL.GetError());
                Assert.IsTrue(SDL.SetJoystickVirtualAxis(joystick, (int)SDL.GamepadAxis.LeftX, 24000), SDL.GetError());
                SDL.UpdateJoysticks();
                Assert.AreNotEqual(GameBoyButtons.None, CallResult<GameBoyButtons>(host, "ReadButtons"));
                Assert.AreNotEqual(GameBoyAdvanceButtons.None, CallResult<GameBoyAdvanceButtons>(host, "ReadAdvanceButtons"));
                Call(host, "OpenMessageDetails", complete);
                Assert.IsTrue(resumedSession.LatestSnapshot.IsPaused);
                Assert.IsFalse(resumedSession.LatestSnapshot.IsTurboEnabled, "Details must cancel held-key turbo too.");
                heldKeys.Add(options.Keys[LinuxInputAction.A]); heldKeys.Add(options.Keys[LinuxInputAction.L]);
                Assert.AreEqual(GameBoyButtons.None, CallResult<GameBoyButtons>(host, "ReadButtons"));
                Assert.AreEqual(GameBoyAdvanceButtons.None, CallResult<GameBoyAdvanceButtons>(host, "ReadAdvanceButtons"));
                Call(host, "CloseMessageDetails");
                Assert.IsFalse(resumedSession.LatestSnapshot.IsPaused, "Closing details restores a previously playing session.");
                resumedSession.SetPausedAsync(true).GetAwaiter().GetResult();
                Call(host, "OpenMessageDetails", complete); Call(host, "CloseMessageDetails");
                Assert.IsTrue(resumedSession.LatestSnapshot.IsPaused, "A session that was already paused must remain paused.");
                resumedSession.SetPausedAsync(false).GetAwaiter().GetResult();
                Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusLost });
                Assert.IsTrue(resumedSession.LatestSnapshot.IsPaused);
                Call(host, "OpenMessageDetails", complete);
                Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusGained });
                Assert.IsTrue(resumedSession.LatestSnapshot.IsPaused, "Returning from the accessibility window must not resume behind an open memo.");
                Call(host, "CloseMessageDetails");
                Assert.IsFalse(resumedSession.LatestSnapshot.IsPaused, "The pending focus-resume intent belongs to the dialog until it closes.");
            }
            finally
            {
                heldKeys.Clear(); Set(host, "gamepad", previousPad); Set(host, "gamepadProfile", previousProfile);
                SDL.CloseGamepad(virtualPad); Assert.IsTrue(SDL.DetachVirtualJoystick(virtualId), SDL.GetError());
            }
            Call(host, "ToggleControlCenter");
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Saves);
            foreach (string language in new[] { "de", "en" })
            {
                UiText.Initialize(language);
                foreach (var theme in UiThemePresets.All)
                {
                    Set(host, "primaryColorInput", theme.Primary);
                    Set(host, "secondaryColorInput", theme.Secondary);
                    Set(host, "backgroundColorInput", theme.Background);
                    Call(host, "ApplyAppearanceColors"); Call(host, "DrawShell");
                    Assert.AreEqual(theme.Primary, options.UiPrimaryColor);
                    Assert.AreEqual(theme.Secondary, options.UiSecondaryColor);
                    Assert.AreEqual(theme.Background, options.UiBackgroundColor);
                    Capture(host, "saves-unreadable-" + language + "-" + theme.Id);
                }
            }
            Set(host, "primaryColorInput", "#FF8800");
            Set(host, "secondaryColorInput", "#0066CC");
            Set(host, "backgroundColorInput", "#FFFFFF");
            SDL.SetWindowSize(Field<IntPtr>(host, "window"), 860, 554); SDL.PumpEvents();
            Call(host, "ApplyAppearanceColors"); Call(host, "DrawShell");
            Assert.AreEqual("#FFFFFF", options.UiBackgroundColor);
            Capture(host, "saves-custom-light-minimum-large-en");
            Call(host, "OpenMessageDetails", complete); Call(host, "DrawShell");
            Capture(host, "details-custom-light-minimum-large-en");
            Key(host, SDL.Scancode.Escape);
            Assert.IsFalse(Field<bool>(host, "showMessageDetails"));
        }
        finally { SDL.Quit(); Directory.Delete(root, true); }
    }

    private sealed class MemoryClipboard : ILinuxTextClipboard
    {
        public string Value { get; private set; } = "";
        public string Read() => Value;
        public void Write(string value) => Value = value;
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static T Property<T>(object instance, string name) => (T)instance.GetType().GetProperty(name, Private)!.GetValue(instance)!;
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private)!.SetValue(instance, value);
    private static void Call(object instance, string name, params object[] arguments) => instance.GetType().GetMethod(name, Private)!.Invoke(instance, arguments);
    private static T CallResult<T>(object instance, string name, params object[] arguments) => (T)instance.GetType().GetMethod(name, Private)!.Invoke(instance, arguments)!;
    private static void Key(object host, SDL.Scancode key, SDL.Keymod mod = 0) => Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = key, Mod = mod }, true);
    private static void Capture(WaylandEmulatorHost host, string name)
    {
        string? output = Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output); Call(host, "DrawShell");
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface, SDL.GetError());
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(output, name + ".png")), SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }
    private static byte[] Rom()
    {
        byte[] data = new byte[32768];
        data[0x100] = 0xC3; data[0x101] = 0x50; data[0x102] = 0x01;
        "UI STATE TEST"u8.CopyTo(data.AsSpan(0x134)); data[0x150] = 0x18; data[0x151] = 0xFE;
        byte checksum = 0;
        for (int i = 0x134; i <= 0x14C; i++) checksum = unchecked((byte)(checksum - data[i] - 1));
        data[0x14D] = checksum; return data;
    }
}
