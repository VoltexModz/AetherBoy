using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

internal sealed class frmSofaQuickMenu : Form
{
    private bool busy;
    private readonly Action exitMode;
    internal Action? NextAction { get; private set; }
    internal bool ResumeRequested { get; private set; }
    internal frmSofaQuickMenu(NanoboySettings settings, Action<int> selectSlot, Func<Task> save, Func<Task> load,
        Func<Task> screenshot, Action library, Action exit, Func<string> feedback)
    {
        exitMode = exit;
        ClientSize = new Size(620, 402); StartPosition = FormStartPosition.CenterParent;
        var status = new Label { Bounds = new Rectangle(22, 332, 576, 60), ForeColor = AetherColors.Muted };
        var resume = Add(global::AetherBoy.Runtime.Localization.UiText.Get("Weiter spielen"), 22, 16, () => { ResumeRequested = true; Close(); }); AcceptButton = resume;
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("Spielebibliothek"), 322, 16, () => Defer(library));
        var slots = new AetherSelect { Bounds = new Rectangle(22, 86, 276, 32) };
        for (int slot = 1; slot <= 5; slot++) slots.Items.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Speicherplatz ") + slot);
        slots.SelectedIndex = settings.SaveSlot - 1; slots.SelectedIndexChanged += (_, _) => selectSlot(slots.SelectedIndex + 1);
        Controls.Add(slots);
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("Sofa-Modus verlassen"), 322, 80, () => Defer(exit));
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("Schnellspeichern"), 22, 148, () => _ = Run(save));
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("Schnellladen"), 322, 148, () => _ = Run(load));
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("Screenshot speichern"), 22, 216, () => _ = Run(screenshot));
        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel pausiert im Menü.\nA: wählen · B: zurück · Strg+Umschalt+F11: Modus verlassen"),
            Bounds = new Rectangle(22, 278, 576, 48), ForeColor = AetherColors.Muted });
        Controls.Add(status);
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Spielmenü"), global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Aktion oder spiele weiter."), gamepadNavigationEnabled: () => !busy);
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };

        AetherButton Add(string text, int x, int y, Action action)
        {
            var button = new AetherButton { Text = text, Kind = AetherButtonKind.Secondary, Bounds = new Rectangle(x, y, 276, 48) };
            button.Click += (_, _) => { if (!busy) action(); }; Controls.Add(button); return button;
        }
        void Defer(Action action) { NextAction = action; Close(); }
        async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            try { await action(); if (!IsDisposed) status.Text = feedback(); }
            finally { busy = false; }
        }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // The visible exit button is also reachable without a controller.
        if (busy) return true;
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.F11)) { NextAction = exitMode; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
