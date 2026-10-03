using System.Buffers.Binary;
using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class BootIntroTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aether-intro-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void PreferencesDefaultOnAndPersistIndependentlyWithRelativeAssetNames()
    {
        var store = new BootIntroStore(Path.Combine(root, "profile"));
        Assert.AreEqual(new BootIntroOptions(), store.Load()); Assert.IsFalse(Directory.Exists(store.DirectoryPath));
        store.Save(new(false, false)); Assert.AreEqual(new BootIntroOptions(false, false), new BootIntroStore(store.DirectoryPath).Load());
        File.WriteAllText(Path.Combine(store.DirectoryPath, "intro.json"), "broken");
        Assert.IsFalse(store.Load().Enabled, "Corrupt preferences must not trap the user in an intro.");
    }

    [TestMethod]
    public void AudioImportCopiesSourceAndRejectsCorruptOversizeAndTraversal()
    {
        var store = new BootIntroStore(Path.Combine(root, "data"));
        string source = Path.Combine(root, "original.wav"); var wav = Wave(16000, 2, 4000); File.WriteAllBytes(source, wav);
        string name = store.Import(source, false); Assert.AreEqual(name, store.Import(source, false));
        CollectionAssert.AreEqual(wav, File.ReadAllBytes(source));
        File.Delete(source); CollectionAssert.AreEqual(wav, store.ReadAsset(name, false));
        Assert.IsNull(store.ReadAsset("../../original.wav", false)); Assert.IsNull(store.ReadAsset(Path.Combine(root, name), false));
        string corrupt = Path.Combine(root, "bad.wav"); File.WriteAllBytes(corrupt, [1, 2, 3]);
        Assert.ThrowsExactly<InvalidDataException>(() => store.Import(corrupt, false));
        using (var file = File.Create(corrupt)) file.SetLength(BootIntroStore.MaxAssetBytes + 1L);
        Assert.ThrowsExactly<InvalidDataException>(() => store.Import(corrupt, false));
        Assert.AreEqual(1, Directory.GetFiles(store.DirectoryPath).Length);
        using(var file=File.Create(Path.Combine(store.DirectoryPath,name))) file.SetLength(BootIntroStore.MaxAssetBytes+1L);
        Assert.IsNull(store.ReadAsset(name,false), "A damaged managed asset must fall back, not block opening a game.");
    }

    [TestMethod]
    [DataRow(8000, 1)][DataRow(22050, 2)][DataRow(44100, 1)][DataRow(48000, 2)]
    public void WaveResamplesToMonoAndBoundsDuration(int rate, int channels)
    {
        var samples = BootIntroStore.DecodeWave(Wave(rate, channels, rate));
        Assert.AreEqual(44100, samples.Length); Assert.AreEqual(0f, samples[0]); Assert.AreEqual(0f, samples[^1]);
        Assert.IsTrue(samples.All(v => float.IsFinite(v) && Math.Abs(v) <= 1));
        Assert.ThrowsExactly<InvalidDataException>(() => BootIntroStore.DecodeWave(Wave(rate, channels, rate*3)));
        var bad = Wave(rate, channels, rate); bad[34] = 8;
        Assert.ThrowsExactly<InvalidDataException>(() => BootIntroStore.DecodeWave(bad));
        bad = Wave(rate, channels, rate); bad[32] = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => BootIntroStore.DecodeWave(bad));
        Assert.ThrowsExactly<InvalidDataException>(() => BootIntroStore.DecodeWave(bad.AsSpan(0, 40)));
    }

    [TestMethod]
    public void ImageBoundsAreCheckedBeforePlatformDecodeAndFailedDecodePublishesNothing()
    {
        var png = new byte[33]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(png,0);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(8),13); "IHDR"u8.CopyTo(png.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16),2049); BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20),1);
        Assert.ThrowsExactly<InvalidDataException>(() => BootIntroStore.ValidatePng(png));
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16),1);
        Assert.AreEqual((1,1), BootIntroStore.ValidatePng(png));
        var store = new BootIntroStore(Path.Combine(root,"images")); string path = Path.Combine(root,"truncated.png"); File.WriteAllBytes(path,png);
        Assert.ThrowsExactly<InvalidDataException>(() => store.Import(path,true,_ => throw new InvalidDataException("decoder rejected")));
        Assert.IsFalse(Directory.Exists(store.DirectoryPath));
    }

    [TestMethod]
    public void BuiltInChimeAndAnimationHaveBoundedOriginalOutputAndSafeFallback()
    {
        var samples = BootIntroStore.CreateChime();
        Assert.AreEqual(105840, samples.Length); Assert.IsTrue(samples.Any(v => Math.Abs(v) > .1));
        Assert.IsTrue(samples.All(v => float.IsFinite(v) && Math.Abs(v) < 1));
        CollectionAssert.AreEqual(samples, new BootIntroStore(root).ReadSound(new(Sound: "../../secret")));
        Assert.AreEqual(0f, BootIntroFrame.At(0).Opacity); Assert.AreEqual(1f, BootIntroFrame.At(800).Opacity);
        Assert.AreEqual(0f, BootIntroFrame.At(2400).Opacity); Assert.AreEqual(1f, BootIntroFrame.At(800).Scale);
    }

    internal static byte[] Wave(int rate, int channels, int frames)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write("RIFF"u8); writer.Write(36 + frames*channels*2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)channels); writer.Write(rate); writer.Write(rate*channels*2);
        writer.Write((ushort)(channels*2)); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(frames*channels*2);
        for(int i=0;i<frames*channels;i++) writer.Write((short)(Math.Sin(i*.1)*12000));
        return output.ToArray();
    }
}
