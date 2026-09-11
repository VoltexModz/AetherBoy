using AetherBoy.Runtime;

namespace AetherBoy.Runtime.Tests;

[TestClass]
public sealed class StereoAudioTests
{
    [TestMethod]
    public void StereoPreservesChannelSeparationAndLegacyMonoConsumers()
    {
        float[] input = [1, -1, 0.5f, 0];
        var block = new AudioSamplesAvailableEventArgs(input, 65536, 2);
        input[0] = 0;
        Assert.AreEqual(2, block.Channels);
        Assert.AreEqual(2, block.SampleCount);
        CollectionAssert.AreEqual(new float[] { 1, -1, 0.5f, 0 }, block.GetInterleavedSamplesCopy());
        CollectionAssert.AreEqual(new float[] { 0, 0.25f }, block.GetSamplesCopy());
        float[] mono = new float[2];
        block.CopySamplesTo(mono);
        CollectionAssert.AreEqual(new float[] { 0, 0.25f }, mono);
    }
}
