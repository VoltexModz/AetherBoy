using System.Reflection;
using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Exercises real SDL gamepad events using process-local virtual devices, not physical hardware.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxVirtualControllerPlaytestTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [TestMethod]
    public void VirtualControllersRouteButtonsRemappingDeadzoneMenuFocusAndHotplug()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_PLAYTEST") != "1" &&
            Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        {
            Assert.Inconclusive("Set AETHERBOY_PLAYTEST=1 or AETHERBOY_UI_TESTS=1 in a native Wayland session.");
            return;
        }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-virtual-controller-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        string rom = Path.Combine(directory, "controller.gb");
        File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, PauseOnFocusLoss = true });
        string? previousHint = SDL.GetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS");
        var attached = new HashSet<uint>();
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        SDL.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        try
        {
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            if (Field<IntPtr>(host, "gamepad") != IntPtr.Zero)
            { Assert.Inconclusive("Run the virtual-device scenario without a physical controller selected."); return; }

            void Pump()
            {
                SDL.UpdateJoysticks();
                while (SDL.PollEvent(out SDL.Event current)) Call(host, "HandleEvent", current);
                Call(host, "UpdateEmulation");
            }
            uint Attach(ushort product)
            {
                var descriptor = new SDL.VirtualJoystickDesc();
                SDL.InitInterface(ref descriptor);
                descriptor.Type = SDL.JoystickType.Gamepad;
                descriptor.VendorID = 0x1209;
                descriptor.ProductID = product;
                descriptor.NAxes = (ushort)SDL.GamepadAxis.Count;
                descriptor.NButtons = (ushort)SDL.GamepadButton.Count;
                descriptor.AxisMask = (1u << (int)SDL.GamepadAxis.Count) - 1;
                descriptor.ButtonMask = (1u << (int)SDL.GamepadButton.Count) - 1;
                descriptor.Name = Marshal.StringToCoTaskMemUTF8("AetherBoy process-local virtual playtest");
                uint id;
                try { id = SDL.AttachVirtualJoystick(in descriptor); }
                finally { Marshal.FreeCoTaskMem(descriptor.Name); }
                Assert.AreNotEqual(0u, id, SDL.GetError());
                attached.Add(id);
                Assert.IsTrue(SDL.IsGamepad(id));
                Pump();
                return id;
            }
            void Detach(uint id)
            {
                Assert.IsTrue(SDL.DetachVirtualJoystick(id), SDL.GetError());
                attached.Remove(id);
                Pump();
            }
            void Button(SDL.GamepadButton button, bool down)
            {
                IntPtr joystick = SDL.GetGamepadJoystick(Field<IntPtr>(host, "gamepad"));
                Assert.IsTrue(SDL.SetJoystickVirtualButton(joystick, (int)button, down), SDL.GetError());
                Pump();
            }
            void Axis(short value)
            {
                IntPtr joystick = SDL.GetGamepadJoystick(Field<IntPtr>(host, "gamepad"));
                Assert.IsTrue(SDL.SetJoystickVirtualAxis(joystick, (int)SDL.GamepadAxis.LeftX, value), SDL.GetError());
                Pump();
            }

            uint first = Attach(0xAEB0);
            Assert.AreEqual(first, SDL.GetGamepadID(Field<IntPtr>(host, "gamepad")));
            Call(host, "TryLoadRom", rom);
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                Call(host, "CompletePendingLoad");
                return Field<EmulationSession?>(host, "session") is not null;
            }, TimeSpan.FromSeconds(10)), Field<string?>(host, "loadError"));
            var session = Field<EmulationSession>(host, "session");
            Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusGained });

            Button(SDL.GamepadButton.South, true);
            Assert.IsTrue(Field<GameBoyButtons>(host, "postedButtons").HasFlag(GameBoyButtons.A));
            Button(SDL.GamepadButton.South, false);
            Assert.AreEqual(GameBoyButtons.None, Field<GameBoyButtons>(host, "postedButtons"));
            typeof(WaylandEmulatorHost).GetField("rebindingGamepad", Private)!.SetValue(host, LinuxInputAction.A);
            Button(SDL.GamepadButton.East, true);
            Assert.IsNull(Field<LinuxInputAction?>(host, "rebindingGamepad"));
            Assert.AreEqual(SDL.GamepadButton.East, Field<LinuxGamepadProfile>(host, "gamepadProfile").Buttons[LinuxInputAction.A]);
            Assert.IsTrue(Field<GameBoyButtons>(host, "postedButtons").HasFlag(GameBoyButtons.A));
            Button(SDL.GamepadButton.East, false);

            Field<LinuxGamepadProfile>(host, "gamepadProfile").Deadzone = 8000;
            Axis(4000);
            Assert.AreEqual(GameBoyButtons.None, Field<GameBoyButtons>(host, "postedButtons"));
            Axis(12000);
            Assert.IsTrue(Field<GameBoyButtons>(host, "postedButtons").HasFlag(GameBoyButtons.Right));
            Axis(0);

            Button(SDL.GamepadButton.RightStick, true);
            Button(SDL.GamepadButton.RightStick, false);
            Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Call(host, "DrawShell");
            Button(SDL.GamepadButton.DPadDown, true);
            Button(SDL.GamepadButton.DPadDown, false);
            Assert.AreEqual(0, Field<int>(host, "focusedControl"));
            Button(SDL.GamepadButton.South, true);
            Button(SDL.GamepadButton.South, false);
            Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
            Assert.IsFalse(session.LatestSnapshot.IsPaused);

            Button(SDL.GamepadButton.North, true);
            Assert.IsTrue(session.LatestSnapshot.IsTurboEnabled);
            Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusLost });
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Assert.IsFalse(session.LatestSnapshot.IsTurboEnabled);
            Assert.AreEqual(GameBoyButtons.None, Field<GameBoyButtons>(host, "postedButtons"));
            Button(SDL.GamepadButton.North, false);
            Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusGained });
            Assert.IsFalse(session.LatestSnapshot.IsPaused);
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusLost });
            Call(host, "HandleEvent", new SDL.Event { Type = (uint)SDL.EventType.WindowFocusGained });
            Assert.IsTrue(session.LatestSnapshot.IsPaused, "Focus gain must preserve a manual pause.");

            uint second = Attach(0xAEB1);
            Assert.AreEqual(first, SDL.GetGamepadID(Field<IntPtr>(host, "gamepad")));
            Detach(first);
            Assert.AreEqual(second, SDL.GetGamepadID(Field<IntPtr>(host, "gamepad")), "Disconnect must select the already-connected replacement.");
            Assert.AreEqual(SDL.GamepadButton.South, Field<LinuxGamepadProfile>(host, "gamepadProfile").Buttons[LinuxInputAction.A]);
            Detach(second);
            Assert.AreEqual(IntPtr.Zero, Field<IntPtr>(host, "gamepad"));
            _ = Attach(0xAEB0);
            Assert.AreEqual(SDL.GamepadButton.East, Field<LinuxGamepadProfile>(host, "gamepadProfile").Buttons[LinuxInputAction.A]);
            Assert.AreEqual(8000, Field<LinuxGamepadProfile>(host, "gamepadProfile").Deadzone);
            Call(host, "FlushSettingsIfDue", true);
            var options = LinuxSettingsStore.Load(settings, out var error);
            Assert.IsNull(error);
            Assert.IsTrue(options.Gamepads.Values.Any(profile => profile.Buttons[LinuxInputAction.A] == SDL.GamepadButton.East && profile.Deadzone == 8000));
            // Use the same visible controls as a player, including the real SDL button event.
            Call(host, "ToggleControlCenter");
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Controller);
            void Click(float x, float y) { Call(host, "DrawShell"); Call(host, "HandleMouseClick", x, y); Call(host, "DrawShell"); }
            Click(550, 315);
            Assert.AreEqual(LinuxInputAction.A, Field<LinuxInputAction?>(host, "rebindingGamepad"));
            Button(SDL.GamepadButton.West, true); Button(SDL.GamepadButton.West, false);
            Assert.AreEqual(SDL.GamepadButton.West, Field<LinuxGamepadProfile>(host, "gamepadProfile").Buttons[LinuxInputAction.A]);
            Click(970, 592); // Stick settings
            Assert.IsTrue(Field<bool>(host, "showControllerStick"));
            Click(370, 468); // Decrease deadzone
            Assert.AreEqual(6000, Field<LinuxGamepadProfile>(host, "gamepadProfile").Deadzone);
            Axis(5000); Call(host, "DrawShell");
            Assert.IsTrue(Field<List<string>>(host, "accessibleDescriptions").Contains("Stick input is inside the deadzone"));
            Axis(14000); Call(host, "DrawShell");
            Assert.IsTrue(Field<List<string>>(host, "accessibleDescriptions").Contains("Stick input is active"));
            Axis(0);
            Click(400, 275); // Back to buttons
            Click(400, 592); // Reset buttons
            Assert.AreEqual(SDL.GamepadButton.South, Field<LinuxGamepadProfile>(host, "gamepadProfile").Buttons[LinuxInputAction.A]);
            Assert.AreEqual(6000, Field<LinuxGamepadProfile>(host, "gamepadProfile").Deadzone);
            Call(host, "FlushSettingsIfDue", true);
            options = LinuxSettingsStore.Load(settings, out error);
            Assert.IsNull(error);
            Assert.IsTrue(options.Gamepads.Values.Any(profile => profile.Buttons[LinuxInputAction.A] == SDL.GamepadButton.South && profile.Deadzone == 6000));

        }
        finally
        {
            foreach (uint id in attached) SDL.DetachVirtualJoystick(id);
            SDL.Quit();
            if (previousHint is null) SDL.ResetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS");
            else SDL.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", previousHint);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
}
