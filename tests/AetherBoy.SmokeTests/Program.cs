using System.Reflection;
using System.Windows.Forms;
using nanoboy;

internal static class Program
{
    private static readonly string[] RequiredAudioInspectorFields =
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

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        using var form = new frmAudioTool();
        Type formType = form.GetType();

        foreach (string fieldName in RequiredAudioInspectorFields)
        {
            FieldInfo? field = formType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(form) == null)
            {
                Console.Error.WriteLine($"Audio Inspector field is not initialized: {fieldName}");
                return 1;
            }
        }

        var refreshCheckBox = GetRequiredField<CheckBox>(formType, form, "checkBox1");
        var refreshTimer = GetRequiredField<System.Windows.Forms.Timer>(formType, form, "timer1");

        refreshCheckBox.Checked = false;
        if (refreshTimer.Enabled)
        {
            Console.Error.WriteLine("Audio Inspector refresh timer ignored the unchecked state.");
            return 1;
        }

        refreshCheckBox.Checked = true;
        if (!refreshTimer.Enabled)
        {
            Console.Error.WriteLine("Audio Inspector refresh timer ignored the checked state.");
            return 1;
        }

        Console.WriteLine("Audio Inspector smoke test passed.");
        return 0;
    }

    private static T GetRequiredField<T>(Type type, object instance, string name) where T : class
    {
        return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T
            ?? throw new InvalidOperationException($"Required field is missing: {name}");
    }
}
