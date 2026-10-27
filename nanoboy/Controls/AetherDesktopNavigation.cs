using System;
using System.Drawing;
using System.Windows.Forms;
using nanoboy.Core;
using UiText = AetherBoy.Runtime.Localization.UiText;

namespace nanoboy.Controls;

internal static class AetherDesktopNavigation
{
    internal static Panel Create(string active, Action<string> navigate)
    {
        var panel = new Panel { Dock = DockStyle.Left, Width = 184, BackColor = AetherColors.Chrome,
            Padding = new Padding(12, 28, 12, 16), Name = "aetherDesktopNavigation" };
        (string Id, string Label, UiIcon Icon)[] items = [
            ("play", "Spielen", UiIcon.Play), ("library", "Bibliothek", UiIcon.Library),
            ("saves", "Spielstände", UiIcon.Save), ("link", "Link", UiIcon.Link),
            ("tools", "Werkzeuge", UiIcon.Tools), ("settings", "Einstellungen", UiIcon.Settings)];
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var button = new AetherButton { Text = UiText.Get(item.Label), AccessibleName = UiText.Get(item.Label),
                Icon = item.Icon, AlignLeft = true, Kind = AetherButtonKind.Ghost, Selected = item.Id == active,
                Bounds = new Rectangle(12, 28 + i * 52, 160, 46), Name = "desktopNav" + item.Id };
            if (i == items.Length - 1) button.Dock = DockStyle.Bottom;
            button.Click += (_, _) => navigate(item.Id);
            panel.Controls.Add(button);
        }
        panel.ParentChanged += (_, _) =>
        {
            if (panel.Parent is not { } parent) return;
            void Arrange()
            {
                float scale = panel.DeviceDpi / 96f * Math.Clamp(Properties.Settings.Default.UiScalePercent, 100, 150) / 100f;
                bool compact = parent.ClientSize.Width / scale < 1200;
                panel.Width = (int)((compact ? 72 : 184) * scale);
                foreach (Control control in panel.Controls)
                    if (control is AetherButton button) { button.IconOnly = compact; button.Width = panel.Width - panel.Padding.Horizontal; button.Invalidate(); }
            }
            parent.SizeChanged += (_, _) => Arrange(); Arrange();
        };
        return panel;
    }
}
