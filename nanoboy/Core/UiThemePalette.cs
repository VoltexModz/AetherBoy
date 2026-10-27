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
    public const string DefaultBackground = "#0D1019";

    public UiRgb Primary { get; }
    public UiRgb Secondary { get; }
    public UiRgb Background { get; }
    public UiRgb Chrome { get; }
    public UiRgb Surface { get; }
    public UiRgb Raised { get; }
    public UiRgb Border { get; }
    public UiRgb Selection { get; }
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
        bool pocket = Primary.Hex == "#386D4A" && Secondary.Hex == "#B88029" && Background.Hex == "#F1E9D2";
        Text = original ? new(240, 242, 248) : pocket ? new(39, 56, 43) : Background.Contrast(new(255, 255, 255)) >= Background.Contrast(new(0, 0, 0))
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
        // pen.dev desktop study, 27 October 2026. Other palettes keep user colors intact.
        Chrome = original ? new(17, 21, 32) : pocket ? new(233, 225, 203) : SurfaceAt(.025);
        Surface = original ? new(23, 28, 41) : pocket ? new(250, 246, 234) : SurfaceAt(.055);
        Raised = original ? new(33, 40, 56) : pocket ? new(255, 255, 255) : SurfaceAt(.09);
        Border = original ? new(48, 56, 74) : pocket ? new(200, 192, 172) : SurfaceAt(.20);
        Selection = original ? new(41, 34, 57) : pocket ? new(219, 230, 214) : UiRgb.Mix(Surface, Primary, .12);
        UiRgb ReadableOnSurfaces(UiRgb color)
        {
            UiRgb[] surfaces = [Background, Chrome, Surface, Raised, Selection];
            foreach (UiRgb surface in surfaces) color = EnsureContrast(color, surface, Text);
            foreach (UiRgb surface in surfaces)
                if (color.Contrast(surface) < 4.5) return Text;
            return color;
        }
        Muted = ReadableOnSurfaces(original ? new(161, 171, 192) : pocket ? new(94, 104, 92) : UiRgb.Mix(Background, Text, .62));
        PrimaryText = ReadableOnSurfaces(original ? new(170, 134, 255) : Primary);
        SecondaryText = ReadableOnSurfaces(original ? new(120, 216, 213) : pocket ? new(116, 83, 33) : Secondary);
        OnPrimary = Primary.Contrast(new(255, 255, 255)) >= Primary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        OnSecondary = Secondary.Contrast(new(255, 255, 255)) >= Secondary.Contrast(new(0, 0, 0))
            ? new(255, 255, 255) : new(0, 0, 0);
        SuccessText = ReadableOnSurfaces(dark ? new(84, 237, 176) : new(0, 112, 69));
        DangerText = ReadableOnSurfaces(dark ? new(255, 92, 132) : new(176, 27, 45));
        UiRgb action = original ? new(120, 67, 206) : Primary;
        UiRgb actionEnd = original ? new(84, 108, 196) : pocket ? new(64, 105, 75) : UiRgb.Mix(Primary, Secondary, .28);
        Button = UiButtonGradient.Create(action, actionEnd);
        ButtonHover = UiButtonGradient.Create(UiRgb.Mix(action, new(255, 255, 255), .13), UiRgb.Mix(actionEnd, new(255, 255, 255), .13));
        ButtonPressed = UiButtonGradient.Create(UiRgb.Mix(action, new(0, 0, 0), .16), UiRgb.Mix(actionEnd, new(0, 0, 0), .16));
    }

    private static UiRgb EnsureContrast(UiRgb color, UiRgb background, UiRgb toward)
    {
        for (int step = 0; step < 32 && color.Contrast(background) < 4.5; step++)
            color = UiRgb.Mix(color, toward, .25);
        return color.Contrast(background) >= 4.5 ? color : toward;
    }
}
