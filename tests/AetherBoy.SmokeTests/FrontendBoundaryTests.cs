using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class FrontendBoundaryTests
{
    private static readonly Type[] FormsBehindSessionBoundary =
    {
        typeof(frmNano),
        typeof(frmAudioTool),
        typeof(frmCheats)
    };

    [TestMethod]
    public void EmulatorForms_DoNotStoreMutableCoreObjects()
    {
        Assembly coreAssembly = typeof(Nanoboy).Assembly;

        foreach (Type formType in FormsBehindSessionBoundary)
        {
            FieldInfo[] leakingFields = formType
                .GetFields(BindingFlags.Instance | BindingFlags.Public |
                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType.Assembly == coreAssembly)
                .ToArray();

            Assert.AreEqual(
                0,
                leakingFields.Length,
                $"{formType.Name} stores Core objects directly: " +
                string.Join(", ", leakingFields.Select(field => field.Name)));
        }
    }

    [TestMethod]
    public void EmulatorForms_UseSessionAtTheirPublicBoundary()
    {
        PropertyInfo? audioSession = typeof(frmAudioTool).GetProperty(nameof(frmAudioTool.Session));
        Assert.IsNotNull(audioSession);
        Assert.AreEqual(typeof(EmulationSession), audioSession.PropertyType);

        ConstructorInfo? cheatConstructor = typeof(frmCheats).GetConstructor(
            new[] { typeof(EmulationSession) });
        Assert.IsNotNull(cheatConstructor);
    }

    [STATestMethod]
    public void MainWindow_ExposesVerifiedSaveStateAndRewindControls()
    {
        using var form = new frmNano();
        Type formType = form.GetType();
        string[] menuNames =
        {
            "menuSaveState",
            "menuSaveStateQuickSave",
            "menuSaveStateQuickLoad",
            "menuSaveSlot1",
            "menuSaveSlot2",
            "menuSaveSlot3",
            "menuSaveSlot4",
            "menuSaveSlot5",
            "menuRewind"
        };

        foreach (string menuName in menuNames)
        {
            ToolStripMenuItem menu = formType
                .GetField(menuName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(form) as ToolStripMenuItem
                ?? throw new AssertFailedException($"Missing menu item: {menuName}");
            Assert.IsTrue(menu.Enabled, $"Verified feature is unexpectedly disabled: {menuName}");
            Assert.IsFalse(
                menu.Text?.Contains("deaktiviert", StringComparison.OrdinalIgnoreCase) == true,
                $"Verified feature still advertises itself as disabled: {menuName}");
        }

        Assert.IsNotNull(formType.GetMethod(
            "menuRewind_Click",
            BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [STATestMethod]
    public void MainWindow_BuildsAetherWaveShellWithDirectSessionControls()
    {
        using var form = new frmNano();
        Type formType = form.GetType();

        Assert.AreEqual(FormBorderStyle.None, form.FormBorderStyle);
        Assert.IsTrue(form.AllowDrop);
        Assert.IsTrue(form.MinimumSize.Width >= 780);
        Assert.IsTrue(form.MinimumSize.Height >= 600);

        Panel root = GetRequiredField<Panel>(formType, form, "aetherRoot");
        Panel emptyState = GetRequiredField<Panel>(formType, form, "aetherEmptyState");
        Control stage = GetRequiredField<Control>(formType, form, "aetherStage");
        Button open = GetRequiredField<Button>(formType, form, "aetherOpenButton");
        Button pause = GetRequiredField<Button>(formType, form, "aetherPauseButton");
        Button rewind = GetRequiredField<Button>(formType, form, "aetherRewindButton");
        Button save = GetRequiredField<Button>(formType, form, "aetherSaveButton");
        Button load = GetRequiredField<Button>(formType, form, "aetherLoadButton");
        Button turbo = GetRequiredField<Button>(formType, form, "aetherTurboButton");
        var legacyMenu = GetRequiredField<MenuStrip>(formType, form, "menuStrip");
        var gameView = GetRequiredField<Control>(formType, form, "gameView");
        var slots = GetRequiredField<Array>(formType, form, "aetherSlotButtons");

        Assert.IsTrue(form.Controls.Contains(root));
        Assert.AreSame(stage, emptyState.Parent);
        Assert.AreEqual(0, stage.Controls.GetChildIndex(emptyState));
        Assert.AreSame(stage, gameView.Parent);
        Assert.IsFalse(legacyMenu.Visible);
        Assert.IsTrue(open.Enabled);
        Assert.IsFalse(pause.Enabled);
        Assert.IsFalse(rewind.Enabled);
        Assert.IsFalse(save.Enabled);
        Assert.IsFalse(load.Enabled);
        Assert.IsFalse(turbo.Enabled);
        Assert.AreEqual(5, slots.Length);
    }

    [STATestMethod]
    public void MainWindow_DragDropAcceptsExactlyOneGameBoyRom()
    {
        MethodInfo method = typeof(frmNano).GetMethod(
            "TryGetDroppedRom",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Missing ROM drag-and-drop validator.");

        var validData = new DataObject();
        validData.SetData(DataFormats.FileDrop, new[] { @"C:\roms\demo.GBC" });
        object?[] validArguments = { validData, null };
        Assert.AreEqual(true, method.Invoke(null, validArguments));
        Assert.AreEqual(@"C:\roms\demo.GBC", validArguments[1]);

        var invalidData = new DataObject();
        invalidData.SetData(DataFormats.FileDrop, new[] { @"C:\roms\notes.txt" });
        object?[] invalidArguments = { invalidData, null };
        Assert.AreEqual(false, method.Invoke(null, invalidArguments));

        var multipleData = new DataObject();
        multipleData.SetData(DataFormats.FileDrop, new[] { @"C:\roms\one.gb", @"C:\roms\two.gb" });
        object?[] multipleArguments = { multipleData, null };
        Assert.AreEqual(false, method.Invoke(null, multipleArguments));

        Type programType = typeof(frmNano).Assembly.GetType("nanoboy.Program")
            ?? throw new AssertFailedException("Missing application entry point.");
        MethodInfo startupMethod = programType.GetMethod(
            "TryGetStartupRom",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Missing startup ROM validator.");
        object?[] startupArguments = { new[] { @"C:\roms\demo.gb" }, null };
        Assert.AreEqual(true, startupMethod.Invoke(null, startupArguments));
        Assert.AreEqual(Path.GetFullPath(@"C:\roms\demo.gb"), startupArguments[1]);

        object?[] invalidStartupArguments = { new[] { @"C:\roms\demo.zip" }, null };
        Assert.AreEqual(false, startupMethod.Invoke(null, invalidStartupArguments));
    }

    [STATestMethod]
    public void SecondaryWindows_ShareTheAetherDialogShell()
    {
        using var cable = new LinkCable();
        Form[] forms =
        {
            new frmAbout(),
            new frmChangelog(),
            new frmControls(new NanoboySettings()),
            new frmAudioTool(),
            new frmLink(cable)
        };

        try
        {
            foreach (Form form in forms)
            {
                Assert.AreEqual(
                    FormBorderStyle.None,
                    form.FormBorderStyle,
                    $"{form.GetType().Name} still uses operating-system chrome.");
                Assert.AreEqual(1, form.Controls.Find("aetherDialogHeader", true).Length);
                Assert.AreEqual(1, form.Controls.Find("aetherDialogBody", true).Length);
                Assert.AreEqual(1, form.Controls.Find("aetherDialogMark", true).Length);
                Assert.AreEqual(1, form.Controls.Find("aetherDialogCloseButton", true).Length);
            }
        }
        finally
        {
            foreach (Form form in forms)
            {
                form.Dispose();
            }
        }
    }

    [STATestMethod]
    public void DisabledLinkLab_DoesNotExposeNetworkActions()
    {
        using var cable = new LinkCable();
        using var form = new frmLink(cable);
        Type formType = form.GetType();

        Button host = GetRequiredField<Button>(formType, form, "btnHost");
        Button connect = GetRequiredField<Button>(formType, form, "btnConnect");
        Button disconnect = GetRequiredField<Button>(formType, form, "btnDisconnect");
        TextBox address = GetRequiredField<TextBox>(formType, form, "txtIP");

        Assert.IsFalse(host.Enabled);
        Assert.IsFalse(connect.Enabled);
        Assert.IsFalse(disconnect.Enabled);
        Assert.IsFalse(address.Enabled);
    }

    private static T GetRequiredField<T>(Type type, object instance, string name) where T : class
    {
        return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T
            ?? throw new AssertFailedException($"Required field is missing: {name}");
    }
}
