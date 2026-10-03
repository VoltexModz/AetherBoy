using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using nanoboy.Controls;
using System.Windows.Forms;
using AetherBoy.Runtime.Netplay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsOnlineRoomTests
{
    [STATestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RoomDialogPersistsMaskedServerSettingsAndRequiresLoadedGame(bool host)
    {
        using var settings = new SettingsScope();
        using var main = new frmNano();
        main.OnlineLinkBrowserLauncher = _ => Assert.Fail("The room dialog must not launch a browser.");
        main.Show();
        try
        {
            Form dialog = Open(main, host);
            Assert.AreEqual(FormBorderStyle.None, dialog.FormBorderStyle);
            Assert.IsTrue(dialog.Controls.Find("aetherDialogViewport", true).Single() is AetherScrollViewport { AutoScroll: false });
            var url = FindTextBox(dialog, "Raumserver HTTPS-Adresse");
            var key = FindTextBox(dialog, "Server-Zugangsschlüssel");
            var code = FindTextBox(dialog, "Raumcode");
            Assert.IsTrue(url.Visible);
            Assert.IsTrue(key.UseSystemPasswordChar);
            Assert.IsFalse(code.Visible);

            url.Text = "https://rooms.example.com";
            key.Text = new string('b', 64); // Synthetic test value, never a real server credential.
            Button(dialog, "Server speichern").PerformClick();
            PumpUntil(() => code.Visible);
            Assert.AreEqual(new OnlineRoomSettings(url.Text, key.Text), OnlineRoomSettings.Load(settings.Path));
            Assert.IsFalse(key.Visible);

            var consent = Descendants(dialog).OfType<CheckBox>().Single();
            consent.Checked = true;
            PumpForRefresh();
            Assert.IsFalse(Button(dialog, "Raum erstellen").Enabled, "Consent alone is insufficient without a loaded ROM.");
            Assert.IsFalse(Button(dialog, "Raum beitreten").Enabled);
            Assert.IsFalse(Button(dialog, "Verbindung beenden").Enabled);
            Assert.IsNull(Field(main, "session"));
            Assert.IsNull(Field(main, "onlineRoomTransport"));

            Assert.AreSame(dialog, Open(main, !host), "Opening again must reuse the existing dialog.");
            Capture(dialog, host ? "online-rooms-host.png" : "online-rooms-guest.png");
            Button(dialog, "Zum Spiel").PerformClick();
            Assert.IsNull(Field(main, "onlineRoomDialog"));
            Assert.IsFalse(main.IsDisposed);

            using var reopened = Open(main, host);
            Button(reopened, "Server einstellen").PerformClick();
            PumpUntil(() => FindTextBox(reopened, "Server-Zugangsschlüssel").Visible);
            Assert.IsFalse(FindTextBox(reopened, "Raumcode").Visible, "The server page must hide room controls immediately, without waiting for the status timer.");
            Assert.IsFalse(Button(reopened, "Raum erstellen").Visible);
            Assert.AreEqual("https://rooms.example.com", FindTextBox(reopened, "Raumserver HTTPS-Adresse").Text);
            Assert.AreEqual(new string('b', 64), FindTextBox(reopened, "Server-Zugangsschlüssel").Text);
            Assert.IsTrue(FindTextBox(reopened, "Server-Zugangsschlüssel").UseSystemPasswordChar);
            Capture(reopened, "online-rooms-server-settings.png");
            reopened.Close();
        }
        finally { main.Close(); }
    }

    [STATestMethod]
    public void InvalidRoomServerCannotReplaceSavedConfigurationOrStartTransport()
    {
        using var settings = new SettingsScope();
        var expected = new OnlineRoomSettings("https://rooms.example.com", new string('c', 64));
        expected.Save(settings.Path);
        using var main = new frmNano();
        main.Show();
        try
        {
            using var dialog = Open(main, true);
            Button(dialog, "Server einstellen").PerformClick();
            PumpUntil(() => FindTextBox(dialog, "Raumserver HTTPS-Adresse").Visible);
            FindTextBox(dialog, "Raumserver HTTPS-Adresse").Text = "http://example.com";
            Button(dialog, "Server speichern").PerformClick();
            Assert.AreEqual(expected, OnlineRoomSettings.Load(settings.Path));
            Assert.IsTrue(FindTextBox(dialog, "Server-Zugangsschlüssel").Visible);
            Assert.IsTrue(Descendants(dialog).OfType<Label>().Any(label => label.Visible && label.Text.Contains("HTTPS", StringComparison.Ordinal)));
            Assert.IsNull(Field(main, "onlineRoomTransport"));
            dialog.Close();
        }
        finally { main.Close(); }
    }

    private static Form Open(frmNano main, bool host)
    {
        typeof(frmNano).GetMethod("ShowOnlineRoomDialog", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new object[] { host });
        Application.DoEvents();
        return (Form)Field(main, "onlineRoomDialog")!;
    }

    private static object? Field(frmNano main, string name) => typeof(frmNano)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main);
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
        .SelectMany(control => new[] { control }.Concat(Descendants(control)));
    private static nanoboy.Controls.AetherTextBox FindTextBox(Control parent, string accessibleName) => Descendants(parent)
        .OfType<nanoboy.Controls.AetherTextBox>().Single(control => control.AccessibleName == accessibleName);
    private static Button Button(Control parent, string text) => Descendants(parent)
        .OfType<Button>().Single(control => control.Text == text);
    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5)) Assert.Fail("The room dialog did not reach its expected state.");
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
    private static void PumpForRefresh()
    {
        var elapsed = Stopwatch.StartNew();
        PumpUntil(() => elapsed.Elapsed >= TimeSpan.FromMilliseconds(250));
    }
    private static void Capture(Form dialog, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Application.DoEvents();
        using var bitmap = new Bitmap(dialog.ClientSize.Width, dialog.ClientSize.Height);
        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(System.IO.Path.Combine(directory, name));
    }

    private sealed class SettingsScope : IDisposable
    {
        // AssemblyInitialize redirects WindowsDataPaths to the isolated test directory.
        internal string Path { get; } = System.IO.Path.Combine(WindowsDataPaths.Default.Settings, "online-room.json");
        private readonly byte[]? previous;
        internal SettingsScope()
        {
            if (File.Exists(Path)) { previous = File.ReadAllBytes(Path); File.Delete(Path); }
        }
        public void Dispose()
        {
            if (previous is not null) File.WriteAllBytes(Path, previous);
            else if (File.Exists(Path)) File.Delete(Path);
        }
    }
}
