using AetherBoy.Runtime;
using AetherBoy.Runtime.Audio;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class AudioQueuePolicyTests
{
    [TestMethod]
    public void NewlyLoadedSessionWinsEvenIfPreviousSessionHadMoreTurboTransitions()
    {
        var cursor = new AudioPlaybackCursor();
        Assert.IsTrue(cursor.TryAccept(1, 500, out bool changed));
        Assert.IsTrue(changed);
        Assert.IsTrue(cursor.TryAccept(2, 1, out changed));
        Assert.IsTrue(changed);
        Assert.IsFalse(cursor.TryAccept(1, 999, out changed));
        Assert.IsFalse(changed);
        Assert.IsTrue(cursor.TryAccept(2, 1, out changed));
        Assert.IsFalse(changed);
    }

    [TestMethod]
    public async Task SessionTagsNewAudioAcrossTurboPauseAndNormalPlayback()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);
        var machine = await factory.Created.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var blocks = System.Threading.Channels.Channel.CreateUnbounded<long>();
        session.AudioSamplesAvailable += (_, block) => blocks.Writer.TryWrite(block.PlaybackGeneration);
        pacer.WaitForWaitCount(1, TimeSpan.FromSeconds(10));
        machine.EmitAudioOnNextFrame = true;
        pacer.ReleaseOneFrame();
        long normal = await blocks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        pacer.WaitForWaitCount(2, TimeSpan.FromSeconds(10));
        machine.EmitAudioOnNextFrame = true;
        Task turbo = session.SetTurboAsync(true);
        pacer.ReleaseOneFrame();
        await turbo.WaitAsync(TimeSpan.FromSeconds(10));
        long fast = await blocks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(10));
        await session.SetTurboAsync(false).WaitAsync(TimeSpan.FromSeconds(10));
        machine.EmitAudioOnNextFrame = true;
        await session.SetPausedAsync(false).WaitAsync(TimeSpan.FromSeconds(10));
        long resumed = await blocks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(normal > 0 && fast > normal && resumed > fast);
        await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    [DataRow(0, 300, 800, 300)]
    [DataRow(600, 300, 800, 0)]
    [DataRow(500, 300, 800, 300)]
    [DataRow(0, 1600, 800, 800)]
    [DataRow(800, 1, 800, 0)]
    public void WindowsAndLinuxShareContiguousBoundedAdmission(int queued, int incoming, int capacity, int expected)
        => Assert.AreEqual(expected, AudioQueuePolicy.AcceptedFrames(queued, incoming, capacity));

    [TestMethod]
    public void PlaybackMetadataDoesNotMutateSamplesOrTheSourceEvent()
    {
        var source = new AudioSamplesAvailableEventArgs(new[] { .1f, .2f, .3f, .4f }, 65536, 2);
        var tagged = source.WithPlaybackGeneration(42);
        Assert.AreEqual(0L, source.PlaybackGeneration);
        Assert.AreEqual(42L, tagged.PlaybackGeneration);
        Assert.AreEqual(source.SampleCount, tagged.SampleCount);
        CollectionAssert.AreEqual(source.GetInterleavedSamplesCopy(), tagged.GetInterleavedSamplesCopy());
        tagged.GetInterleavedSamplesCopy()[0] = 99;
        Assert.AreEqual(.1f, source.GetInterleavedSamplesCopy()[0]);
    }
}
