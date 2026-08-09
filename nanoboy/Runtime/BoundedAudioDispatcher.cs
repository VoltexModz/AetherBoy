using System;
using System.Threading;
using System.Threading.Channels;

namespace AetherBoy.Runtime
{
    internal sealed class BoundedAudioDispatcher
    {
        private readonly record struct QueuedAudio(
            int Generation,
            AudioSamplesAvailableEventArgs EventArgs);

        private const int Capacity = 4;

        private readonly Channel<QueuedAudio> channel;
        private readonly Action<AudioSamplesAvailableEventArgs> dispatch;
        private readonly Thread thread;
        private int stopped;
        private int threadId;
        private int generation;

        public BoundedAudioDispatcher(Action<AudioSamplesAvailableEventArgs> dispatch)
        {
            this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            channel = Channel.CreateBounded<QueuedAudio>(
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
                channel.Writer.TryWrite(new QueuedAudio(
                    Volatile.Read(ref generation),
                    eventArgs));
            }
        }

        public void DiscardPending()
        {
            Interlocked.Increment(ref generation);
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
            ChannelReader<QueuedAudio> reader = channel.Reader;
            while (Volatile.Read(ref stopped) == 0 &&
                   reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                while (Volatile.Read(ref stopped) == 0 && reader.TryRead(out QueuedAudio queued))
                {
                    if (queued.Generation == Volatile.Read(ref generation))
                    {
                        dispatch(queued.EventArgs);
                    }
                }
            }
        }
    }
}
