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

    public UiThemePalette(string? primary, string? secondary, string? background)
    {
        UiRgb.TryParse(DefaultPrimary, out UiRgb defaultPrimary);
        UiRgb.TryParse(DefaultSecondary, out UiRgb defaultSecondary);
        UiRgb.TryParse(DefaultBackground, out UiRgb defaultBackground);
        Primary = UiRgb.TryParse(primary, out UiRgb p) ? p : defaultPrimary;
        Secondary = UiRgb.TryParse(secondary, out UiRgb s) ? s : defaultSecondary;
        Background = UiRgb.TryParse(background, out UiRgb b) ? b : defaultBackground;
        Text = Background.Contrast(new(255, 255, 255)) >= Background.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        UiRgb opposite = Text.R == 255 ? new(0, 0, 0) : new(255, 255, 255);
        UiRgb SurfaceAt(double amount)
        {
            UiRgb candidate = UiRgb.Mix(Background, Text, amount);
            return Text.Contrast(candidate) >= 4.5 ? candidate : UiRgb.Mix(Background, opposite, amount);
        }
        Chrome = SurfaceAt(.055);
        Surface = SurfaceAt(.105);
        Raised = SurfaceAt(.16);
        Border = SurfaceAt(.25);
        Muted = EnsureContrast(UiRgb.Mix(Background, Text, .62), Raised, Text);
        PrimaryText = EnsureContrast(Primary, Surface, Text);
        SecondaryText = EnsureContrast(Secondary, Surface, Text);
        OnPrimary = Primary.Contrast(new(255, 255, 255)) >= Primary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        OnSecondary = Secondary.Contrast(new(255, 255, 255)) >= Secondary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        SuccessText = EnsureContrast(Text.R == 255 ? new(109, 221, 166) : new(0, 112, 69), Raised, Text);
        DangerText = EnsureContrast(Text.R == 255 ? new(255, 118, 136) : new(176, 27, 45), Raised, Text);
    }

    private static UiRgb EnsureContrast(UiRgb color, UiRgb background, UiRgb toward)
    {
        for (int step = 0; step <= 20 && color.Contrast(background) < 4.5; step++)
            color = UiRgb.Mix(color, toward, .1);
        return color;
    }
}
