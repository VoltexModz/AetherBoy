using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace nanoboy.Controls;

internal sealed class AetherColorDialog : IDisposable
{
    internal Color Color { get; set; }
    private AetherWindow? window;
    internal AetherWindow? Window => window;
    private bool updating;
    internal DialogResult ShowDialog(IWin32Window owner)
    {
        Build(); return window!.ShowDialog(owner);
    }
    internal void Build()
    {
        window?.Dispose();
        window = new AetherWindow { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Farbe wählen"), ClientSize = new Size(600, 390) };
        var preview = new AetherColorSwatch { Bounds = new(24, 24, 130, 140), BackColor = Color, Tag = "theme-swatch" };
        var hex = new AetherTextBox { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Farbe als Hex-Wert"), Text = $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}", Bounds = new(24, 182, 180, 36), MaxLength = 7 };
        var status = new Label { Bounds = new(24, 262, 552, 50), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ändere Rot, Grün und Blau oder gib einen Hex-Wert ein.") };
        var tracks = new AetherScrollBar[3]; var fields = new AetherTextBox[3];
        Color selected = Color;
        void Update(Color color)
        {
            if (updating) return; updating = true;
            selected = color; preview.BackColor = color; preview.Invalidate();
            int[] values = [color.R, color.G, color.B];
            for (int i = 0; i < 3; i++) { tracks[i].Value = values[i]; fields[i].Text = values[i].ToString(CultureInfo.InvariantCulture); }
            hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}"; status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Vorschau zeigt deine Auswahl. Übernehmen speichert diese Farbe."); updating = false;
        }
        for (int i = 0; i < 3; i++)
        {
            int channel = i, y = 24 + i * 70;
            window.Controls.Add(new Label { Text = new[] { global::AetherBoy.Runtime.Localization.UiText.Get("Rot"), global::AetherBoy.Runtime.Localization.UiText.Get("Grün"), global::AetherBoy.Runtime.Localization.UiText.Get("Blau") }[i], Bounds = new(224, y, 90, 24) });
            tracks[i] = new AetherScrollBar { Direction = Orientation.Horizontal, Bounds = new(224, y + 26, 252, 24), AccessibleName = new[] { global::AetherBoy.Runtime.Localization.UiText.Get("Rot"), global::AetherBoy.Runtime.Localization.UiText.Get("Grün"), global::AetherBoy.Runtime.Localization.UiText.Get("Blau") }[i] };
            tracks[i].Configure(271, 16);
            fields[i] = new AetherTextBox { Bounds = new(490, y + 18, 86, 34), MaxLength = 3, AccessibleName = tracks[i].AccessibleName + global::AetherBoy.Runtime.Localization.UiText.Get(" von 0 bis 255") };
            tracks[i].ValueChanged += (_, _) => { if (!updating) Update(System.Drawing.Color.FromArgb(tracks[0].Value, tracks[1].Value, tracks[2].Value)); };
            fields[i].TextChanged += (_, _) =>
            {
                if (updating) return;
                if (!byte.TryParse(fields[channel].Text, out byte value)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Gib für Rot, Grün und Blau eine Zahl von 0 bis 255 ein."); return; }
                int[] values = [selected.R, selected.G, selected.B]; values[channel] = value; Update(System.Drawing.Color.FromArgb(values[0], values[1], values[2]));
            };
            window.Controls.AddRange([tracks[i], fields[i]]);
        }
        hex.TextChanged += (_, _) => { if (!updating) { if (TryParseHex(hex.Text, out Color color)) Update(color); else status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Gib sechs Hex-Zeichen ein, zum Beispiel #8B38FF."); } };
        var accept = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Übernehmen"), Bounds = new(24, 326, 264, 42) };
        accept.Click += (_, _) =>
        {
            if (!TryParseHex(hex.Text, out _) || Array.Exists(fields, field => !byte.TryParse(field.Text, out _))) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Prüfe den Hex-Wert und die drei Farbwerte, bevor du übernimmst."); return; }
            Color = selected; window.DialogResult = DialogResult.OK; window.Close();
        };
        var cancel = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), Bounds = new(312, 326, 264, 42), DialogResult = DialogResult.Cancel, Kind = AetherButtonKind.Secondary };
        window.Controls.AddRange([preview, hex, status, accept, cancel]); window.AcceptButton = accept; window.CancelButton = cancel;
        Update(Color); AetherDialog.Apply(window, global::AetherBoy.Runtime.Localization.UiText.Get("Eigene Farben"), global::AetherBoy.Runtime.Localization.UiText.Get("Dein Theme bleibt bis zum Übernehmen unverändert."));
    }
    internal static bool TryParseHex(string text, out Color color)
    {
        text = text.Trim().TrimStart('#'); color = default;
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = System.Drawing.Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); return true;
    }
    public void Dispose() => window?.Dispose();
}
