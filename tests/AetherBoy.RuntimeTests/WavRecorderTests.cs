using System.Text;
using AetherBoy.Runtime.Audio;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class WavRecorderTests
{
    [TestMethod]
    public void StartAddSamplesStopWritesValidPcmWaveFile()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "recording.wav");

        try
        {
            float[] samples = [-2f, -1f, -0.5f, 0f, 0.5f, 1f, 2f];

            using (var recorder = new WavRecorder())
            {
                recorder.Start(path, 44_100);
                recorder.AddSamples(samples);
                recorder.Stop();
            }

            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            Assert.AreEqual("RIFF", ReadFourCc(reader));
            Assert.AreEqual(36 + samples.Length * sizeof(short), reader.ReadInt32());
            Assert.AreEqual("WAVE", ReadFourCc(reader));
            Assert.AreEqual("fmt ", ReadFourCc(reader));
            Assert.AreEqual(16, reader.ReadInt32());
            Assert.AreEqual((short)1, reader.ReadInt16());
            Assert.AreEqual((short)1, reader.ReadInt16());
            Assert.AreEqual(44_100, reader.ReadInt32());
            Assert.AreEqual(88_200, reader.ReadInt32());
            Assert.AreEqual((short)2, reader.ReadInt16());
            Assert.AreEqual((short)16, reader.ReadInt16());
            Assert.AreEqual("data", ReadFourCc(reader));
            Assert.AreEqual(samples.Length * sizeof(short), reader.ReadInt32());

            short[] expectedPcm =
            [
                -32_767,
                -32_767,
                -16_383,
                0,
                16_383,
                32_767,
                32_767
            ];

            foreach (short expectedSample in expectedPcm)
            {
                Assert.AreEqual(expectedSample, reader.ReadInt16());
            }

            Assert.AreEqual(44 + samples.Length * sizeof(short), stream.Length);
            Assert.AreEqual(stream.Length, stream.Position);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [TestMethod]
    public async Task AddSamplesAndStopAreAtomicAndLeaveFinalizedHeader()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "concurrent.wav");

        try
        {
            using var recorder = new WavRecorder();
            recorder.Start(path, 44_100);

            float[] samples = Enumerable.Repeat(0.25f, 65_536).ToArray();
            using var workersReady = new CountdownEvent(2);
            using var startGate = new ManualResetEventSlim(false);

            Task addTask = Task.Run(() =>
            {
                workersReady.Signal();
                startGate.Wait();
                recorder.AddSamples(samples);
            });

            Task stopTask = Task.Run(() =>
            {
                workersReady.Signal();
                startGate.Wait();
                recorder.Stop();
            });

            Assert.IsTrue(workersReady.Wait(TimeSpan.FromSeconds(10)), "Die Worker wurden nicht rechtzeitig bereit.");
            startGate.Set();
            await Task.WhenAll(addTask, stopTask);

            Assert.IsFalse(recorder.IsRecording);

            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            Assert.AreEqual("RIFF", ReadFourCc(reader));
            int riffSize = reader.ReadInt32();
            Assert.AreEqual("WAVE", ReadFourCc(reader));
            Assert.AreEqual("fmt ", ReadFourCc(reader));
            Assert.AreEqual(16, reader.ReadInt32());
            Assert.AreEqual((short)1, reader.ReadInt16());
            Assert.AreEqual((short)1, reader.ReadInt16());
            Assert.AreEqual(44_100, reader.ReadInt32());
            Assert.AreEqual(88_200, reader.ReadInt32());
            Assert.AreEqual((short)2, reader.ReadInt16());
            Assert.AreEqual((short)16, reader.ReadInt16());
            Assert.AreEqual("data", ReadFourCc(reader));
            int dataSize = reader.ReadInt32();

            int completeBufferSize = samples.Length * sizeof(short);
            Assert.IsTrue(
                dataSize == 0 || dataSize == completeBufferSize,
                $"Der atomare Sample-Block muss vollständig geschrieben oder vollständig verworfen werden; tatsächlich: {dataSize} Bytes.");
            Assert.AreEqual(36 + dataSize, riffSize);
            Assert.AreEqual(44L + dataSize, stream.Length);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    [TestMethod]
    public void FailedStartDoesNotPoisonRecorderOrLeakTheNextOutput()
    {
        string tempDirectory = CreateTempDirectory();
        string validPath = Path.Combine(tempDirectory, "recovered.wav");

        try
        {
            using var recorder = new WavRecorder();

            Assert.Throws<UnauthorizedAccessException>(() => recorder.Start(tempDirectory));
            Assert.IsFalse(recorder.IsRecording);

            recorder.Start(validPath, 44_100);
            recorder.AddSamples([0.25f]);
            recorder.Stop();

            Assert.IsFalse(recorder.IsRecording);
            Assert.AreEqual(46L, new FileInfo(validPath).Length);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        return Encoding.ASCII.GetString(reader.ReadBytes(4));
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AetherBoy.CoreTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
