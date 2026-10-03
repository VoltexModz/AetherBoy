using System.Diagnostics;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private BootIntroStore IntroStore => new(Path.Combine(dataPaths.Data, "BootIntro"));
    private Stopwatch? introClock;
    private Action? afterIntro;
    private bool introResumePrevious;
    private bool introCustomImage;
    private IntPtr introTexture;
    private SdlAudioOutput? introAudio;
    private float[] introSamples = [];
    private int introSent;
    private bool? pickingIntroImage;

    private void StartBootIntro(Action? next)
    {
        if (introClock is not null || IsOnlineLink) return;
        var prefs = IntroStore.Load();
        introResumePrevious = session is not null && !session.LatestSnapshot.IsPaused;
        if (session is not null)
        {
            session.SetTurboAsync(false).GetAwaiter().GetResult();
            session.SetPausedAsync(true).GetAwaiter().GetResult();
        }
        pressedKeys.Clear(); audioOutput?.Clear();
        afterIntro = next; introSent = 0; introCustomImage = false;
        introTexture = IntPtr.Zero;
        if (IntroStore.ReadAsset(prefs.Image, true) is { } bytes)
        {
            try { introTexture = CreateIntroTexture(bytes); introCustomImage = true; }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException) { diagnostics.Failure("intro_image", e); }
        }
        introSamples = options.AudioEnabled && prefs.SoundEnabled && options.AudioVolume > 0 ? IntroStore.ReadSound(prefs) : [];
        if (introSamples.Length > 0)
        {
            try { introAudio = new SdlAudioOutput(BootIntroStore.SampleRate, options.AudioVolume / 100f); }
            catch (InvalidOperationException e) { diagnostics.Failure("intro_audio", e); introAudio = null; }
        }
        introClock = Stopwatch.StartNew();
    }

    private unsafe IntPtr CreateIntroTexture(byte[] bytes)
    {
        BootIntroStore.ValidatePng(bytes);
        fixed (byte* pointer = bytes)
        {
            IntPtr io = SDL.IOFromConstMem((IntPtr)pointer, (nuint)bytes.Length);
            if (io == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot read intro image."));
            IntPtr surface = SDL.LoadPNGIO(io, true);
            if (surface == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot decode intro PNG."));
            try
            {
                IntPtr texture = SDL.CreateTextureFromSurface(renderer, surface);
                if (texture == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot create intro texture."));
                SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Linear);
                SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
                return texture;
            }
            finally { SDL.DestroySurface(surface); }
        }
    }

    private void UpdateBootIntro()
    {
        if (introClock is null) return;
        if (introClock.ElapsedMilliseconds >= BootIntroStore.DurationMs) { FinishBootIntro(true); return; }
        int end = Math.Min(introSamples.Length, (int)((introClock.ElapsedMilliseconds + 20) * BootIntroStore.SampleRate / 1000));
        if (end <= introSent) return;
        int start = Math.Max(introSent, end - BootIntroStore.SampleRate / 20);
        try
        {
            if (windowFocused) introAudio?.Submit(introSamples[start..end], BootIntroStore.SampleRate);
            else introAudio?.Clear();
        }
        catch (InvalidOperationException e) { diagnostics.Failure("intro_audio", e); introAudio?.Dispose(); introAudio = null; }
        introSent = end;
    }

    private void FinishBootIntro(bool proceed)
    {
        if (introClock is null) return;
        introClock = null;
        var next = afterIntro; afterIntro = null;
        introAudio?.Dispose(); introAudio = null; introSamples = [];
        if (introTexture != IntPtr.Zero) { SDL.DestroyTexture(introTexture); introTexture = IntPtr.Zero; }
        pressedKeys.Clear(); sofaAwaitNeutral = true;
        if (introResumePrevious && !disposed && session is not null)
        {
            resumeAfterLoad = true;
            ResumeAfterFailedLoad();
        }
        introResumePrevious = false;
        if (proceed && !disposed) next?.Invoke();
    }

    private void DrawBootIntro()
    {
        var frame = BootIntroFrame.At(introClock?.Elapsed.TotalMilliseconds ?? 0);
        IntPtr texture = introTexture == IntPtr.Zero ? brandTexture : introTexture;
        SDL.GetTextureSize(texture, out float width, out float height);
        float side = Math.Min(LogicalWidth * (introCustomImage ? .5f : .7f), LogicalHeight * (introCustomImage ? .43f : .64f)) * frame.Scale;
        float ratio = side / Math.Max(1, Math.Max(width, height));
        SDL.FRect target = new() { X = (LogicalWidth - width*ratio)/2, Y = (LogicalHeight-height*ratio)/2 - 12 + frame.OffsetY, W = width*ratio, H = height*ratio };
        SDL.SetTextureAlphaMod(texture, (byte)(255 * frame.Opacity));
        SDL.RenderTexture(renderer, texture, IntPtr.Zero, in target);
        SDL.SetTextureAlphaMod(texture, 255);
        SDL.Color Fade(SDL.Color color) => new()
        {
            R = (byte)(Colors.Void.R + (color.R-Colors.Void.R)*frame.Opacity),
            G = (byte)(Colors.Void.G + (color.G-Colors.Void.G)*frame.Opacity),
            B = (byte)(Colors.Void.B + (color.B-Colors.Void.B)*frame.Opacity), A = 255
        };
        // The approved artwork already includes the wordmark.
        AetherShapeRenderer.Fill(renderer, LogicalWidth * .35f, target.Y + target.H + 24,
            LogicalWidth * .3f, 2, 0, Fade(Colors.Primary), Fade(Colors.Secondary));
    }

    private void DrawBootIntroSettings()
    {
        var store = IntroStore; var prefs = store.Load();
        Ink(300, 260, global::AetherBoy.Runtime.Localization.UiText.Get("Game start animation"), 22, bold: true);
        DrawSettingsParagraph(300, 305, global::AetherBoy.Runtime.Localization.UiText.Get("A 2.4-second logo and sound before opening a single-player game. Not on reset or quick load; separate from firmware. Mute and volume also apply here."), 790, 16);
        void Save(BootIntroOptions value) => TryUiAction(() => store.Save(value), global::AetherBoy.Runtime.Localization.UiText.Get("Start animation preference saved."));
        ActionButton(300, 380, 390, 44, prefs.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Before each game: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Before each game: off"), () => Save(prefs with { Enabled = !prefs.Enabled }), prefs.Enabled);
        ActionButton(710, 380, 390, 44, prefs.SoundEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Intro sound: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Intro sound: off"), () => Save(prefs with { SoundEnabled = !prefs.SoundEnabled }), prefs.SoundEnabled);
        ActionButton(300, 437, 390, 44, prefs.Image is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose custom PNG") : global::AetherBoy.Runtime.Localization.UiText.Get("Replace custom PNG"), () => PickIntroAsset(true));
        ActionButton(710, 437, 390, 44, prefs.Sound is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose custom WAV") : global::AetherBoy.Runtime.Localization.UiText.Get("Replace custom WAV"), () => PickIntroAsset(false));
        DrawSettingsParagraph(300, 493, global::AetherBoy.Runtime.Localization.UiText.Get("PNG: up to 2048 x 2048, 4 MiB. WAV: 16-bit PCM, mono/stereo, 8-48 kHz, up to 2.4 seconds. Imported files are copied into AetherBoy data."), 790, 14);
        ActionButton(300, 571, 390, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Preview intro"), () => StartBootIntro(null), enabled: !IsOnlineLink && localLinkSession is null);
        ActionButton(710, 571, 390, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Use AetherBoy logo and sound"), () => Save(prefs with { Image = null, Sound = null }));
    }

    private void PickIntroAsset(bool image)
    {
        if (IsLoading || fileDialogOpen != 0) return;
        pickingIntroImage = image;
        ShowRomDialog();
    }

    private void ImportIntroAsset(string path, bool image)
    {
        TryUiAction(() =>
        {
            var store = IntroStore; var prefs = store.Load();
            string name = store.Import(path, image, bytes => { IntPtr texture = CreateIntroTexture(bytes); SDL.DestroyTexture(texture); });
            store.Save(image ? prefs with { Image = name } : prefs with { Sound = name });
        }, global::AetherBoy.Runtime.Localization.UiText.Get("Intro file copied into AetherBoy data."));
    }
}
