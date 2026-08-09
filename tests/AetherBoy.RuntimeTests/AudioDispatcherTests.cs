using System.Collections.Concurrent;
using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class AudioDispatcherTests
{
    private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void BlockedConsumerKeepsOnlyTheFourNewestPendingBlocks()
    {
        var received = new ConcurrentQueue<int>();
        using var firstEntered = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);
        using var finalReceived = new ManualResetEventSlim(false);
        var dispatcher = new BoundedAudioDispatcher(eventArgs =>
        {
            int value = (int)eventArgs.GetSamplesCopy()[0];
            received.Enqueue(value);
            if (value == 0)
            {
                firstEntered.Set();
                releaseFirst.Wait(DeadlockTimeout);
            }
            else if (value == 32)
            {
                finalReceived.Set();
            }
        });

        try
        {
            dispatcher.TryPost(CreateBlock(0));
            Assert.IsTrue(firstEntered.Wait(DeadlockTimeout));

            for (int value = 1; value <= 32; value++)
            {
                dispatcher.TryPost(CreateBlock(value));
            }

            releaseFirst.Set();
            Assert.IsTrue(finalReceived.Wait(DeadlockTimeout));
            CollectionAssert.AreEqual(
                new[] { 0, 29, 30, 31, 32 },
                received.ToArray());
        }
        finally
        {
            releaseFirst.Set();
            dispatcher.StopWithoutWaiting();
        }
    }

    [TestMethod]
    public void TimelineChangeDropsQueuedAudioButAcceptsNewGeneration()
    {
        var received = new ConcurrentQueue<int>();
        using var firstEntered = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);
        using var currentGenerationReceived = new ManualResetEventSlim(false);
        var dispatcher = new BoundedAudioDispatcher(eventArgs =>
        {
            int value = (int)eventArgs.GetSamplesCopy()[0];
            received.Enqueue(value);
            if (value == 0)
            {
                firstEntered.Set();
                releaseFirst.Wait(DeadlockTimeout);
            }
            else if (value == 2)
            {
                currentGenerationReceived.Set();
            }
        });

        try
        {
            dispatcher.TryPost(CreateBlock(0));
            Assert.IsTrue(firstEntered.Wait(DeadlockTimeout));
            dispatcher.TryPost(CreateBlock(1));
            dispatcher.DiscardPending();
            dispatcher.TryPost(CreateBlock(2));

            releaseFirst.Set();
            Assert.IsTrue(currentGenerationReceived.Wait(DeadlockTimeout));
            CollectionAssert.AreEqual(new[] { 0, 2 }, received.ToArray());
        }
        finally
        {
            releaseFirst.Set();
            dispatcher.StopWithoutWaiting();
        }
    }

    private static AudioSamplesAvailableEventArgs CreateBlock(int value)
    {
        return new AudioSamplesAvailableEventArgs(new[] { (float)value }, 44_100);
    }
}
