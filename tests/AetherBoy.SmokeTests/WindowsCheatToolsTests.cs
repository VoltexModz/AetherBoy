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
    [DataRow("de")]
    [DataRow("en")]
    public void GbaManagerExposesAllFormatsAndDeviceButtonWithReadableWarning(string language)
    {
        string previousLanguage = AetherBoy.Runtime.Localization.UiText.Language;
        AetherBoy.Runtime.Localization.UiText.Initialize(language);
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
            session.AddCheatAsync("1 Hit Kill (Test)", "95EDFBBA A5A72A78\nC833D1A0 02FA7205").GetAwaiter().GetResult();
            using var form = new frmCheats(session);
            form.Show(); Application.DoEvents();
            var formats = (AetherSelect)form.Controls.Find("cheatCodeFormat", true).Single();
            var device = (AetherCheckBox)form.Controls.Find("cheatDeviceButton", true).Single();
            var list = (AetherList)form.Controls.Find("lstCheats", true).Single();
            var warning = (Label)form.Controls.Find("lblExperimentalInfo", true).Single();
            Assert.IsTrue(formats.Visible); Assert.AreEqual(6, formats.Items.Count);
            Assert.IsTrue(device.Visible);
            Assert.IsTrue(list.CheckBoxes);
            Assert.IsTrue(list.Items[0].Checked);
            Assert.AreEqual("Action Replay v3", list.Items[0].SubItems[3].Text);
            Assert.IsEmpty(form.Controls.Find("btnToggle", true));
            var measured = TextRenderer.MeasureText(warning.Text, warning.Font,
                new Size(warning.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Assert.IsLessThanOrEqualTo(warning.Height, measured.Height);
            Assert.IsFalse(formats.Bounds.IntersectsWith(device.Bounds));
            Assert.IsFalse(warning.Bounds.IntersectsWith(device.Bounds));
            list.Items[0].Selected = true;
            list.AccessibilityObject.GetChild(0)!.DoDefaultAction();
            PumpUntil(() => list.Enabled && !session.LatestSnapshot.Cheats[0].Enabled);
            Assert.IsFalse(list.Items[0].Checked);
            Assert.IsFalse(list.AccessibilityObject.GetChild(0)!.State.HasFlag(AccessibleStates.Checked));
            typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(list, [new KeyEventArgs(Keys.Space)]);
            PumpUntil(() => list.Enabled && session.LatestSnapshot.Cheats[0].Enabled);
            Assert.IsTrue(list.Items[0].Checked);
            device.AccessibilityObject.DoDefaultAction();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((!session.LatestSnapshot.CheatButtonPressed || !device.Enabled) && DateTime.UtcNow < deadline)
            { Application.DoEvents(); Thread.Sleep(10); }
            Assert.IsTrue(session.LatestSnapshot.CheatButtonPressed);
            Assert.IsTrue(device.Checked);
            string output = Path.Combine(AppContext.BaseDirectory, "ui-captures");
            Directory.CreateDirectory(output);
            foreach (var theme in UiThemePresets.All)
            {
                AetherColors.Apply(new UiThemePalette(theme.Primary, theme.Secondary, theme.Background));
                Application.DoEvents();
                using var capture = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(capture, new Rectangle(Point.Empty, form.Size));
                capture.Save(Path.Combine(output, $"cheats-checkbox-{language}-{theme.Id}.png"));
            }
            form.Close();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            AetherBoy.Runtime.Localization.UiText.Initialize(previousLanguage);
            AetherColors.Apply(new(null, null, null));
        }
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
        Assert.IsTrue(condition(), "Cheat UI did not acknowledge the runtime state.");
    }

    [STATestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void PasteReviewPreservesInputOffersFormatsAndGuardsPlaceholders(string language)
    {
        string previous = AetherBoy.Runtime.Localization.UiText.Language;
        AetherBoy.Runtime.Localization.UiText.Initialize(language);
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-cheat-paste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] rom = new byte[512]; BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE);
            string path = Path.Combine(root, "test.gba"); File.WriteAllBytes(path, rom);
            using var session = new EmulationSession(path, Path.Combine(root, "test.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            using var form = new frmCheats(session); form.Show(); Application.DoEvents();
            T Find<T>(string name) where T : Control => (T)form.Controls.Find(name, true).Single();
            var input = Find<AetherTextBox>("txtCode"); var details = Find<AetherTextBox>("cheatReviewDetails");
            var confirm = Find<AetherCheckBox>("confirmCheatValues");
            var list = Find<AetherList>("cheatInputReview");
            Find<AetherTextBox>("txtName").Text = "Test";
            input.Text = "C12BBBE1 D1ED426C";
            Assert.IsTrue(Find<AetherButton>("chooseCheatGameShark").Visible);
            Assert.IsTrue(Find<AetherButton>("chooseCheatActionReplay").Visible);
            Find<AetherButton>("chooseCheatActionReplay").PerformClick();
            Assert.AreEqual((int)CheatCodeFormat.ActionReplayV3, Find<AetherSelect>("cheatCodeFormat").SelectedIndex);
            Assert.IsFalse(Find<AetherButton>("chooseCheatActionReplay").Visible);
            Assert.AreEqual("C12BBBE1 D1ED426C", input.Text);
            Find<AetherSelect>("cheatCodeFormat").SelectedIndex = 0;
            input.Text = "Cheat code:\r\n82025840 0044svg";
            Find<AetherButton>("btnAdd").PerformClick(); Application.DoEvents();
            Assert.IsEmpty(session.LatestSnapshot.Cheats);
            Assert.HasCount(2, list.Items); StringAssert.Contains(details.Text, language == "de" ? "Zeile" : "Line");
            input.Text = "82025840 AAAA"; Assert.IsTrue(confirm.Visible); Assert.IsFalse(confirm.Checked);
            Find<AetherButton>("btnAdd").PerformClick(); Assert.IsEmpty(session.LatestSnapshot.Cheats);
            confirm.Checked = true; input.Text = "82025840 0044"; Assert.IsFalse(confirm.Checked); Assert.IsFalse(confirm.Visible);
            string output = Path.Combine(AppContext.BaseDirectory, "ui-captures"); Directory.CreateDirectory(output);
            foreach (var theme in UiThemePresets.All)
            {
                input.Text = theme.Id == "pocket-light" ? "82025840 AAAA" : "C12BBBE1 D1ED426C";
                AetherColors.Apply(new UiThemePalette(theme.Primary, theme.Secondary, theme.Background)); Application.DoEvents();
                using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(output, $"cheat-review-{language}-{theme.Id}.png"));
            }
            form.Close();
        }
        finally
        {
            Directory.Delete(root, true); AetherBoy.Runtime.Localization.UiText.Initialize(previous);
            AetherColors.Apply(new(null, null, null));
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GameBoyAndColorUseTheSameCheckedSessionList(bool color)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-gb-cheat-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] rom = new byte[0x8000];
            rom[0x100] = 0x18; rom[0x101] = 0xFE; // JR .
            rom[0x143] = color ? (byte)0x80 : (byte)0;
            string path = Path.Combine(root, color ? "synthetic.gbc" : "synthetic.gb");
            File.WriteAllBytes(path, rom);
            using var session = new EmulationSession(path, Path.Combine(root, "synthetic.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            session.AddCheatAsync("test", "01FF00C0").GetAwaiter().GetResult();
            using var form = new frmCheats(session); form.Show(); Application.DoEvents();
            Assert.IsFalse(form.Controls.Find("cheatCodeFormat", true).Single().Visible);
            Assert.IsFalse(form.Controls.Find("cheatDeviceButton", true).Single().Visible);
            var list = (AetherList)form.Controls.Find("lstCheats", true).Single();
            Assert.IsTrue(list.Items[0].Checked);
            list.RequestItemCheck(list.Items[0]);
            PumpUntil(() => list.Enabled && !session.LatestSnapshot.Cheats[0].Enabled);
            Assert.IsFalse(list.Items[0].Checked);
            form.Close();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
