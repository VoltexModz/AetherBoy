using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using NativeButtonAlias = System.Windows.Forms.Button;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsUiPolicyTests
{
    [TestMethod]
    public void NativeUiDebtCannotGrowOrMoveToAnotherFactoryUnreviewed()
    {
        var actual = WindowsUiInventory.Collect(typeof(frmNano).Assembly);
        using var stream = typeof(WindowsUiPolicyTests).Assembly.GetManifestResourceStream("AetherBoy.WindowsUiDebt.json")!;
        var recorded = JsonSerializer.Deserialize<Dictionary<string, int>>(stream)!;
        string[] changes = actual.Keys.Union(recorded.Keys).Order(StringComparer.Ordinal)
            .Where(key => actual.GetValueOrDefault(key) != recorded.GetValueOrDefault(key))
            .Select(key => $"{key}: recorded {recorded.GetValueOrDefault(key)}, actual {actual.GetValueOrDefault(key)}").ToArray();
        if (changes.Length != 0)
        {
            Console.WriteLine("Native UI inventory (call sites, not visible control instances):");
            Console.WriteLine(JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.IsEmpty(changes,
            "Use an Aether component. Existing debt is recorded, not approved for new UI. " +
            "After removing a native element, shrink WindowsUiDebt.json; do not blindly regenerate it. " +
            "See AGENTS.md and docs/UI_THEME_HANDOFF_2026-09-28.md.\n" + string.Join("\n", changes));
    }

    [TestMethod]
    public void InventoryFindsAliasedAndTargetTypedControlsAndNativeDialogs()
    {
        CollectionAssert.AreEqual(new[] { "new Button" }, Inspect(nameof(AliasedButton)));
        CollectionAssert.AreEqual(new[] { "new TextBox" }, Inspect(nameof(TargetTypedInput)));
        CollectionAssert.AreEqual(new[] { "new ColorDialog" }, Inspect(nameof(NativeColorPicker)));
        CollectionAssert.AreEqual(new[] { "new Form" }, Inspect(nameof(NativeWindow)));
        CollectionAssert.AreEqual(new[] { "MessageBox.Show" }, Inspect(nameof(NativeMessage)));
    }

    [TestMethod]
    public void InventoryDoesNotConfuseAetherConstructorsWithTheirWinFormsBaseClass()
    {
        Assert.IsEmpty(Inspect(nameof(OwnButton)));
        Assert.IsEmpty(Inspect(nameof(LayoutPrimitive)));
        Assert.IsEmpty(Inspect(nameof(TextThatOnlyMentionsControls)));
        CollectionAssert.AreEqual(new[] { "ScrollableControl.set_AutoScroll" }, Inspect(nameof(ScrollingPanel)));
    }

    private static string[] Inspect(string name) => WindowsUiInventory.Inspect(typeof(WindowsUiPolicyTests)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!).ToArray();

    // These are NEVER executed: they are IL fixtures, including the message box.
    private static object AliasedButton() => new NativeButtonAlias();
    private static object TargetTypedInput() { TextBox input = new(); return input; }
    private static object NativeColorPicker() => new ColorDialog();
    private static object NativeWindow() => new Form();
    private static object NativeMessage() => MessageBox.Show("fixture");
    private static object OwnButton() => new AetherButton();
    private static object LayoutPrimitive() => new Panel();
    private static object ScrollingPanel() => new Panel { AutoScroll = true };
    private static string TextThatOnlyMentionsControls() => "new Button(); MessageBox.Show();";
}
