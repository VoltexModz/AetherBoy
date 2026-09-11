using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

internal sealed class frmQuickMenu : Form
{
    private bool busy;
    private readonly Label status;
    private readonly Func<string> feedback;
    internal bool PauseOnExit { get; private set; }
    internal Action? NextAction { get; private set; }
    internal frmQuickMenu(NanoboySettings settings, bool wasPaused, Action<int> selectSlot,
        Func<Task> save, Func<Task> load, Func<Task> screenshot, Action toggleOverlay, Action toggleFullscreen,
        Action openLibrary, Action openSettings, Action openGallery, Func<string> feedback, Action? markProblem = null)
    {
        this.feedback = feedback; PauseOnExit = wasPaused;
        Text = "Quick Deck"; ClientSize = new Size(760, 510); StartPosition = FormStartPosition.CenterParent;
        Add("WEITER ZUM SPIEL", "quickMenuContinue", 20, 18, 350, Close).Kind = AetherButtonKind.Primary;
        AetherButton? pause = null;
        pause = Add(PauseOnExit ? "BEIM SCHLIESSEN PAUSIERT" : "BEIM SCHLIESSEN WEITERSPIELEN", "quickMenuPause", 390, 18, 350,
            () => { PauseOnExit = !PauseOnExit; pause!.Text = PauseOnExit ? "BEIM SCHLIESSEN PAUSIERT" : "BEIM SCHLIESSEN WEITERSPIELEN"; });
        Controls.Add(new Label { Text = "MANUELLER SLOT", Bounds = new(20, 82, 150, 30) });
        var slots = new ComboBox { Name = "quickMenuSlot", DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new(176, 78, 96, 30) };
        slots.Items.AddRange(new object[] { "1", "2", "3", "4", "5" }); slots.SelectedIndex = settings.SaveSlot - 1;
        slots.SelectedIndexChanged += (_, _) => selectSlot(slots.SelectedIndex + 1); Controls.Add(slots);
        Add("SPEICHERN", "quickMenuSave", 292, 72, 216, () => Run(save));
        Add("LADEN", "quickMenuLoad", 524, 72, 216, () => Run(load));
        Add("STATE-GALERIE", "quickMenuGallery", 20, 142, 350, () => Defer(openGallery));
        Add("EINSTELLUNGEN", "quickMenuSettings", 390, 142, 350, () => Defer(openSettings));
        Add("SPIELBIBLIOTHEK", "quickMenuLibrary", 20, 204, 350, () => Defer(openLibrary));
        Add("VOLLBILD UMSCHALTEN", "quickMenuFullscreen", 390, 204, 350, toggleFullscreen);
        Add("SCREENSHOT · F12", "quickMenuScreenshot", 20, 266, 350, () => Run(screenshot));
        AetherButton? overlay = null;
        overlay = Add("PERFORMANCE · " + (settings.PerformanceOverlay ? "AN" : "AUS"), "quickMenuOverlay", 390, 266, 350,
            () => { toggleOverlay(); overlay!.Text = "PERFORMANCE · " + (settings.PerformanceOverlay ? "AN" : "AUS"); });
        if (markProblem != null)
            Add("PROBLEM MARKIEREN", "quickMenuMarkProblem", 20, 328, 350,
                () => { markProblem(); status.Text = feedback(); });
        status = new Label { Name = "quickMenuStatus", Text = feedback(), AutoEllipsis = true, Bounds = new(390, 332, 350, 62) };
        Controls.Add(status);
        Controls.Add(new Label { Text = "Das Spiel pausiert, solange das Quick Deck offen ist.\r\nD-Pad / Stick: bewegen · A/South: wählen · B/East: zurück · LB/RB: Fokus\r\nF10 oder beide Stick-Tasten (L3+R3): Quick Deck öffnen", Bounds = new(20, 410, 720, 76) });
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        AetherDialog.Apply(this, "QUICK DECK // GAMEPAD", "Pause, Speicherstände und Anzeige ohne Tastatur erreichbar");
    }
    private void Defer(Action action) { NextAction = action; Close(); }
    private async void Run(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        foreach (Control control in Input.GamepadNavigation.Targets(this)) control.Enabled = false;
        try { await action(); }
        finally
        {
            busy = false;
            if (!IsDisposed)
            {
                EnableChildren(Controls); status.Text = feedback();
            }
        }
    }
    private static void EnableChildren(Control.ControlCollection controls)
    { foreach (Control control in controls) { control.Enabled = true; EnableChildren(control.Controls); } }
    private AetherButton Add(string label, string name, int x, int y, int width, Action action)
    { var button = new AetherButton { Name = name, Text = label, Kind = AetherButtonKind.Secondary, Bounds = new(x, y, width, 44) }; button.Click += (_, _) => action(); Controls.Add(button); return button; }
}
