using System;

namespace nanoboy.Core;

/// <summary>Shared logical desktop measurements from the Windows/Linux pen.dev design.</summary>
public readonly record struct UiDesktopLayout(float Navigation, float Inset, float ContentX,
    float ContentWidth, float GameX, float GameY, float GameWidth, float GameHeight, float RailX, float RailWidth)
{
    public const int Header = 64, Footer = 32;
    public static UiDesktopLayout Create(float width, float height)
    {
        bool compact = width < 1200;
        float nav = compact ? 72 : 184, inset = compact ? 24 : 32;
        float contentX = nav + inset, contentWidth = width - contentX - inset;
        float rail = compact ? 224 : 264, gap = compact ? 20 : 24;
        float gameY = Header + inset + (compact ? 76 : 85);
        return new(nav, inset, contentX, contentWidth, contentX, gameY,
            Math.Max(120, contentWidth - rail - gap), Math.Max(120, height - Footer - inset - gameY - 68),
            width - inset - rail, rail);
    }
}

public enum UiIcon { None, Favorite, Play, Library, Save, Link, Tools, Settings, Open, Rewind, Turbo, Fullscreen, More, Volume, Close, Minimize, Maximize }

/// <summary>Small shared line icons in a 24-unit viewport; both native renderers use the same geometry.</summary>
public static class UiIconGeometry
{
    public static ReadOnlySpan<float> Lines(UiIcon icon) => icon switch
    {
        UiIcon.Favorite => [12,3,15,9, 15,9,22,10, 22,10,17,15, 17,15,18,22, 18,22,12,18, 12,18,6,22, 6,22,7,15, 7,15,2,10, 2,10,9,9, 9,9,12,3],
        UiIcon.Play => [5,5,19,12, 19,12,5,19, 5,19,5,5],
        UiIcon.Library => [4,4,4,20, 8,4,8,20, 13,4,13,20, 17,5,21,19, 3,20,14,20],
        UiIcon.Save => [4,3,17,3, 17,3,21,7, 21,7,21,21, 21,21,3,21, 3,21,3,3, 7,3,7,9, 7,9,16,9, 16,9,16,3, 7,21,7,14, 7,14,17,14, 17,14,17,21],
        UiIcon.Link => [6,3,6,8, 3,8,9,8, 3,8,3,12, 9,8,9,12, 3,12,9,12, 6,12,6,18, 6,18,18,18, 18,18,18,12, 15,12,21,12, 15,12,15,7, 21,12,21,7, 15,7,21,7, 18,7,18,3],
        UiIcon.Tools => [3,6,8,6, 12,6,21,6, 3,12,14,12, 18,12,21,12, 3,18,6,18, 10,18,21,18, 8,3,8,9, 14,9,14,15, 6,15,6,21],
        UiIcon.Settings => [3,6,21,6, 3,12,21,12, 3,18,21,18, 7,3,7,9, 16,9,16,15, 10,15,10,21],
        UiIcon.Open => [3,7,3,20, 3,7,9,7, 9,7,11,10, 11,10,21,10, 21,10,18,20, 18,20,3,20, 3,20,7,12, 7,12,21,12],
        UiIcon.Rewind => [12,5,3,12, 3,12,12,19, 12,19,12,5, 21,5,12,12, 12,12,21,19, 21,19,21,5],
        UiIcon.Turbo => [3,5,12,12, 12,12,3,19, 3,19,3,5, 12,5,21,12, 21,12,12,19, 12,19,12,5],
        UiIcon.Fullscreen => [3,9,3,3, 3,3,9,3, 15,3,21,3, 21,3,21,9, 21,15,21,21, 21,21,15,21, 9,21,3,21, 3,21,3,15],
        UiIcon.Volume => [3,9,7,9, 7,9,12,5, 12,5,12,19, 12,19,7,15, 7,15,3,15, 3,15,3,9, 16,8,18,12, 18,12,16,16, 19,5,22,12, 22,12,19,19],
        UiIcon.Close => [6,6,18,18, 18,6,6,18],
        UiIcon.Minimize => [6,12,18,12],
        UiIcon.Maximize => [5,5,19,5, 19,5,19,19, 19,19,5,19, 5,19,5,5],
        UiIcon.More => [4,12,5,12, 11,12,12,12, 18,12,19,12],
        _ => []
    };
}
