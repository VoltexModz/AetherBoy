using System;
using System.Globalization;

namespace nanoboy.Core;

/// <summary>Three user colors and the readable UI colors derived from them.</summary>
public readonly record struct UiRgb(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public static bool TryParse(string? value, out UiRgb color)
    {
        color = default;
        if (value is null) return false;
        ReadOnlySpan<char> hex = value.AsSpan().Trim();
        if (hex.Length == 7 && hex[0] == '#') hex = hex[1..];
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    public static UiRgb Mix(UiRgb from, UiRgb to, double amount) => new(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    public double Luminance
    {
        get
        {
            static double Channel(byte value)
            {
                double unit = value / 255d;
                return unit <= .04045 ? unit / 12.92 : Math.Pow((unit + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(R) + .7152 * Channel(G) + .0722 * Channel(B);
        }
    }

    public double Contrast(UiRgb other)
    {
        double first = Luminance, second = other.Luminance;
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
}

public sealed class UiThemePalette
{
    public const string DefaultPrimary = "#8B38FF";
    public const string DefaultSecondary = "#29E2ED";
    public const string DefaultBackground = "#050712";

    public UiRgb Primary { get; }
    public UiRgb Secondary { get; }
    public UiRgb Background { get; }
    public UiRgb Chrome { get; }
    public UiRgb Surface { get; }
    public UiRgb Raised { get; }
    public UiRgb Border { get; }
    public UiRgb Text { get; }
    public UiRgb Muted { get; }
    public UiRgb PrimaryText { get; }
    public UiRgb SecondaryText { get; }
    public UiRgb OnPrimary { get; }
    public UiRgb OnSecondary { get; }
    public UiRgb SuccessText { get; }
    public UiRgb DangerText { get; }
    public UiButtonGradient Button { get; }
    public UiButtonGradient ButtonHover { get; }
    public UiButtonGradient ButtonPressed { get; }

    public UiThemePalette(string? primary, string? secondary, string? background)
    {
        UiRgb.TryParse(DefaultPrimary, out UiRgb defaultPrimary);
        UiRgb.TryParse(DefaultSecondary, out UiRgb defaultSecondary);
        UiRgb.TryParse(DefaultBackground, out UiRgb defaultBackground);
        Primary = UiRgb.TryParse(primary, out UiRgb p) ? p : defaultPrimary;
        Secondary = UiRgb.TryParse(secondary, out UiRgb s) ? s : defaultSecondary;
        Background = UiRgb.TryParse(background, out UiRgb b) ? b : defaultBackground;
        bool original = Primary == defaultPrimary && Secondary == defaultSecondary && Background == defaultBackground;
        Text = original ? new(241, 244, 255) : Background.Contrast(new(255, 255, 255)) >= Background.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        bool dark = Text.Luminance > .5;
        UiRgb opposite = dark ? new(0, 0, 0) : new(255, 255, 255);
        UiRgb SurfaceAt(double amount)
        {
            // Keep dark themes close to their background instead of washing out
            // the shell. Custom colors remain untouched in settings and swatches.
            UiRgb tint = UiRgb.Mix(Primary, Secondary, .22);
            UiRgb candidate = UiRgb.Mix(UiRgb.Mix(Background, Text, amount), tint, dark ? .035 : .02);
            return Text.Contrast(candidate) >= 4.5 ? candidate : UiRgb.Mix(Background, opposite, amount);
        }
        // These are the actual Aether Wave surface colors, not just the three
        // logo colors. Other presets and custom palettes use the same hierarchy.
        Chrome = original ? new(8, 11, 24) : SurfaceAt(.012);
        Surface = original ? new(12, 16, 31) : SurfaceAt(.03);
        Raised = original ? new(17, 22, 41) : SurfaceAt(.055);
        Border = original ? new(47, 55, 83) : SurfaceAt(.20);
        UiRgb ReadableOnSurfaces(UiRgb color)
        {
            UiRgb[] surfaces = [Background, Chrome, Surface, Raised];
            foreach (UiRgb surface in surfaces) color = EnsureContrast(color, surface, Text);
            foreach (UiRgb surface in surfaces)
                if (color.Contrast(surface) < 4.5) return Text;
            return color;
        }
        Muted = ReadableOnSurfaces(original ? new(139, 148, 177) : UiRgb.Mix(Background, Text, .62));
        PrimaryText = ReadableOnSurfaces(Primary);
        SecondaryText = ReadableOnSurfaces(Secondary);
        OnPrimary = Primary.Contrast(new(255, 255, 255)) >= Primary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        OnSecondary = Secondary.Contrast(new(255, 255, 255)) >= Secondary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        SuccessText = ReadableOnSurfaces(dark ? new(84, 237, 176) : new(0, 112, 69));
        DangerText = ReadableOnSurfaces(dark ? new(255, 92, 132) : new(176, 27, 45));
        Button = UiButtonGradient.Create(Primary, Secondary);
        ButtonHover = UiButtonGradient.Create(UiRgb.Mix(Primary, new(255, 255, 255), .13), UiRgb.Mix(Secondary, new(255, 255, 255), .13));
        ButtonPressed = UiButtonGradient.Create(UiRgb.Mix(Primary, new(0, 0, 0), .16), UiRgb.Mix(Secondary, new(0, 0, 0), .16));
    }

    private static UiRgb EnsureContrast(UiRgb color, UiRgb background, UiRgb toward)
    {
        for (int step = 0; step < 32 && color.Contrast(background) < 4.5; step++)
            color = UiRgb.Mix(color, toward, .25);
        return color.Contrast(background) >= 4.5 ? color : toward;
    }
}
