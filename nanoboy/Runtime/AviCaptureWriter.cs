using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AetherBoy.Runtime;

/// <summary>AVI 1.0: bottom-up BGR24 + stereo PCM16, indexed and finalized before publication.</summary>
internal sealed class AviCaptureWriter : IDisposable
{
    private readonly BinaryWriter writer;
    private readonly VideoGeometry geometry;
    private readonly int stride;
    private readonly byte[] pixels;
    private readonly List<(string Id, uint Offset, uint Size)> index = new();
    private readonly long riff, mainFrames, videoFrames, audioSamples, movi;
    private uint frameCount, sampleCount;
    private bool finished;

    public AviCaptureWriter(Stream output, VideoGeometry geometry, int sampleRate)
    {
        writer = new(output, Encoding.ASCII, leaveOpen: true); this.geometry = geometry;
        stride = (geometry.Width * 3 + 3) & ~3; pixels = new byte[stride * geometry.Height];
        riff = Begin("RIFF"); Four("AVI ");
        long hdrl = Begin("LIST"); Four("hdrl");
        long avih = Begin("avih");
        U((uint)Math.Round(1_000_000.0 * GameplayRecorder.FrameScale / GameplayRecorder.FrameRate));
        U((uint)(pixels.Length * 60L + sampleRate * 4L)); U(0); U(0x10 | 0x100); // indexed, interleaved
        mainFrames = Position; U(0); U(0); U(2); U((uint)pixels.Length);
        U((uint)geometry.Width); U((uint)geometry.Height); Zeros(16); End(avih);

        long video = Begin("LIST"); Four("strl");
        long vstrh = Begin("strh"); Four("vids"); Four("DIB "); U(0); U(0); U(0);
        U(GameplayRecorder.FrameScale); U(GameplayRecorder.FrameRate); U(0);
        videoFrames = Position; U(0); U((uint)pixels.Length); U(uint.MaxValue); U(0);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)geometry.Width); writer.Write((short)geometry.Height); End(vstrh);
        long vstrf = Begin("strf"); U(40); writer.Write(geometry.Width); writer.Write(geometry.Height);
        writer.Write((ushort)1); writer.Write((ushort)24); U(0); U((uint)pixels.Length); Zeros(16); End(vstrf); End(video);

        long audio = Begin("LIST"); Four("strl");
        long astrh = Begin("strh"); Four("auds"); U(0); U(0); U(0); U(0);
        U(4); U((uint)sampleRate * 4); U(0); audioSamples = Position; U(0);
        U((uint)sampleRate * 4); U(uint.MaxValue); U(4); Zeros(8); End(astrh);
        long astrf = Begin("strf"); writer.Write((ushort)1); writer.Write((ushort)2);
        U((uint)sampleRate); U((uint)sampleRate * 4); writer.Write((ushort)4); writer.Write((ushort)16); End(astrf); End(audio); End(hdrl);
        movi = Begin("LIST"); Four("movi");
    }

    public long EstimatedFinalSize(int pcmValues) => Position + 16 + pixels.Length + pcmValues * 2L + 8 + (index.Count + 2L) * 16;
    public void WriteFrame(ReadOnlySpan<int> source, ReadOnlySpan<short> pcm)
    {
        if (finished || source.Length != geometry.PixelCount || pcm.Length % 2 != 0) throw new InvalidOperationException();
        for (int y = 0; y < geometry.Height; y++)
            for (int x = 0; x < geometry.Width; x++)
            {
                int color = source[y * geometry.Width + x], offset = (geometry.Height - 1 - y) * stride + x * 3;
                pixels[offset] = (byte)color; pixels[offset + 1] = (byte)(color >> 8); pixels[offset + 2] = (byte)(color >> 16);
            }
        Chunk("00db", pixels);
        byte[] audio = new byte[pcm.Length * 2];
        for (int i = 0; i < pcm.Length; i++) { audio[i * 2] = (byte)pcm[i]; audio[i * 2 + 1] = (byte)(pcm[i] >> 8); }
        Chunk("01wb", audio); frameCount++; sampleCount += (uint)pcm.Length / 2;
    }

    public void Finish()
    {
        if (finished) return;
        End(movi);
        long idx = Begin("idx1");
        foreach (var item in index) { Four(item.Id); U(item.Id == "00db" ? 0x10u : 0); U(item.Offset); U(item.Size); }
        End(idx); End(riff); Patch(mainFrames, frameCount); Patch(videoFrames, frameCount); Patch(audioSamples, sampleCount);
        writer.Flush(); finished = true;
    }
    private void Chunk(string id, byte[] data)
    {
        uint offset = checked((uint)(Position - (movi + 4))); // Relative to the 'movi' FOURCC.
        long chunk = Begin(id); writer.Write(data); End(chunk); index.Add((id, offset, (uint)data.Length));
    }
    private long Position => writer.BaseStream.Position;
    private void Four(string value) => writer.Write(Encoding.ASCII.GetBytes(value));
    private void U(uint value) => writer.Write(value);
    private void Zeros(int count) => writer.Write(new byte[count]);
    private long Begin(string id) { Four(id); long at = Position; U(0); return at; }
    private void End(long sizeAt)
    {
        long size = Position - sizeAt - 4; Patch(sizeAt, checked((uint)size));
        if ((size & 1) != 0) writer.Write((byte)0);
    }
    private void Patch(long at, uint value) { long end = Position; writer.BaseStream.Position = at; U(value); writer.BaseStream.Position = end; }
    public void Dispose() => writer.Dispose();
}
