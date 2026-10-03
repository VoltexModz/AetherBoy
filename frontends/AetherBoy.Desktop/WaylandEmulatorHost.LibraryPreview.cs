using System.Runtime.InteropServices;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LinuxLibrarySort librarySort;
    private bool libraryGrid;
    private readonly Dictionary<string, LinuxStateCard> libraryPreviewCards = new();
    private readonly Dictionary<string, IntPtr> libraryPreviewTextures = new();
    private Task<(string Identity, LinuxStateCard Card)>? libraryPreviewLoad;

    private void PollLibraryPreview()
    {
        if (libraryPreviewLoad is not { IsCompleted: true } pending) return;
        libraryPreviewLoad = null;
        try
        {
            var result = pending.GetAwaiter().GetResult();
            if (libraryPreviewCards.Count >= 24)
            {
                string oldest = libraryPreviewCards.Keys.First();
                libraryPreviewCards.Remove(oldest);
                if (libraryPreviewTextures.Remove(oldest, out IntPtr texture)) SDL.DestroyTexture(texture);
            }
            libraryPreviewCards[result.Identity] = result.Card;
        }
        catch (Exception) { /* Missing or unreadable optional preview is rendered as a placeholder. */ }
    }

    private void RequestLibraryPreview(string identity)
    {
        if (libraryPreviewCards.ContainsKey(identity) || libraryPreviewLoad is not null) return;
        string path = Path.Combine(dataPaths.Data, "states", identity, "game.rom");
        libraryPreviewLoad = Task.Run(() => (identity, LinuxStateGallery.ReadPreview(path)));
    }

    private void ClearLibraryPreviews()
    {
        foreach (IntPtr texture in libraryPreviewTextures.Values) SDL.DestroyTexture(texture);
        libraryPreviewTextures.Clear(); libraryPreviewCards.Clear();
    }

    private void DrawLibraryPreview(string identity, float x, float y, float width, float height)
    {
        Paint(x, y, width, height, Colors.Chrome);
        if (!libraryPreviewCards.TryGetValue(identity, out LinuxStateCard? card))
        { RequestLibraryPreview(identity); Ink(x + 8, y + height / 2 - 8, global::AetherBoy.Runtime.Localization.UiText.Get("Loading…"), 11, Colors.Muted); return; }
        if (card.Pixels is null)
        { Ink(x + 8, y + height / 2 - 8, card.Exists ? global::AetherBoy.Runtime.Localization.UiText.Get("No preview") : global::AetherBoy.Runtime.Localization.UiText.Get("No resume"), 11, Colors.Muted); return; }
        if (!libraryPreviewTextures.TryGetValue(identity, out IntPtr texture))
        {
            texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Static, card.Width, card.Height);
            if (texture == IntPtr.Zero) return;
            SDL.UpdateTexture(texture, IntPtr.Zero, MemoryMarshal.AsBytes(card.Pixels.AsSpan()).ToArray(), card.Width * 4);
            SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Nearest);
            libraryPreviewTextures[identity] = texture;
        }
        float scale = Math.Min(width / card.Width, height / card.Height);
        SDL.FRect target = new()
        {
            X = x + (width - card.Width * scale) / 2, Y = y + (height - card.Height * scale) / 2,
            W = card.Width * scale, H = card.Height * scale
        };
        SDL.RenderTexture(renderer, texture, IntPtr.Zero, in target);
    }
}
