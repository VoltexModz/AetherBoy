using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

internal sealed class frmStateGallery : Form
{
    private readonly string rom;
    private readonly Func<int, bool, Task> save, load;
    private readonly Func<bool> canUndo, busy;
    private readonly Dictionary<int, (PictureBox Picture, Label Details, AetherButton Save, AetherButton Load)> cards = new();
    private readonly AetherButton undoButton;
    private int refreshGeneration;

    internal frmStateGallery(string rom, Func<int, bool, Task> save, Func<int, bool, Task> load,
        Func<bool> canUndo, Func<bool> busy)
    {
        this.rom = rom; this.save = save; this.load = load; this.canUndo = canUndo; this.busy = busy;
        Text = "State-Galerie";
        ClientSize = new Size(1010, 710);
        StartPosition = FormStartPosition.CenterParent;
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 66 };
        undoButton = new AetherButton { Name = "stateGalleryUndoButton", Text = "LETZTES LADEN RÜCKGÄNGIG", Bounds = new(18, 12, 330, 40) };
        undoButton.Click += async (_, _) => await RunAction(() => load(0, true));
        footer.Controls.Add(undoButton);
        footer.Controls.Add(new Label { Text = "Fortsetzen: eigener Slot · Auto alle 60 s und beim Beenden\r\nRückgängig gilt einmalig in dieser Sitzung; Batterie-Saves bleiben getrennt.",
            ForeColor = AetherColors.Muted, Bounds = new(370, 10, 610, 48) });
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new(12), BackColor = AetherColors.Void };
        foreach (int slot in new[] { 1, 2, 3, 4, 5, 0 })
        {
            var card = new AetherSurfacePanel { Size = new(310, 298), Margin = new(6), AccentEdge = true };
            card.Controls.Add(new Label { Text = slot == 0 ? "FORTSETZEN // AUTO" : $"SLOT {slot}", ForeColor = AetherColors.Cyan,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Bounds = new(14, 10, 280, 24) });
            var picture = new PictureBox { BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new(14, 42, 282, 146) };
            var details = new Label { Bounds = new(14, 196, 282, 48), AutoEllipsis = true, ForeColor = AetherColors.Text };
            var saveButton = new AetherButton { Text = "SPEICHERN", Name = $"stateGallerySave{slot}", Bounds = new(14, 252, 134, 34) };
            var loadButton = new AetherButton { Text = slot == 0 ? "FORTSETZEN" : "LADEN", Name = $"stateGalleryLoad{slot}", Bounds = new(162, 252, 134, 34), Kind = AetherButtonKind.Primary };
            saveButton.Click += async (_, _) => await RunAction(() => save(slot, false));
            loadButton.Click += async (_, _) => await RunAction(() => load(slot, false));
            card.Controls.AddRange(new Control[] { picture, details, saveButton, loadButton });
            cards.Add(slot, (picture, details, saveButton, loadButton));
            flow.Controls.Add(card);
        }
        Controls.Add(flow); Controls.Add(footer);
        AetherDialog.Apply(this, "STATE BANK // VORSCHAU", "Fünf manuelle Slots, Fortsetzen und ein Rückkehrpunkt nach Schnellladen");
        Shown += async (_, _) => await RefreshSlotsAsync();
        Disposed += (_, _) => { refreshGeneration++; foreach (var card in cards.Values) card.Picture.Image?.Dispose(); };
    }

    private async Task RunAction(Func<Task> action)
    {
        if (busy()) return;
        foreach (var card in cards.Values) card.Save.Enabled = card.Load.Enabled = false;
        undoButton.Enabled = false;
        await action();
        if (!IsDisposed) await RefreshSlotsAsync();
    }

    internal async Task RefreshSlotsAsync()
    {
        int generation = ++refreshGeneration;
        StateSlotInfo[] states = await Task.Run(() => cards.Keys.Select(slot => WindowsSaveStateStore.Default.Inspect(rom, slot)).ToArray());
        if (IsDisposed || generation != refreshGeneration) return;
        foreach (StateSlotInfo state in states)
        {
            var card = cards[state.Slot];
            Image? previous = card.Picture.Image;
            card.Picture.Image = WindowsSaveStateStore.DecodePreview(state.Preview?.Png);
            previous?.Dispose();
            card.Details.Text = !state.Exists ? "LEER\r\nNoch kein Zustand gespeichert" :
                $"{state.Preview?.Title ?? "State-Datei"} · {state.WrittenUtc?.ToLocalTime():g}\r\n" +
                (state.Warning ?? $"Frame {state.Preview?.Frame:N0} · Vorschau zugeordnet");
            card.Save.Enabled = !busy();
            card.Load.Enabled = !busy() && state.Exists;
        }
        undoButton.Enabled = !busy() && canUndo();
    }
}
