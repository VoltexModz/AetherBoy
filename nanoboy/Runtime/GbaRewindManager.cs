using System;
using System.Collections.Generic;

namespace AetherBoy.Runtime
{
    internal sealed class GbaRewindManager
    {
        private const int CaptureIntervalFrames = 4;
        private const int MaximumStates = 150;
        private const long MaximumStoredBytes = 96L * 1024 * 1024;

        private readonly LinkedList<byte[]> history = new();
        private int framesSinceCapture;
        private long storedByteCount;

        internal int HistoryCount => history.Count;
        internal long StoredByteCount => storedByteCount;

        internal void Initialize(byte[] state)
        {
            ArgumentNullException.ThrowIfNull(state);
            Clear();
            AddState(state);
        }

        internal void CaptureFrame(Func<byte[]> capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            framesSinceCapture++;
            if (framesSinceCapture < CaptureIntervalFrames)
                return;

            framesSinceCapture = 0;
            AddState(capture());
        }

        internal bool Rewind(Action<byte[]> restore)
        {
            ArgumentNullException.ThrowIfNull(restore);
            if (history.Count == 0)
                return false;

            if (framesSinceCapture == 0)
            {
                if (history.Count == 1)
                    return false;
                RemoveFirstOrLast(removeFirst: false);
            }

            restore(history.Last!.Value);
            framesSinceCapture = 0;
            return true;
        }

        internal void Clear()
        {
            history.Clear();
            framesSinceCapture = 0;
            storedByteCount = 0;
        }

        private void AddState(byte[] state)
        {
            byte[] ownedState = (byte[])state.Clone();
            history.AddLast(ownedState);
            storedByteCount += ownedState.LongLength;
            while (history.Count > MaximumStates || storedByteCount > MaximumStoredBytes)
                RemoveFirstOrLast(removeFirst: true);
        }

        private void RemoveFirstOrLast(bool removeFirst)
        {
            LinkedListNode<byte[]> node = removeFirst ? history.First! : history.Last!;
            storedByteCount -= node.Value.LongLength;
            history.Remove(node);
        }
    }
}
