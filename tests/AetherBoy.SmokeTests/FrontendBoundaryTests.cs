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
}
