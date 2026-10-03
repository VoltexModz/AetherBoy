using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Branding;
using nanoboy.Platform.Audio;

namespace nanoboy.Controls;

internal sealed class AetherBootIntro : Control
{
    private readonly Bitmap mark;
    private readonly bool custom;
    private double elapsed;
    private bool skipped;
    internal void Skip() => skipped = true;
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4)) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData is Keys.Escape or Keys.Enter or Keys.Space) Skip();
        return true;
    }

    internal AetherBootIntro(BootIntroStore store, BootIntroOptions options)
    {
        Name = "aetherBootIntro";
        AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy-Startanimation");
        Dock = DockStyle.Fill;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        mark = AppBrand.CreateMarkBitmap();
        if (store.ReadAsset(options.Image, true) is { } bytes)
        {
            try
            {
                using var decoded = DecodeImage(bytes);
                var replacement = new Bitmap(decoded); mark.Dispose(); mark = replacement; custom = true;
            }
            catch (Exception e) when (e is ArgumentException or InvalidDataException or OutOfMemoryException) { }
        }
    }

    internal static Bitmap DecodeImage(byte[] bytes)
    {
        BootIntroStore.ValidatePng(bytes);
        using var stream = new MemoryStream(bytes, false);
        using var source = Image.FromStream(stream, false, true);
        return new Bitmap(source);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(AetherColors.Void);
        var frame = BootIntroFrame.At(elapsed);
        float side = Math.Min(Width * (custom ? .50f : .70f), Height * (custom ? .43f : .64f)) * frame.Scale;
        float ratio = Math.Min(side / mark.Width, side / mark.Height);
        float w = mark.Width * ratio, h = mark.Height * ratio;
        var box = new Rectangle((int)((Width-w)/2), (int)((Height-h)/2 - 12 + frame.OffsetY), (int)w, (int)h);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = frame.Opacity });
        if (w > 0 && h > 0) g.DrawImage(mark, box, 0, 0, mark.Width, mark.Height, GraphicsUnit.Pixel, attributes);
        // The approved artwork already includes the wordmark.
        float lineY = box.Bottom + 24;
        using var line = new LinearGradientBrush(new RectangleF(Width*.35f, lineY, Math.Max(1,Width*.3f), 2),
            Color.FromArgb((int)(255*frame.Opacity), AetherColors.Primary), Color.FromArgb((int)(255*frame.Opacity), AetherColors.Secondary), 0f);
        g.FillRectangle(line, Width*.35f, lineY, Width*.3f, 2);
    }

    internal static async Task PlayAsync(Control parent, BootIntroStore store, float volume, CancellationToken token, Action<AetherBootIntro?>? active = null)
    {
        var options = store.Load();
        using var overlay = new AetherBootIntro(store, options);
        NAudioSoundOut? sound = null;
        float[] samples = options.SoundEnabled && volume > 0 ? store.ReadSound(options) : [];
        try
        {
            parent.Controls.Add(overlay); overlay.BringToFront(); overlay.Focus(); active?.Invoke(overlay);
            if (samples.Length != 0)
                try { sound = new NAudioSoundOut(BootIntroStore.SampleRate, volume, 60); }
                catch (Exception e) when (e is InvalidOperationException or PlatformNotSupportedException) { Debug.WriteLine(e); }
            var timer = Stopwatch.StartNew(); int sent = 0;
            while (timer.ElapsedMilliseconds < BootIntroStore.DurationMs && !overlay.skipped && !parent.IsDisposed && parent.Visible)
            {
                token.ThrowIfCancellationRequested(); overlay.elapsed = timer.Elapsed.TotalMilliseconds; overlay.Invalidate();
                // Feed bounded 20-ms blocks; never enqueue a full jingle into the game ring.
                int end = Math.Min(samples.Length, (int)((timer.ElapsedMilliseconds + 20) * BootIntroStore.SampleRate / 1000));
                if (end > sent)
                {
                    int start = Math.Max(sent, end - BootIntroStore.SampleRate / 20);
                    if (Form.ActiveForm == parent.FindForm()) sound?.Submit(samples[start..end], BootIntroStore.SampleRate);
                    else sound?.ClearBuffer();
                    sent = end;
                }
                await Task.Delay(16, token);
            }
        }
        finally { active?.Invoke(null); sound?.Dispose(); if (!parent.IsDisposed) parent.Controls.Remove(overlay); }
    }

    protected override void Dispose(bool disposing) { if (disposing) mark.Dispose(); base.Dispose(disposing); }
}
