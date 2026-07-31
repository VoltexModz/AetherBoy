using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nanoboy.Core.Audio.Backend
{
    public abstract class SoundOut : IDisposable
    {
        protected Audio audio;
        public SoundOut(Audio audio)
        {
            audio.AudioAvailable += Audio_AudioAvailable;
        }
        protected abstract void Audio_AudioAvailable(object sender, AudioAvailableEventArgs e);

        public abstract void Dispose();
    }
}
