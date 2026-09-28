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
        public Action ApplyVideoSettings { get; init; } = () => { };
        public Action ApplyUiTheme { get; init; } = () => { };
        public Func<string> AudioOutputProvider { get; init; } = () => "No output";
        public Func<string> VideoOutputProvider { get; init; } = () => "No output";
        public Func<string> SaveFeedbackProvider { get; init; } = () => "Noch keine Save-State-Aktion.";
        public Action OpenStateGallery { get; init; } = () => { };
        public Action OpenQuickMenu { get; init; } = () => { };
        public Action CaptureScreenshot { get; init; } = () => { };
        public Action TogglePerformanceOverlay { get; init; } = () => { };
        public Action MarkProblem { get; init; } = () => { };
        public Func<string> HealthStatusProvider { get; init; } = () => "Keine Beobachtung aktiv.";
        public Action ToggleGameProfile { get; init; } = () => { };
        public Action ResetGameProfile { get; init; } = () => { };
        public Action OpenFirmwareManager { get; init; } = () => { };
        public Func<string> FirmwareStatusProvider { get; init; } = () => "Firmwareverwaltung nicht angebunden.";
        public Func<bool> RecordNextSessionProvider { get; init; } = () => ProductInfo.IsDevelopmentBuild;
        public Func<string> DiagnosticsPreferenceStatusProvider { get; init; } = () => "Änderungen gelten ab dem nächsten Programmstart.";
        public Func<bool, bool> SetRecordNextSession { get; init; } = _ => false;
        public required Action<int> SetFrameskip { get; init; }
        public required Action<int> SetSaveSlot { get; init; }
        public required Action OpenControls { get; init; }
        public required Action OpenSaveSafety { get; init; }
        public required Action OpenAudioInspector { get; init; }
        public required Action QuickSave { get; init; }
        public required Action QuickLoad { get; init; }
        public required Action ResetSettings { get; init; }
        public required Func<bool> TesterModeProvider { get; init; }
        public Func<bool> TesterReportAvailableProvider { get; init; } = () => false;
        public Func<string> TesterRecordingStatusProvider { get; init; } = () => "Keine Sitzungsaufzeichnung verfügbar.";
        public required Func<string?> TesterLogPathProvider { get; init; }
        public required Action ExportTesterReport { get; init; }
        public required Action OpenTesterFolder { get; init; }
    }
}
