using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class AudioInspectorSmokeTests
{
    private static readonly string[] RequiredFields =
    {
        "levelDisplayControl1",
        "levelDisplayControl2",
        "levelDisplayControl3",
        "levelDisplayControl4",
        "waveDataControl1",
        "btnRecordWav",
        "checkBox1",
        "timer1"
    };

    [STATestMethod]
    public void AudioInspector_InitializesControlsAndTracksRefreshCheckbox()
    {
        using var form = new frmAudioTool();
        Type formType = form.GetType();

        foreach (string fieldName in RequiredFields)
        {
            FieldInfo? field = formType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field?.GetValue(form), $"Audio Inspector field is not initialized: {fieldName}");
        }

        var refreshCheckBox = GetRequiredField<CheckBox>(formType, form, "checkBox1");
        var refreshTimer = GetRequiredField<System.Windows.Forms.Timer>(formType, form, "timer1");

        refreshCheckBox.Checked = false;
        Assert.IsFalse(refreshTimer.Enabled, "Refresh timer ignored the unchecked state.");

        refreshCheckBox.Checked = true;
        Assert.IsTrue(refreshTimer.Enabled, "Refresh timer ignored the checked state.");
    }

    private static T GetRequiredField<T>(Type type, object instance, string name) where T : class
    {
        return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T
            ?? throw new AssertFailedException($"Required field is missing: {name}");
    }
}
