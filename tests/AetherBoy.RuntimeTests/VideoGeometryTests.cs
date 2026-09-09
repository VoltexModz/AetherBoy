using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class VideoGeometryTests
{
    [TestMethod]
    public void DefinesNativeGameBoyAndAdvanceDimensions()
    {
        Assert.AreEqual(new VideoGeometry(160, 144), VideoGeometry.GameBoy);
        Assert.AreEqual(new VideoGeometry(240, 160), VideoGeometry.GameBoyAdvance);
        Assert.AreEqual(38_400, VideoGeometry.GameBoyAdvance.PixelCount);
        Assert.AreEqual(EmulationSnapshot.FramePixelCount, VideoGeometry.GameBoy.PixelCount);
    }

    [TestMethod]
    [DataRow(0, 160)]
    [DataRow(240, -1)]
    [DataRow(1025, 160)]
    [DataRow(240, int.MaxValue)]
    public void RejectsInvalidOrUnboundedDimensions(int width, int height)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VideoGeometry(width, height));
    }

    [TestMethod]
    public void ExchangesEntireAdvanceFrameAndKeepsPublishedBufferIsolated()
    {
        var exchange = new FrameExchange();
        exchange.Configure(VideoGeometry.GameBoyAdvance);
        int[] writer = exchange.WriteBuffer;
        writer[0] = 10;
        writer[^1] = 99;
        exchange.Publish();
        exchange.WriteBuffer[0] = 20;

        int[] pixels = new int[38_400];
        long sequence = 0;
        Assert.IsTrue(exchange.TryCopyLatestFrame(pixels, ref sequence));
        Assert.AreEqual(10, pixels[0]);
        Assert.AreEqual(99, pixels[^1]);
        Assert.IsFalse(exchange.TryCopyLatestFrame(pixels, ref sequence));
        Assert.ThrowsExactly<ArgumentException>(() =>
            exchange.TryCopyLatestFrame(new int[23_040], ref sequence));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            exchange.Configure(VideoGeometry.GameBoy));
    }

    [TestMethod]
    public async Task OwnerPublishesGeometryBeforeConsumerCopiesFrameAndRetainsItOnShutdown()
    {
        var machine = new RecordingMachine(null, VideoGeometry.GameBoyAdvance);
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(() => machine, pacer);
        pacer.WaitForWaitCount(1, TimeSpan.FromSeconds(10));

        Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.LatestSnapshot.VideoGeometry);
        int[] frame = new int[session.LatestSnapshot.VideoGeometry.PixelCount];
        long sequence = 0;
        Assert.IsTrue(session.TryCopyLatestFrame(frame, ref sequence));
        Assert.AreEqual(38_400, frame.Length);
        await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.LatestSnapshot.VideoGeometry);
    }
}
