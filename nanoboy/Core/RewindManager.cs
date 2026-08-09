using System;
using System.Collections.Generic;

namespace nanoboy.Core
{
    public sealed class RewindManager
    {
        private const int CaptureIntervalFrames = 4;
        private const int MaximumStates = 150;

        private readonly LinkedList<byte[]> history = new();
        private int framesSinceCapture;

        public int HistoryCount => history.Count;

        public void Initialize(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            Clear();
            history.AddLast(SaveState.Capture(emulator));
        }

        public void CaptureFrame(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            framesSinceCapture++;
            if (framesSinceCapture < CaptureIntervalFrames) {
                return;
            }

            framesSinceCapture = 0;
            history.AddLast(SaveState.Capture(emulator));
            if (history.Count > MaximumStates) {
                history.RemoveFirst();
            }
        }

        public bool Rewind(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            if (history.Count == 0) {
                return false;
            }

            if (framesSinceCapture == 0) {
                if (history.Count == 1) {
                    return false;
                }
                history.RemoveLast();
            }

            SaveState.Restore(emulator, history.Last!.Value);
            framesSinceCapture = 0;
            return true;
        }

        public void Clear()
        {
            history.Clear();
            framesSinceCapture = 0;
        }
    }
}
