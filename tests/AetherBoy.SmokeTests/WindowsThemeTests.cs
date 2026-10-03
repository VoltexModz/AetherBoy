using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsThemeTests
{
    [STATestMethod]
    [DataRow("#8B38FF", "#29E2ED", "#050712", "logo")]
    [DataRow("#FF8000", "#0066CC", "#F7F7F7", "light")]
    [DataRow("#FFFFFF", "#29E2ED", "#000000", "black")]
    [DataRow("#F05BAF", "#6DCCFF", "#120B1A", "sakura")]
    [DataRow("#218FD5", "#37D7CE", "#061321", "ocean")]
    [DataRow("#36D392", "#A2E57B", "#061711", "emerald")]
    [DataRow("#FFAD42", "#FF6C65", "#1A1010", "amber")]
    [DataRow("#386D4A", "#B88029", "#F1E9D2", "pocket")]
    public void ExistingWindowsFollowThemeAndReturnToLogoColors(string primary, string secondary, string background, string capture)
    {
        using var settings = new ThemeScope();
        settings.Set(UiThemePalette.DefaultPrimary, UiThemePalette.DefaultSecondary, UiThemePalette.DefaultBackground);
        using var main = new frmNano();
        main.Show();
        Call(main, "OpenControlCenter");
        var center = Field<frmControlCenter>(main, "controlCenter");
        Application.DoEvents();
        Call(center, "ShowPage", "appearance");
        var content = center.Controls.Find("controlCenterContentHost", true).Single();
        Color defaultContent = content.BackColor;
        var page = center.Controls.Find("controlCenterPageAppearance", true).Single();
        var title = page.Controls.OfType<Label>().First(label => Equals(label.Tag, "value"));
        Color defaultTitle = title.ForeColor;

        settings.Set(primary, secondary, background);
        var palette = new UiThemePalette(primary, secondary, background);
        AetherColors.Apply(palette);
        Call(center, "RefreshAll");
        Application.DoEvents();
        Assert.AreEqual(ToColor(palette.Background), content.BackColor);
        Assert.AreEqual(ToColor(palette.Text), title.ForeColor);
        Assert.AreEqual(FormBorderStyle.None, center.FormBorderStyle);
        Assert.IsTrue(page.Visible);
        Assert.AreEqual(3, Descendants(page).OfType<AetherButton>().Count(button => button.Text.StartsWith("Wählen · ", StringComparison.Ordinal)));
        Panel[] swatches = Descendants(page).OfType<Panel>()
            .Where(panel => Equals(panel.Tag, "theme-swatch")).ToArray();
        Assert.AreEqual(UiThemePresets.All.Count * 2, swatches.Length);
        for (int index = 0; index < UiThemePresets.All.Count; index++)
        {
            Assert.AreEqual(ColorTranslator.FromHtml(UiThemePresets.All[index].Primary), swatches[index * 2].BackColor);
            Assert.AreEqual(ColorTranslator.FromHtml(UiThemePresets.All[index].Secondary), swatches[index * 2 + 1].BackColor);
        }
        Capture(main, "windows-theme-" + capture + ".png");
        Capture(center, "windows-appearance-" + capture + ".png");
        foreach (AetherButton slot in Field<AetherButton[]>(main, "aetherSlotButtons"))
            Assert.IsTrue(slot.Parent!.ClientRectangle.Contains(slot.Bounds), "The save-slot button must not be clipped by its row.");

        var restore = (AetherButton)center.Controls.Find("controlCenterRestoreTheme", true).Single();
        restore.PerformClick();
        Application.DoEvents();
        Assert.AreEqual(defaultContent, content.BackColor);
        Assert.AreEqual(defaultTitle, title.ForeColor);
        Assert.AreEqual(UiThemePalette.DefaultBackground, settings.Settings.UiBackgroundColor);
        center.Close(); main.Close();
    }

    [STATestMethod]
    public void ThemeColorsPersistGloballyWithoutChangingGamePalette()
    {
        using var scope = new ThemeScope();
        int gamePalette = scope.Settings.PaletteIndex;
        scope.Set("#112233", "#ABCDEF", "#F5F5F5");
        scope.Settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        nanoboy.Properties.Settings.Default.Reload();
        Assert.AreEqual("#112233", scope.Settings.UiPrimaryColor);
        Assert.AreEqual("#ABCDEF", scope.Settings.UiSecondaryColor);
        Assert.AreEqual("#F5F5F5", scope.Settings.UiBackgroundColor);
        Assert.AreEqual(gamePalette, scope.Settings.PaletteIndex);
    }

    [STATestMethod]
    public void PresetButtonsKeepCustomControlsAndLiveThemeSwitching()
    {
        using var scope = new ThemeScope();
        using var main = new frmNano(); main.Show();
        Call(main, "OpenControlCenter");
        var center = Field<frmControlCenter>(main, "controlCenter");
        Application.DoEvents();
        Call(center, "ShowPage", "appearance"); Application.DoEvents();
        foreach (var preset in UiThemePresets.All)
        {
            var button = (AetherButton)center.Controls.Find("themePreset_" + preset.Id, true).Single();
            Assert.IsTrue(button.Visible);
            button.PerformClick(); Application.DoEvents();
            Assert.AreEqual(preset.Primary, scope.Settings.UiPrimaryColor);
            Assert.AreEqual(preset.Secondary, scope.Settings.UiSecondaryColor);
            Assert.AreEqual(preset.Background, scope.Settings.UiBackgroundColor);
            Assert.IsTrue(button.Selected);
            Assert.AreEqual(1, Descendants(center).OfType<AetherButton>().Count(b => b.Name.StartsWith("themePreset_") && b.Selected));
        }
        center.Close(); main.Close();
    }

    [STATestMethod]
    public void ButtonsHaveTwoCutCornersGradientAndReadableDisabledSurface()
    {
        using var scope = new ThemeScope();
        AetherColors.Apply(new(null, null, null));
        using var parent = new Panel { BackColor = AetherColors.Void };
        using var button = new AetherButton { Text = "", Size = new Size(160, 42) };
        parent.Controls.Add(button);
        using var bitmap = new Bitmap(button.Width, button.Height);
        button.DrawToBitmap(bitmap, button.ClientRectangle);
        Assert.AreEqual(AetherColors.Void.ToArgb(), bitmap.GetPixel(157, 2).ToArgb(), "Top-right must be cut.");
        Assert.AreEqual(AetherColors.Void.ToArgb(), bitmap.GetPixel(2, 39).ToArgb(), "Bottom-left must be cut.");
        Assert.AreNotEqual(AetherColors.Void.ToArgb(), bitmap.GetPixel(2, 2).ToArgb(), "Top-left stays square.");
        Assert.AreNotEqual(AetherColors.Void.ToArgb(), bitmap.GetPixel(157, 39).ToArgb(), "Bottom-right stays square.");
        Assert.AreNotEqual(bitmap.GetPixel(20, 20), bitmap.GetPixel(140, 20), "Primary action must have a ramp.");
        button.Enabled = false;
        button.DrawToBitmap(bitmap, button.ClientRectangle);
        Assert.AreEqual(AetherColors.SurfaceRaised.ToArgb(), bitmap.GetPixel(80, 20).ToArgb());
        button.Size = new Size(1, 1);
        button.DrawToBitmap(bitmap, new Rectangle(0, 0, 1, 1));
    }

    private sealed class ThemeScope : IDisposable
    {
        public NanoboySettings Settings { get; } = new();
        private readonly string primary, secondary, background;
        public ThemeScope()
        {
            primary = Settings.UiPrimaryColor; secondary = Settings.UiSecondaryColor; background = Settings.UiBackgroundColor;
        }
        public void Set(string p, string s, string b)
        {
            Settings.UiPrimaryColor = p; Settings.UiSecondaryColor = s; Settings.UiBackgroundColor = b;
        }
        public void Dispose()
        {
            Set(primary, secondary, background);
            AetherColors.Apply(new UiThemePalette(primary, secondary, background));
            Settings.Dispose();
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static Color ToColor(UiRgb value) => Color.FromArgb(value.R, value.G, value.B);
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        form.Refresh(); Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, name));
    }
}
