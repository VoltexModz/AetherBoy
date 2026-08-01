using System;
using System.Threading;
using System.Threading.Channels;

namespace AetherBoy.Runtime
{
    internal sealed class BoundedAudioDispatcher
    {
        private const int Capacity = 4;

        private readonly Channel<AudioSamplesAvailableEventArgs> channel;
        private readonly Action<AudioSamplesAvailableEventArgs> dispatch;
        private readonly Thread thread;
        private int stopped;
        private int threadId;

        public BoundedAudioDispatcher(Action<AudioSamplesAvailableEventArgs> dispatch)
        {
            this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            channel = Channel.CreateBounded<AudioSamplesAvailableEventArgs>(
                new BoundedChannelOptions(Capacity)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });
            thread = new Thread(DispatchLoop)
            {
                IsBackground = true,
                Name = "AetherBoy audio events"
            };
            thread.Start();
        }

        internal int ThreadId => Volatile.Read(ref threadId);

        public void TryPost(AudioSamplesAvailableEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            if (Volatile.Read(ref stopped) == 0)
            {
                channel.Writer.TryWrite(eventArgs);
            }
        }

        public void StopWithoutWaiting()
        {
            if (Interlocked.Exchange(ref stopped, 1) == 0)
            {
                channel.Writer.TryComplete();
            }
        }

        private void DispatchLoop()
        {
            Volatile.Write(ref threadId, Environment.CurrentManagedThreadId);
            ChannelReader<AudioSamplesAvailableEventArgs> reader = channel.Reader;
            while (Volatile.Read(ref stopped) == 0 &&
                   reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                while (Volatile.Read(ref stopped) == 0 && reader.TryRead(out var eventArgs))
                {
                    dispatch(eventArgs);
                }
            }
        }
    }
}
