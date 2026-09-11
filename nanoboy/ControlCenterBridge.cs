using System;
using AetherBoy.Runtime;
using nanoboy.Input;

namespace nanoboy
{
    internal sealed class ControlCenterBridge
    {
        public required NanoboySettings Settings { get; init; }
        public required Func<EmulationSnapshot?> SnapshotProvider { get; init; }
        public required Func<HostGamepadState> GamepadProvider { get; init; }
        public required Func<string?> RomPathProvider { get; init; }
        public required Action<int> SetPalette { get; init; }
        public required Action<int> SetDisplayFilter { get; init; }
        public required Action<int> SetWindowScale { get; init; }
        public required Action ToggleFullscreen { get; init; }
        public required Action ApplyAudioSettings { get; init; }
        public required Action<int> SetFrameskip { get; init; }
        public required Action<int> SetSaveSlot { get; init; }
        public required Action OpenControls { get; init; }
        public required Action OpenSaveSafety { get; init; }
        public required Action OpenAudioInspector { get; init; }
        public required Action QuickSave { get; init; }
        public required Action QuickLoad { get; init; }
        public required Action ResetSettings { get; init; }
        public required Func<bool> TesterModeProvider { get; init; }
        public required Func<string?> TesterLogPathProvider { get; init; }
        public required Action ExportTesterReport { get; init; }
        public required Action OpenTesterFolder { get; init; }
    }
}
