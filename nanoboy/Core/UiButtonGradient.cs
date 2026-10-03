using System;

namespace nanoboy.Core;

/// <summary>A readable accent ramp shared by both frontends, independent of stored colors.</summary>
public readonly record struct UiButtonGradient(UiRgb Start, UiRgb End, UiRgb Text)
{
    public static UiButtonGradient Create(UiRgb start, UiRgb end)
    {
        UiRgb black = new(0, 0, 0), white = new(255, 255, 255);
        UiRgb text = MinimumContrast(start, end, black) >= MinimumContrast(start, end, white) ? black : white;
        UiRgb toward = text == black ? white : black;
        // The middle of a complementary-color ramp can have less contrast than
        // either endpoint. Check the whole ramp, including hover/pressed colors.
        for (int step = 0; step <= 100; step++)
        {
            UiRgb first = UiRgb.Mix(start, toward, step / 100d);
            UiRgb last = UiRgb.Mix(end, toward, step / 100d);
            if (MinimumContrast(first, last, text) >= 4.6)
                return new(first, last, text);
        }
        return new(toward, toward, text);
    }

    private static double MinimumContrast(UiRgb start, UiRgb end, UiRgb text)
    {
        double minimum = double.MaxValue;
        for (int step = 0; step <= 64; step++)
            minimum = Math.Min(minimum, UiRgb.Mix(start, end, step / 64d).Contrast(text));
        return minimum;
    }
}
