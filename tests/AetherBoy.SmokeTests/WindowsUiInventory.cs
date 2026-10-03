using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AetherBoy.SmokeTests;

/// <summary>
/// Inspect compiled call sites, not text: catches aliases, target-typed new(),
/// designer code, factories, lambdas and async state machines without opening UI.
/// This inventories native-appearance risks; it does not prove visual conformance.
/// </summary>
internal static class WindowsUiInventory
{
    private static readonly Dictionary<short, OpCode> Instructions = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(op => op.Value);

    // These primitives have no themed Windows interaction chrome of their own.
    // Native scrolling, borders and form chrome configured on them are audited below.
    private static readonly HashSet<Type> Primitives =
    [typeof(Control), typeof(Panel), typeof(FlowLayoutPanel), typeof(TableLayoutPanel), typeof(System.Windows.Forms.Label), typeof(PictureBox)];

    internal static SortedDictionary<string, int> Collect(Assembly assembly)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Type type in assembly.GetTypes())
        {
            // A new window must not bypass the shared Aether frame unnoticed.
            // Existing subclasses use shared styling or their own Aether rendering;
            // the declaration alone is not evidence of a visible native frame.
            if (type.BaseType == typeof(Form)) counts[type.FullName + " | native Form base"] = 1;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
                foreach (string risk in Inspect(method))
                {
                    string key = $"{StableName(type.FullName!)}::{StableName(method.Name)} | {risk}";
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
        }
        return counts;
    }

    // Ordinals change when an unrelated method is inserted. Keep source method /
    // local function names, but group lambdas within that method by native API.
    private static string StableName(string name) => Regex.Replace(name,
        @"(?<=__DisplayClass)\d+(?:_\d+)?|(?<=d__)\d+|(?<=b__)\d+(?:_\d+)?|(?<=\|)\d+(?:_\d+)?", "*");

    internal static IEnumerable<string> Inspect(MethodBase method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        for (int offset = 0; offset < il.Length;)
        {
            short value = il[offset++];
            if (value == 0xFE) value = (short)(0xFE00 | il[offset++]);
            OpCode op = Instructions[value];
            if (op.OperandType == OperandType.InlineMethod)
            {
                MethodBase? target = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset),
                    method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                string? risk = Classify(op, target);
                if (risk is not null) yield return risk;
            }
            offset += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod
                    or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
                    or OperandType.ShortInlineR => 4,
                _ => throw new InvalidOperationException($"Unsupported IL operand {op.OperandType} in {method}.")
            };
        }
    }

    private static string? Classify(OpCode op, MethodBase? target)
    {
        Type? type = target?.DeclaringType;
        if (type is null || type.Assembly != typeof(Control).Assembly) return null;
        if (op == OpCodes.Newobj &&
            (typeof(Control).IsAssignableFrom(type) && !Primitives.Contains(type)
            || typeof(CommonDialog).IsAssignableFrom(type) || typeof(ToolStripItem).IsAssignableFrom(type)
            || type == typeof(ToolTip) || type == typeof(NotifyIcon) || type == typeof(ErrorProvider)))
            return "new " + type.Name;

        if (target!.Name is "Show" or "ShowDialog" && (type == typeof(MessageBox) || type == typeof(TaskDialog)))
            return type.Name + "." + target.Name;
        if (target.Name == "ShowDropDown" && typeof(ToolStripDropDownItem).IsAssignableFrom(type)
            || target.Name == "Show" && typeof(ToolStripDropDown).IsAssignableFrom(type))
            return type.Name + "." + target.Name;

        if (target.Name is "set_AutoScroll" or "set_ScrollBars" or "set_ShowItemToolTips" or "set_AutoCompleteMode" or "set_FormBorderStyle"
            || target.Name == "set_BorderStyle" && type == typeof(Panel)
            || target.Name == "DrawFocusRectangle" && (type == typeof(ControlPaint) || type == typeof(DrawListViewItemEventArgs)))
            return type.Name + "." + target.Name;
        return null;
    }
}
