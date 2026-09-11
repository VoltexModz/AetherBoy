using System.IO.Compression;
using System.Text;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxMediaDiagnosticsTests
{
    [TestMethod]
    public void WavRecordsStereoSamplesAndFinalizesHeader()
    {
        WithDirectory(root =>
        {
            string path = Path.Combine(root, "recording.wav");
            using (var recording = new LinuxWavRecorder(path, 48000, 2))
                recording.Submit([1, -1, 0.5f, 0], 48000, 2);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.AreEqual(2, BitConverter.ToInt16(bytes, 22));
            Assert.AreEqual(48000, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(8, BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual((short)32767, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual((short)-32767, BitConverter.ToInt16(bytes, 46));
        });
    }

    [TestMethod]
    public void DiagnosticReportExcludesPrivateExceptionMessagesAndCanBeExported()
    {
        WithDirectory(root =>
        {
            string report;
            using (var diagnostics = new LinuxDiagnostics(LinuxDataPaths.Isolated(root), true))
            {
                report = diagnostics.ReportPath!;
                diagnostics.Failure("save", new IOException("PRIVATE/ROM/path.gba and SAVE DATA"));
            }
            string text = File.ReadAllText(report);
            Assert.IsTrue(text.Contains("IOException"));
            Assert.IsFalse(text.Contains("PRIVATE"));
            Assert.IsFalse(text.Contains("path.gba"));
            using var active = new LinuxDiagnostics(LinuxDataPaths.Isolated(root), true);
            string zip = active.Export(root);
            using var archive = ZipFile.OpenRead(zip);
            Assert.IsNotNull(archive.GetEntry("session.jsonl"));
        });
    }

    [TestMethod]
    public void DisabledDiagnosticsCreateNoReports()
    {
        WithDirectory(root =>
        {
            using var diagnostics = new LinuxDiagnostics(LinuxDataPaths.Isolated(root), false);
            diagnostics.Failure("save", new IOException("private"));
            Assert.IsNull(diagnostics.ReportPath);
            Assert.AreEqual(0, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        });
    }

    private static void WithDirectory(Action<string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, true); }
    }
}
