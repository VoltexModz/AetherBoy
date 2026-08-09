using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace nanoboy.Core
{
    public sealed class RewindManager
    {
        private const int CaptureIntervalFrames = 4;
        private const int MaximumStates = 150;

        private readonly LinkedList<byte[]> history = new();
        private int framesSinceCapture;
        private long storedByteCount;

        public int HistoryCount => history.Count;
        public long StoredByteCount => storedByteCount;

        public void Initialize(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            Clear();
            AddState(SaveState.Capture(emulator));
        }

        public void CaptureFrame(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            framesSinceCapture++;
            if (framesSinceCapture < CaptureIntervalFrames) {
                return;
            }

            framesSinceCapture = 0;
            AddState(SaveState.Capture(emulator));
            if (history.Count > MaximumStates) {
                storedByteCount -= history.First!.Value.LongLength;
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
                storedByteCount -= history.Last!.Value.LongLength;
                history.RemoveLast();
            }

            SaveState.Restore(emulator, Decompress(history.Last!.Value));
            framesSinceCapture = 0;
            return true;
        }

        public void Clear()
        {
            history.Clear();
            framesSinceCapture = 0;
            storedByteCount = 0;
        }

        private void AddState(byte[] state)
        {
            byte[] compressed = Compress(state);
            history.AddLast(compressed);
            storedByteCount += compressed.LongLength;
        }

        private static byte[] Compress(byte[] state)
        {
            using var destination = new MemoryStream();
            using (var compressor = new BrotliStream(
                destination,
                CompressionLevel.Fastest,
                leaveOpen: true)) {
                compressor.Write(state, 0, state.Length);
            }
            return destination.ToArray();
        }

        private static byte[] Decompress(byte[] state)
        {
            using var source = new MemoryStream(state, writable: false);
            using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
            using var destination = new MemoryStream();
            decompressor.CopyTo(destination);
            return destination.ToArray();
        }
    }
}
