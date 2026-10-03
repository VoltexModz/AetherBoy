using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace nanoboy.Controls;

// Data only: commands cannot create a native menu or popup. Keyboard shortcuts
// remain owned by frmNano's existing input routing, not a hidden MenuStrip.
internal class AetherCommandItem : IDisposable
{
    internal AetherCommand? Parent;
    private bool enabled = true, available = true;
    internal string Name { get; set; } = "";
    internal string Text { get; set; } = "";
    internal Size Size { get; set; }
    internal string ToolTipText { get; set; } = "";
    internal object? Tag { get; set; }
    internal bool Enabled { get => enabled && (Parent?.Enabled ?? true); set => enabled = value; }
    internal bool Available { get => available && (Parent?.Available ?? true); set => available = value; }
    internal bool Visible { get => Available; set => Available = value; }
    public virtual void Dispose() { }
    internal virtual void PerformClick() { }
}
internal sealed class AetherCommand : AetherCommandItem
{
    internal bool Checked { get; set; }
    internal bool HasDropDownItems => DropDownItems.Count != 0;
    internal AetherCommandCollection DropDownItems { get; }
    internal event EventHandler? Click;
    internal AetherCommand(string text = "") { Text = text; DropDownItems = new AetherCommandCollection(this); }
    internal AetherCommand(string text, Image? image, EventHandler click) : this(text) { Click += click; }
    internal override void PerformClick() { if (Enabled && Available) Click?.Invoke(this, EventArgs.Empty); }
    public override void Dispose() { foreach (var item in DropDownItems) item.Dispose(); Click = null; DropDownItems.Clear(); }
}
internal sealed class AetherCommandSeparator : AetherCommandItem { }
internal sealed class AetherCommandCollection(AetherCommand? parent = null) : Collection<AetherCommandItem>
{
    internal AetherCommandItem? this[string name] => this.FirstOrDefault(item => item.Name == name);
    internal bool ContainsKey(string name) => this[name] is not null;
    internal AetherCommandItem[] Find(string name, bool recursive) => this.Where(item => item.Name == name)
        .Concat(recursive ? this.OfType<AetherCommand>().SelectMany(command => command.DropDownItems.Find(name, true)) : []).ToArray();
    internal void AddRange(AetherCommandItem[] items) { foreach (var item in items) Add(item); }
    protected override void InsertItem(int index, AetherCommandItem item) { base.InsertItem(index, item); item.Parent = parent; }
    protected override void RemoveItem(int index) { this[index].Parent = null; base.RemoveItem(index); }
    protected override void ClearItems() { foreach (var item in this) item.Parent = null; base.ClearItems(); }
}
internal sealed class AetherCommandSet
{
    internal AetherCommandCollection Items { get; } = new();
    internal bool Visible => false; // Never a visual control.
}
