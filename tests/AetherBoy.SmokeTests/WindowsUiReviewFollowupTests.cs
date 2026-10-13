using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsUiReviewFollowupTests
{
    [STATestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void ChromeNamesAndSelectedButtonStatesPreserveButtonActions(string language)
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize(language);
            using var main = new frmNano();
            var buttons = Descendants(main).OfType<AetherButton>().ToArray();
            foreach (string text in new[] { "Fenster schließen", "Fenster minimieren", "Fenster maximieren" })
                Assert.IsTrue(buttons.Any(button => button.AccessibilityObject.Name == UiText.Get(text)), text);
            var maximize = buttons.Single(button => button.AccessibleName == UiText.Get("Fenster maximieren"));
            main.WindowState = FormWindowState.Maximized;
            Call(main, "UpdateAetherWindowButton");
            Assert.AreEqual(UiText.Get("Fenster wiederherstellen"), maximize.AccessibilityObject.Name);
            main.WindowState = FormWindowState.Normal;
            using var window = new AetherWindow { Text = "Test", ClientSize = new(400, 180) };
            AetherDialog.Apply(window, "Test", "Test", showMinimize: true);
            Assert.AreEqual(UiText.Get("Fenster schließen"), window.Controls.Find("aetherDialogCloseButton", true).Single().AccessibilityObject.Name);
            Assert.AreEqual(UiText.Get("Fenster minimieren"), window.Controls.Find("aetherDialogMinimizeButton", true).Single().AccessibilityObject.Name);
            var button = new AetherButton { Text = "Action" }; window.Controls.Add(button);
            int clicks = 0; button.Click += (_, _) => clicks++;
            button.AccessibilityObject.DoDefaultAction(); Assert.AreEqual(0, clicks, "A handleless control must not activate.");
            window.Show(); Application.DoEvents();
            Assert.AreEqual(AccessibleRole.PushButton, button.AccessibilityObject.Role);
            button.AccessibleRole = AccessibleRole.Link;
            Assert.AreEqual(AccessibleRole.Link, button.AccessibilityObject.Role, "Explicit accessibility roles remain authoritative.");
            button.AccessibleRole = AccessibleRole.Default;
            Assert.AreEqual(AccessibleRole.PushButton, button.AccessibilityObject.Role);
            Assert.IsFalse(button.AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
            button.Selected = true;
            Assert.IsTrue(button.AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
            Assert.IsFalse(button.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));
            typeof(Control).GetMethod("RecreateHandle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(button, null);
            Assert.IsTrue(button.IsHandleCreated);
            Assert.AreEqual(AccessibleRole.PushButton, button.AccessibilityObject.Role);
            Assert.IsTrue(button.AccessibilityObject.State.HasFlag(AccessibleStates.Selected), "Selected state must survive native handle recreation.");
            button.AccessibilityObject.DoDefaultAction(); Assert.AreEqual(1, clicks);
            button.Selected = false; button.Enabled = false;
            Assert.IsFalse(button.AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
            Assert.IsTrue(button.AccessibilityObject.State.HasFlag(AccessibleStates.Unavailable));
            button.AccessibilityObject.DoDefaultAction(); Assert.AreEqual(1, clicks);
            button.Enabled = true; button.Visible = false;
            button.AccessibilityObject.DoDefaultAction(); Assert.AreEqual(1, clicks, "A hidden control must not bypass CanSelect.");
            var accessibility = button.AccessibilityObject; button.Dispose();
            accessibility.DoDefaultAction(); Assert.AreEqual(1, clicks, "A disposed control must not activate.");
        }
        finally { UiText.Initialize(previous); }
    }

    [STATestMethod]
    [DataRow("cancel")]
    [DataRow("close")]
    [DataRow("dispose")]
    public void PickerRemainsCancelableDuringBlockedReadAndIgnoresLateResult(string action)
    {
        string root = TempDirectory();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); using var completed = new ManualResetEventSlim();
        try
        {
            using var picker = new AetherFileDialog { InitialDirectory = root, FileName = "preserve.sav", ReadEntries = (path, pattern, token) =>
            { entered.Set(); release.Wait(); completed.Set(); return [new(Path.Combine(path, "late.sav"), false)]; } };
            picker.Build();
            if (action == "cancel") { picker.Window!.Show(); Application.DoEvents(); }
            else Assert.IsFalse(picker.Window!.IsHandleCreated, "Closing/disposal must work before native handles exist.");
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
            Assert.IsTrue(picker.IsBusy); Assert.IsFalse(((Control)picker.Window.AcceptButton!).Enabled);
            var cancel = (AetherButton)picker.Window.Controls.Find("fileCancel", true).Single();
            Assert.IsTrue(cancel.Enabled);
            var closeTime = Stopwatch.StartNew();
            if (action == "dispose") picker.Dispose();
            else if (action == "close") picker.Window.Close();
            else cancel.PerformClick();
            Assert.IsTrue(closeTime.Elapsed < TimeSpan.FromSeconds(1), "Closing must not wait for an uncancellable filesystem call.");
            Assert.AreEqual("preserve.sav", picker.FileName); Assert.IsFalse(picker.IsBusy);
            release.Set(); Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(3))); picker.PollNavigation();
            Assert.AreEqual("preserve.sav", picker.FileName);
        }
        finally { release.Set(); Directory.Delete(root, true); }
    }

    [STATestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void PickerNewPathAndFilterDiscardOlderSuccessAndFailure(bool obsoleteFailure, bool rebuild)
    {
        string root = TempDirectory();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); using var completed = new ManualResetEventSlim();
        try
        {
            string first = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
            using var picker = new AetherFileDialog { InitialDirectory = first, Filter = "Saves|*.sav|Games|*.gb", ReadEntries = (path, pattern, token) =>
            {
                if (path == first)
                {
                    entered.Set(); release.Wait(); completed.Set();
                    if (obsoleteFailure) throw new IOException("obsolete failure");
                    return [new(Path.Combine(path, "obsolete.sav"), false)];
                }
                return [new(Path.Combine(path, pattern == "*.sav" ? "new.sav" : "new.gb"), false)];
            } };
            picker.Build(); Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
            if (rebuild) { picker.InitialDirectory = second; picker.Build(); }
            else picker.LoadDirectory(second);
            WaitPicker(picker);
            var files = (AetherList)picker.Window!.Controls.Find("fileEntries", true).Single();
            Assert.AreEqual("new.sav", files.Items.Single().Text);
            var filter = Descendants(picker.Window).OfType<AetherSelect>().Single(select => select.AccessibleName == UiText.Get("Dateityp"));
            filter.SelectedIndex = 1; WaitPicker(picker);
            release.Set(); Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(3))); picker.PollNavigation();
            Assert.AreEqual("new.gb", files.Items.Single().Text);
            Assert.IsFalse(picker.Window.Controls.Find("fileStatus", true).Single().Text.Contains("obsolete failure", StringComparison.Ordinal));
            picker.Dispose(); picker.Dispose();
        }
        finally { release.Set(); Directory.Delete(root, true); }
    }

    [STATestMethod]
    public void SlotInventoryRejectsPreviousSessionAndReportsReadFailures()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        object first = new(), second = new();
        var inventory = new AetherSaveSlotInventory { Inspect = (rom, slot) =>
        {
            if (rom == "old") { entered.Set(); release.Wait(); return AetherSaveSlotState.Saved; }
            if (slot == 3) throw new IOException("synthetic unreadable slot");
            return slot == 2 ? AetherSaveSlotState.Saved : AetherSaveSlotState.Empty;
        } };
        try
        {
            inventory.Update(first, "old"); Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
            inventory.Update(second, "new"); Assert.AreEqual(AetherSaveSlotState.Reading, inventory[1]);
            // The replacement inventory must complete even while the obsolete OS call is blocked.
            Wait(() => { inventory.Update(second, "new"); return inventory[1] == AetherSaveSlotState.Empty; });
            release.Set();
            Assert.AreEqual(AetherSaveSlotState.Saved, inventory[2]); Assert.AreEqual(AetherSaveSlotState.Unreadable, inventory[3]);
            inventory.Update(null, null); Assert.AreEqual(AetherSaveSlotState.NoGame, inventory[2]);
        }
        finally { release.Set(); }
    }

    [TestMethod]
    public void SlotInspectionDistinguishesAbsentUnreadableAndRecognizableStates()
    {
        string root = TempDirectory();
        try
        {
            string path = Path.Combine(root, "slot.ss1");
            Assert.AreEqual(AetherSaveSlotState.Empty, AetherSaveSlotInventory.InspectFile(path));
            File.WriteAllBytes(path, []);
            Assert.AreEqual(AetherSaveSlotState.Unreadable, AetherSaveSlotInventory.InspectFile(path));
            File.WriteAllBytes(path, new byte[96]);
            Assert.AreEqual(AetherSaveSlotState.Unreadable, AetherSaveSlotInventory.InspectFile(path));
            foreach (byte[] magic in new[] { "AETHSTAT"u8.ToArray(), "AETHGBA\0"u8.ToArray() })
            {
                var bytes = new byte[96]; magic.CopyTo(bytes, 0); File.WriteAllBytes(path, bytes);
                Assert.AreEqual(AetherSaveSlotState.Saved, AetherSaveSlotInventory.InspectFile(path));
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Assert.AreEqual(AetherSaveSlotState.Unreadable, AetherSaveSlotInventory.InspectFile(path));
            }
            var truncated = new byte[82]; "AETHGBA\0"u8.CopyTo(truncated); File.WriteAllBytes(path, truncated);
            Assert.AreEqual(AetherSaveSlotState.Unreadable, AetherSaveSlotInventory.InspectFile(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [STATestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    public void MainSaveSlotsFitTheScrollableRailAtMinimumSize(int scale)
    {
        using var settings = new NanoboySettings();
        int previousScale = settings.UiScalePercent;
        try
        {
            settings.UiScalePercent = scale;
            using var main = new frmNano();
            main.Size = main.MinimumSize;
            main.Show(); Application.DoEvents();
            foreach (AetherButton slot in Field<AetherButton[]>(main, "aetherSlotButtons"))
            {
                Assert.IsTrue(slot.Visible, "The save-slot bounds must be checked in a visible shell.");
                Assert.IsTrue(slot.Parent!.ClientRectangle.Contains(slot.Bounds),
                    $"Slot {slot.Text} must fit with the rail scrollbar at {scale}%: {slot.Bounds} inside {slot.Parent.ClientRectangle}.");
            }
        }
        finally { settings.UiScalePercent = previousScale; }
    }

    [STATestMethod]
    public void MainLoadAvailabilityFollowsSelectedSlotAndOperationGuards()
    {
        string root = TempDirectory();
        try
        {
            string rom = Path.Combine(root, "idle.gb"); var bytes = new byte[32768]; bytes[0x100] = 0x18; bytes[0x101] = 0xFE; File.WriteAllBytes(rom, bytes);
            using var session = new EmulationSession(rom, Path.Combine(root, "idle.sav"), null, new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            Wait(() => session.LatestSnapshot.Rom is not null);
            using var main = new frmNano();
            Set(main, "session", session); Set(main, "currentRomPath", rom);
            var inventory = new AetherSaveSlotInventory { Inspect = (_, slot) => slot == 2 ? AetherSaveSlotState.Saved : AetherSaveSlotState.Empty };
            Set(main, "aetherSaveSlots", inventory);
            try
            {
                Wait(() => { Call(main, "UpdateAetherSessionUi", session.LatestSnapshot); return inventory[2] == AetherSaveSlotState.Saved; });
                Call(main, "SelectSaveSlot", 1); Assert.IsFalse(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                Call(main, "SelectSaveSlot", 2); Assert.IsTrue(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                Assert.IsTrue(Field<AetherButton[]>(main, "aetherSlotButtons")[1].Text.Contains('●'));
                Set(main, "stateOperationInProgress", true); Call(main, "UpdateAetherSlotButtons"); Assert.IsFalse(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                Set(main, "stateOperationInProgress", false); session.SetPausedAsync(true).GetAwaiter().GetResult();
                Call(main, "UpdateAetherSlotButtons"); Assert.IsTrue(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                session.ShutdownAsync().GetAwaiter().GetResult(); Call(main, "UpdateAetherSlotButtons");
                Assert.IsFalse(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                Set(main, "session", null); Call(main, "UpdateAetherSessionUi", (object?)null); Assert.IsFalse(Field<AetherButton>(main, "aetherLoadButton").Enabled);
                Assert.IsFalse(Field<AetherButton[]>(main, "aetherSlotButtons")[1].Text.Contains('●'));
            }
            finally { Set(main, "session", null); Set(main, "currentRomPath", null); Set(main, "stateOperationInProgress", false); }
        }
        finally { Directory.Delete(root, true); }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Private)!.SetValue(owner, value);
    private static void Call(object owner, string name, params object?[] args) => owner.GetType().GetMethod(name, Private)!.Invoke(owner, args);
    private static string TempDirectory() { string path = Path.Combine(Path.GetTempPath(), "aether-review-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void WaitPicker(AetherFileDialog picker) => Wait(() => { picker.PollNavigation(); return !picker.IsBusy; });
    private static void Wait(Func<bool> complete)
    {
        var clock = Stopwatch.StartNew(); while (!complete() && clock.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(1); }
        Assert.IsTrue(complete(), "The isolated UI operation did not complete.");
    }
    private static IEnumerable<Control> Descendants(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Descendants(child)) yield return nested; } }
}
