using System;

namespace AetherBoy.Runtime
{
    internal sealed class FrameExchange
    {
        private readonly object sync = new();
        private VideoGeometry geometry = VideoGeometry.GameBoy;
        private int[] writeBuffer = new int[EmulationSnapshot.FramePixelCount];
        private int[] publishedBuffer = new int[EmulationSnapshot.FramePixelCount];
        private long publishedSequence;
        private bool hasPublishedFrame;

        public int[] WriteBuffer => writeBuffer;

        public VideoGeometry Geometry
        {
            get { lock (sync) { return geometry; } }
        }

        // Called by the owner once, before the first frame is published.
        public void Configure(VideoGeometry nextGeometry)
        {
            ArgumentNullException.ThrowIfNull(nextGeometry);
            lock (sync)
            {
                if (hasPublishedFrame)
                    throw new InvalidOperationException("Video geometry cannot change during a session.");

                if (geometry == nextGeometry)
                    return;

                int[] nextWriteBuffer = new int[nextGeometry.PixelCount];
                int[] nextPublishedBuffer = new int[nextGeometry.PixelCount];
                writeBuffer = nextWriteBuffer;
                publishedBuffer = nextPublishedBuffer;
                geometry = nextGeometry;
            }
        }

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

        public void Publish()
        {
            lock (sync)
            {
                (publishedBuffer, writeBuffer) = (writeBuffer, publishedBuffer);
                publishedSequence = publishedSequence == long.MaxValue
                    ? 1
                    : publishedSequence + 1;
                hasPublishedFrame = true;
            }
        }

        public bool TryCopyLatestFrame(Span<int> destination, ref long sequence)
        {
            lock (sync)
            {
                if (destination.Length < geometry.PixelCount)
                {
                    throw new ArgumentException(
                        "The destination is too small for a video frame.",
                        nameof(destination));
                }

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
