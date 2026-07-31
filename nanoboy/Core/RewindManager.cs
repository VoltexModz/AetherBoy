using System.Collections.Generic;

namespace nanoboy.Core
{
    public class RewindManager
    {
        private readonly LinkedList<byte[]> history = new LinkedList<byte[]>();
        private const int MAX_STATES = 150; // 150 states * 4 frames = 600 frames = 10 seconds
        private int frameCounter = 0;

        public bool IsRewinding { get; set; }

        public void CaptureFrame(Nanoboy nano)
        {
            if (nano == null || IsRewinding) return;

            if (++frameCounter % 4 == 0)
            {
                byte[] stateData = SaveState.SaveToBuffer(nano);
                if (stateData != null)
                {
                    history.AddLast(stateData);
                    if (history.Count > MAX_STATES)
                    {
                        history.RemoveFirst();
                    }
                }
            }
        }

        public bool Rewind(Nanoboy nano)
        {
            if (nano == null || history.Count == 0) return false;

            byte[] lastState = history.Last.Value;
            history.RemoveLast();

            return SaveState.LoadFromBuffer(nano, lastState);
        }

        public void Clear()
        {
            history.Clear();
            frameCounter = 0;
            IsRewinding = false;
        }
    }
}
