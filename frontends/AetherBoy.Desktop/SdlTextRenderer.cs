using SDL3;

namespace AetherBoy.Desktop;

/// <summary>Cached system-font text, with a dependency-free SDL bitmap fallback.</summary>
internal sealed class SdlTextRenderer : IDisposable
{
    private readonly IntPtr renderer;
    private readonly string? fontPath;
    private readonly bool initialized;
    private readonly Dictionary<int, IntPtr> fonts = new();
    private readonly Dictionary<(string, int), (IntPtr Texture, float Width, float Height)> cache = new();

    public SdlTextRenderer(IntPtr renderer)
    {
        this.renderer = renderer;
        fontPath = new[]
        {
            "/usr/share/fonts/noto/NotoSans-Regular.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
        }.FirstOrDefault(File.Exists);
        try
        {
            initialized = fontPath is not null && TTF.Init();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // SDL_ttf is an optional enhancement, never a startup requirement.
        }
    }

    private IntPtr Font(int size)
    {
        if (!initialized) return IntPtr.Zero;
        if (!fonts.TryGetValue(size, out IntPtr font))
        {
            font = TTF.OpenFont(fontPath!, size);
            fonts[size] = font;
        }
        return font;
    }

    public float Measure(string text, int size = 14)
    {
        IntPtr font = Font(size);
        return font != IntPtr.Zero && TTF.GetStringSize(font, text, 0, out int width, out _)
            ? width : text.Length * size;
    }

    public string Fit(string text, float width, int size = 14)
    {
        // Filenames and core errors are untrusted UI text.
        text = string.Concat(text.Select(c => char.IsControl(c) ? ' ' : c));
        if (Measure(text, size) <= width) return text;
        int length = text.Length;
        while (length > 0 && Measure(text[..length] + "...", size) > width) length--;
        return text[..length] + "...";
    }

    public void Draw(float x, float y, string text, byte red, byte green, byte blue, int size = 14)
    {
        if (string.IsNullOrEmpty(text)) return;
        IntPtr font = Font(size);
        if (font != IntPtr.Zero)
        {
            var key = (text, size);
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

        SDL.GetRenderScale(renderer, out float previousX, out float previousY);
        float scale = size / 8f;
        SDL.SetRenderScale(renderer, previousX * scale, previousY * scale);
        SDL.SetRenderDrawColor(renderer, red, green, blue, 255);
        SDL.RenderDebugText(renderer, x / scale, y / scale, text);
        SDL.SetRenderScale(renderer, previousX, previousY);
    }

    private void ClearCache()
    {
        foreach (var item in cache.Values) SDL.DestroyTexture(item.Texture);
        cache.Clear();
    }

    public void Dispose()
    {
        ClearCache();
        foreach (IntPtr font in fonts.Values)
            if (font != IntPtr.Zero) TTF.CloseFont(font);
        fonts.Clear();
        if (initialized) TTF.Quit();
    }
}
