using System.Text.Json;
using SDL3;

namespace AetherBoy.Desktop;

/// <summary>Portable antialiased text with no system fonts or SDL_ttf requirement.</summary>
internal sealed class SdlFontAtlas : IDisposable
{
    internal sealed record Glyph(int X, int Y, float Advance);
    private readonly IntPtr renderer;
    private readonly (IntPtr Texture, Dictionary<string, Glyph> Glyphs)[] faces;

    public SdlFontAtlas(IntPtr renderer)
    {
        this.renderer = renderer;
        faces = new (IntPtr, Dictionary<string, Glyph>)[2];
        try
        {
            for (int i = 0; i < faces.Length; i++)
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts",
                    $"NotoSans-{(i == 0 ? "Regular" : "Bold")}-atlas");
                var glyphs = JsonSerializer.Deserialize<Dictionary<string, Glyph>>(File.ReadAllText(path + ".json"))!;
                IntPtr surface = SDL.LoadPNG(path + ".png");
                if (surface == IntPtr.Zero) throw new IOException("Cannot load bundled font: " + SDL.GetError());
                IntPtr texture;
                try { texture = SDL.CreateTextureFromSurface(renderer, surface); }
                finally { SDL.DestroySurface(surface); }
                if (texture == IntPtr.Zero) throw new IOException("Cannot create font texture: " + SDL.GetError());
                faces[i] = (texture, glyphs);
                SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Linear);
                SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            }
        }
        catch { Dispose(); throw; }
    }

    public float Measure(string text, int size, bool bold) =>
        text.EnumerateRunes().Sum(c => GetGlyph(c.ToString(), bold).Advance) * size / 32f;

    private Glyph GetGlyph(string character, bool bold)
    {
        var glyphs = faces[bold ? 1 : 0].Glyphs;
        return glyphs.TryGetValue(character, out var glyph) ? glyph : glyphs["?"];
    }

    public void Draw(float x, float y, string text, byte red, byte green, byte blue, int size, bool bold)
    {
        IntPtr texture = faces[bold ? 1 : 0].Texture;
        SDL.SetTextureColorMod(texture, red, green, blue);
        float scale = size / 32f;
        foreach (var rune in text.EnumerateRunes())
        {
            Glyph glyph = GetGlyph(rune.ToString(), bold);
            SDL.FRect source = new() { X = glyph.X, Y = glyph.Y, W = 64, H = 44 };
            SDL.FRect target = new() { X = x - 2 * scale, Y = y, W = 64 * scale, H = 44 * scale };
            SDL.RenderTexture(renderer, texture, in source, in target);
            x += glyph.Advance * scale;
        }
    }

    public void Dispose()
    {
        foreach (var face in faces)
            if (face.Texture != IntPtr.Zero) SDL.DestroyTexture(face.Texture);
    }
}
