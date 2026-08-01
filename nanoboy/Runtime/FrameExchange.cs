using System;

namespace AetherBoy.Runtime
{
    internal sealed class FrameExchange
    {
        private readonly object sync = new();
        private int[] writeBuffer = new int[EmulationSnapshot.FramePixelCount];
        private int[] publishedBuffer = new int[EmulationSnapshot.FramePixelCount];
        private long publishedSequence;
        private bool hasPublishedFrame;

        public int[] WriteBuffer => writeBuffer;

        public long PublishedSequence
        {
            get
            {
                lock (sync)
                {
                    return publishedSequence;
                }
            }
        }

        public void Publish(long sequence)
        {
            lock (sync)
            {
                (publishedBuffer, writeBuffer) = (writeBuffer, publishedBuffer);
                publishedSequence = sequence;
                hasPublishedFrame = true;
            }
        }

        public bool TryCopyLatestFrame(Span<int> destination, ref long sequence)
        {
            if (destination.Length < EmulationSnapshot.FramePixelCount)
            {
                throw new ArgumentException(
                    "The destination is too small for a video frame.",
                    nameof(destination));
            }

            lock (sync)
            {
                if (!hasPublishedFrame || sequence == publishedSequence)
                {
                    return false;
                }

                publishedBuffer.AsSpan().CopyTo(destination);
                sequence = publishedSequence;
                return true;
            }
        }
    }
}
