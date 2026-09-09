using System;
using System.Collections.Generic;

namespace AetherBoy.Runtime
{
    public sealed class EmulationSnapshot
    {
        public const int FrameWidth = 160;
        public const int FrameHeight = 144;
        public const int FramePixelCount = FrameWidth * FrameHeight;

        private readonly CheatSnapshot[] cheats;
        private readonly IReadOnlyList<CheatSnapshot> readOnlyCheats;
        private readonly GbaDiagnosticEventSnapshot[] diagnosticEvents;
        private readonly IReadOnlyList<GbaDiagnosticEventSnapshot> readOnlyDiagnosticEvents;

        internal EmulationSnapshot(
            SessionState state,
            bool isPaused,
            bool isTurboEnabled,
            long emulatedFrameCount,
            long videoFrameSequence,
            RomSnapshot? rom,
            AudioSnapshot? audio,
            ReadOnlySpan<CheatSnapshot> cheats,
            VideoGeometry? videoGeometry = null,
            EmulationFeature features = EmulationFeature.None,
            ReadOnlySpan<GbaDiagnosticEventSnapshot> diagnosticEvents = default)
        {
            State = state;
            IsPaused = isPaused;
            IsTurboEnabled = isTurboEnabled;
            EmulatedFrameCount = emulatedFrameCount;
            VideoFrameSequence = videoFrameSequence;
            Rom = rom;
            Audio = audio;
            VideoGeometry = videoGeometry ?? VideoGeometry.GameBoy;
            Features = features;
            this.cheats = cheats.ToArray();
            readOnlyCheats = Array.AsReadOnly(this.cheats);
            this.diagnosticEvents = diagnosticEvents.ToArray();
            readOnlyDiagnosticEvents = Array.AsReadOnly(this.diagnosticEvents);
        }

        public SessionState State { get; }
        public bool IsPaused { get; }
        public bool IsTurboEnabled { get; }
        public long EmulatedFrameCount { get; }
        public long VideoFrameSequence { get; }
        public RomSnapshot? Rom { get; }
        public AudioSnapshot? Audio { get; }
        public VideoGeometry VideoGeometry { get; }
        public EmulationFeature Features { get; }
        public IReadOnlyList<CheatSnapshot> Cheats => readOnlyCheats;
        public IReadOnlyList<GbaDiagnosticEventSnapshot> DiagnosticEvents => readOnlyDiagnosticEvents;
        public bool HasVideoFrame => VideoFrameSequence != 0;

        internal EmulationSnapshot WithState(SessionState state, bool isPaused)
        {
            return new EmulationSnapshot(
                state,
                isPaused,
                IsTurboEnabled,
                EmulatedFrameCount,
                VideoFrameSequence,
                Rom,
                Audio,
                cheats,
                VideoGeometry,
                Features,
                diagnosticEvents);
        }

        internal EmulationSnapshot WithVideoGeometry(VideoGeometry geometry)
        {
            if (VideoGeometry == geometry)
                return this;

            return new EmulationSnapshot(
                State, IsPaused, IsTurboEnabled, EmulatedFrameCount, VideoFrameSequence,
                Rom, Audio, cheats, geometry, Features, diagnosticEvents);
        }

        public bool Supports(EmulationFeature feature) =>
            (Features & feature) == feature;

        internal static EmulationSnapshot Starting { get; } = new EmulationSnapshot(
            SessionState.Starting,
            isPaused: false,
            isTurboEnabled: false,
            emulatedFrameCount: 0,
            videoFrameSequence: 0,
            rom: null,
            audio: null,
            ReadOnlySpan<CheatSnapshot>.Empty);
    }

    public sealed class AudioSamplesAvailableEventArgs : EventArgs
    {
        private readonly float[] samples;

        internal AudioSamplesAvailableEventArgs(ReadOnlySpan<float> samples, int sampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            this.samples = samples.ToArray();
            SampleRate = sampleRate;
        }

        public int SampleRate { get; }
        public int SampleCount => samples.Length;

        public float[] GetSamplesCopy() => (float[])samples.Clone();

        public void CopySamplesTo(Span<float> destination)
        {
            if (destination.Length < samples.Length)
            {
                throw new ArgumentException("The destination is too small for the audio samples.", nameof(destination));
            }

            samples.CopyTo(destination);
        }
    }
}
