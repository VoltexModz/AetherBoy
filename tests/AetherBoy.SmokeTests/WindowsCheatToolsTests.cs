using System.Buffers.Binary;
using System.Drawing;
using System.Windows.Forms;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsCheatToolsTests
{
    [STATestMethod]
    public void GbaManagerExposesAllFormatsAndDeviceButtonWithReadableWarning()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-cheat-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] rom = new byte[512];
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE); // ARM B .
            string path = Path.Combine(root, "synthetic.gba");
            File.WriteAllBytes(path, rom);
            using var session = new EmulationSession(path, Path.Combine(root, "synthetic.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            using var form = new frmCheats(session);
            form.Show(); Application.DoEvents();
            var formats = (AetherSelect)form.Controls.Find("cheatCodeFormat", true).Single();
            var device = (AetherButton)form.Controls.Find("cheatDeviceButton", true).Single();
            var warning = (Label)form.Controls.Find("lblExperimentalInfo", true).Single();
            Assert.IsTrue(formats.Visible); Assert.AreEqual(6, formats.Items.Count);
            Assert.IsTrue(device.Visible);
            var measured = TextRenderer.MeasureText(warning.Text, warning.Font,
                new Size(warning.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Assert.IsLessThanOrEqualTo(warning.Height, measured.Height);
            Assert.IsFalse(formats.Bounds.IntersectsWith(device.Bounds));
            Assert.IsFalse(warning.Bounds.IntersectsWith(device.Bounds));
            device.PerformClick();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((!session.LatestSnapshot.CheatButtonPressed || !device.Enabled) && DateTime.UtcNow < deadline)
            { Application.DoEvents(); Thread.Sleep(10); }
            Assert.IsTrue(session.LatestSnapshot.CheatButtonPressed);
            StringAssert.Contains(device.Text, "gehalten");
            string output = Path.Combine(AppContext.BaseDirectory, "ui-captures");
            Directory.CreateDirectory(output);
            using var capture = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(capture, new Rectangle(Point.Empty, form.Size));
            capture.Save(Path.Combine(output, "phase2-gba-cheats.png"));
            form.Close();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
