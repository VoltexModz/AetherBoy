using System;

namespace nanoboy.Core.Audio.Backend
{
    public abstract class SoundOut : IDisposable
    {
        protected SoundOut(Audio audio)
        {
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
        }

        protected Audio Audio { get; }

        public abstract void Dispose();
    }
}
