using System;

namespace nanoboy.Core.Advance
{
    public sealed class GbaVideo
    {
        public const int FrameWidth = 240;
        public const int FrameHeight = 160;
        public const int FramePixelCount = FrameWidth * FrameHeight;

        private readonly int[] frame = new int[FramePixelCount];
        private readonly int[] publishedFrame = new int[FramePixelCount];
        private readonly object publishLock = new();
        private long sequence;

        public void RenderFrame(GbaMemoryBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            ushort control = bus.DisplayControl;
            bool mode3 = (control & 7) == 3 && (control & (1 << 10)) != 0;
            if (!mode3)
            {
                frame.AsSpan().Fill(unchecked((int)0xFF000000));
            }
            else
            {
                ReadOnlySpan<byte> vram = bus.VideoRam;
                for (int pixel = 0; pixel < frame.Length; pixel++)
                {
                    ushort color = (ushort)(vram[pixel * 2] | vram[pixel * 2 + 1] << 8);
                    int red5 = color & 0x1F;
                    int green5 = (color >> 5) & 0x1F;
                    int blue5 = (color >> 10) & 0x1F;
                    int red = red5 << 3 | red5 >> 2;
                    int green = green5 << 3 | green5 >> 2;
                    int blue = blue5 << 3 | blue5 >> 2;
                    frame[pixel] = unchecked((int)(0xFF000000u | (uint)(red << 16 | green << 8 | blue)));
                }
            }

            lock (publishLock)
            {
                frame.CopyTo(publishedFrame, 0);
                sequence = sequence == long.MaxValue ? 1 : sequence + 1;
            }
        }

        public bool TryCopyPublishedFrame(int[] destination, ref long previousSequence)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (destination.Length != FramePixelCount)
                throw new ArgumentException($"A GBA frame requires {FramePixelCount} pixels.", nameof(destination));
            lock (publishLock)
            {
                if (sequence == 0 || previousSequence == sequence)
                    return false;
                publishedFrame.CopyTo(destination, 0);
                previousSequence = sequence;
                return true;
            }
        }
    }
}
