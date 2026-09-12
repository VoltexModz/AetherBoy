using System.Globalization;
using SDL3;

namespace AetherBoy.Desktop;

/// <summary>Bundled Noto text, with a bundled glyph atlas when SDL_ttf is unavailable.</summary>
internal sealed class SdlTextRenderer : IDisposable
{
    public int MinimumSize { get; set; } = 14;
    private readonly IntPtr renderer;
    private readonly string? fontPath;
    private readonly SdlFontAtlas atlas;
    private readonly bool initialized;
    private readonly Dictionary<(int, bool), IntPtr> fonts = new();
    private readonly Dictionary<(string, int, bool), (IntPtr Texture, float Width, float Height)> cache = new();

    public SdlTextRenderer(IntPtr renderer)
    {
        this.renderer = renderer;
        fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf");
        atlas = new SdlFontAtlas(renderer);
        try
        {
            initialized = Environment.GetEnvironmentVariable("AETHERBOY_TEXT_RENDERER") != "atlas" && TTF.Init();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // SDL_ttf is an optional enhancement, never a startup requirement.
        }
    }

    private IntPtr Font(int size, bool bold = false)
    {
        if (!initialized) return IntPtr.Zero;
        if (!fonts.TryGetValue((size, bold), out IntPtr font))
        {
            font = TTF.OpenFont(bold ? fontPath!.Replace("Regular", "Bold") : fontPath!, size);
            fonts[(size, bold)] = font;
        }
        return font;
    }

    public float Measure(string text, int size = 14, bool bold = false)
    {
        size = Math.Max(size, MinimumSize);
        IntPtr font = Font(size, bold);
        return font != IntPtr.Zero && TTF.GetStringSize(font, text, 0, out int width, out _)
            ? width : atlas.Measure(text, size, bold);
    }

    public string Fit(string text, float width, int size = 14, bool bold = false)
    {
        // Filenames and core errors are untrusted UI text.
        text = string.Concat(text.Select(c => char.IsControl(c) ? ' ' : c));
        if (Measure(text, size, bold) <= width) return text;
        int[] boundaries = StringInfo.ParseCombiningCharacters(text);
        int count = boundaries.Length - 1;
        while (count > 0 && Measure(text[..boundaries[count]] + "...", size, bold) > width) count--;
        return text[..(count >= 0 ? boundaries[count] : 0)] + "...";
    }

    public void Draw(float x, float y, string text, byte red, byte green, byte blue, int size = 14, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        size = Math.Max(size, MinimumSize);
        IntPtr font = Font(size, bold);
        if (font != IntPtr.Zero)
        {
            var key = (text, size, bold);
            if (!cache.TryGetValue(key, out var item))
            {
                if (cache.Count >= 256) ClearCache();
                IntPtr surface = TTF.RenderTextBlended(font, text, 0,
                    new SDL.Color { R = 255, G = 255, B = 255, A = 255 });
                if (surface != IntPtr.Zero)
                {
                    IntPtr texture = SDL.CreateTextureFromSurface(renderer, surface);
                    SDL.DestroySurface(surface);
                    if (texture != IntPtr.Zero)
                    {
                        SDL.GetTextureSize(texture, out float width, out float height);
                        item = (texture, width, height);
                        cache[key] = item;
                    }
                }
            }
            if (item.Texture != IntPtr.Zero)
            {
                SDL.SetTextureColorMod(item.Texture, red, green, blue);
                SDL.FRect destination = new() { X = x, Y = y, W = item.Width, H = item.Height };
                SDL.RenderTexture(renderer, item.Texture, IntPtr.Zero, in destination);
                return;
            }
        }

        atlas.Draw(x, y, text, red, green, blue, size, bold);
    }

    private void ClearCache()
    {
        foreach (var item in cache.Values) SDL.DestroyTexture(item.Texture);
        cache.Clear();
    }

    public void Dispose()
    {
        ClearCache();
        atlas.Dispose();
        foreach (IntPtr font in fonts.Values)
            if (font != IntPtr.Zero) TTF.CloseFont(font);
        fonts.Clear();
        if (initialized) TTF.Quit();
    }
}
