using System;
using System.Diagnostics;
using System.Threading;
using nanoboy.Core;

namespace AetherBoy.Runtime
{
    internal interface IEmulationMachineFactory
    {
        IEmulationMachine Create();
    }

    internal interface IEmulationMachine : IDisposable
    {
        VideoGeometry VideoGeometry => VideoGeometry.GameBoy;
        EmulationFeature Features => EmulationFeature.GameBoyStandard;

        event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable;

        void RunFrame();
        void SetButtons(GameBoyButtons pressedButtons);
        void SetGameBoyAdvanceButtons(GameBoyAdvanceButtons pressedButtons) { }
        void Configure(EmulatorConfiguration configuration);
        void SetPalette(int paletteIndex);
        void Reset();
        byte[] CaptureState();
        void RestoreState(byte[] state);
        bool Rewind();
        CheatSnapshot AddCheat(string name, string code);
        bool RemoveCheat(Guid id);
        bool ToggleCheat(Guid id);
        bool TryCopyVideoFrame(int[] destination, ref long sequence);
        EmulationSnapshot CaptureSnapshot(
            SessionState state,
            bool isPaused,
            bool isTurboEnabled,
            long emulatedFrameCount,
            long videoFrameSequence);
    }

    internal interface IFramePacer
    {
        void Reset();
        void WaitForNextFrame(CancellationToken cancellationToken);
    }

    // A network owner must keep servicing commands while emulated time is waiting.
    // Returning false means no complete frame was emulated; it is not a fake frame.
    internal interface ICooperativeEmulationMachine
    {
        bool WaitingForNetwork { get; }
        bool NeedsIdleWait { get; }
        void SetLocalPaused(bool paused) { }
        void RecordFault(Exception fault) { }
        void PollNetwork();
        bool TryRunFrame();
    }

    internal interface IGracefulOnlineStop
    {
        void RequestStop();
        bool StopReady { get; }
    }

    internal sealed class RealTimeFramePacer : IFramePacer
    {
        private static readonly long FrameTicks = Math.Max(
            1,
            (long)Math.Round(Stopwatch.Frequency * EmulationClock.FrameSeconds));

        private long nextFrameTimestamp;

        public void Reset()
        {
            nextFrameTimestamp = 0;
        }

        public void WaitForNextFrame(CancellationToken cancellationToken)
        {
            long now = Stopwatch.GetTimestamp();
            if (nextFrameTimestamp == 0)
            {
                nextFrameTimestamp = now + FrameTicks;
            }

            long remaining = nextFrameTimestamp - now;
            if (remaining > 0)
            {
                TimeSpan delay = TimeSpan.FromSeconds((double)remaining / Stopwatch.Frequency);
                cancellationToken.WaitHandle.WaitOne(delay);
            }

            now = Stopwatch.GetTimestamp();
            nextFrameTimestamp += FrameTicks;
            if (now - nextFrameTimestamp > FrameTicks)
            {
                nextFrameTimestamp = now + FrameTicks;
            }
        }
    }
}
