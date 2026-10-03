using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherBoy.Runtime;

/// <summary>Presentation only. Never changes firmware, ROMs, clocks or save states.</summary>
public sealed record BootIntroOptions(bool Enabled = true, bool SoundEnabled = true,
    string? Image = null, string? Sound = null);

public sealed class BootIntroStore
{
    public const int DurationMs = 2400;
    public const int SampleRate = 44100;
    public const int MaxAssetBytes = 4 * 1024 * 1024;
    public string DirectoryPath { get; }
    public BootIntroStore(string root) => DirectoryPath = Path.GetFullPath(root);

    public BootIntroOptions Load()
    {
        string path = Path.Combine(DirectoryPath, "intro.json");
        if (!File.Exists(path)) return new();
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length > 4096) return new(Enabled: false);
            return JsonSerializer.Deserialize<BootIntroOptions>(file) ?? new(Enabled: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { return new(Enabled: false); }
    }

    public void Save(BootIntroOptions options)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporary = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(options));
            File.Move(temporary, Path.Combine(DirectoryPath, "intro.json"), true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public byte[]? ReadAsset(string? name, bool image)
    {
        // Persisted settings cannot name an arbitrary file or network path.
        string extension = image ? ".png" : ".wav";
        if (name is null || name.Length != 64 + extension.Length || !name.EndsWith(extension, StringComparison.Ordinal)) return null;
        foreach (char c in name.AsSpan(0, 64)) if (!Uri.IsHexDigit(c)) return null;
        try { return ReadBounded(Path.Combine(DirectoryPath, name)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { return null; }
    }

    public string Import(string sourcePath, bool image, Action<byte[]>? decodeImage = null)
    {
        var bytes = ReadBounded(sourcePath);
        if (image) { ValidatePng(bytes); decodeImage?.Invoke(bytes); }
        else _ = DecodeWave(bytes);
        string name = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + (image ? ".png" : ".wav");
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, name);
        if (!File.Exists(path))
        {
            // Publish only complete assets, before changing the selected preference.
            string temp = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, false); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        return name;
    }

    private static byte[] ReadBounded(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > MaxAssetBytes) throw new InvalidDataException("File size must be between 1 byte and 4 MiB.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new InvalidDataException("The file changed while reading.");
        return bytes;
    }

    public static (int Width, int Height) ValidatePng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 33 || bytes.Length > MaxAssetBytes || !bytes[..8].SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            BinaryPrimitives.ReadInt32BigEndian(bytes[8..]) != 13 || !bytes.Slice(12,4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Choose a PNG image.");
        int w = BinaryPrimitives.ReadInt32BigEndian(bytes[16..]), h = BinaryPrimitives.ReadInt32BigEndian(bytes[20..]);
        if (w is < 1 or > 2048 || h is < 1 or > 2048) throw new InvalidDataException("PNG dimensions must not exceed 2048 by 2048.");
        return (w, h);
    }

    public static float[] DecodeWave(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 44 || bytes.Length > MaxAssetBytes || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes.Slice(8,4).SequenceEqual("WAVE"u8) || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != bytes.Length - 8)
            throw new InvalidDataException("Choose an uncompressed 16-bit PCM WAV.");
        int rate = 0, channels = 0; ReadOnlySpan<byte> data = default; bool format = false;
        for (int p = 12; p <= bytes.Length - 8;)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(p+4)..]);
            if (size > bytes.Length - p - 8) throw new InvalidDataException("Truncated WAV chunk.");
            var chunk = bytes.Slice(p+8, (int)size);
            if (bytes.Slice(p,4).SequenceEqual("fmt "u8))
            {
                if (format || size < 16 || BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]) != 16)
                    throw new InvalidDataException("Only 16-bit PCM WAV is supported.");
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                if (channels is not (1 or 2) || rate is < 8000 or > 48000 || BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]) != channels * 2 ||
                    BinaryPrimitives.ReadInt32LittleEndian(chunk[8..]) != rate * channels * 2) throw new InvalidDataException("Invalid PCM format.");
                format = true;
            }
            if (bytes.Slice(p,4).SequenceEqual("data"u8))
            { if (!data.IsEmpty) throw new InvalidDataException("Multiple WAV data chunks."); data = chunk; }
            p = checked(p + 8 + (int)size + ((int)size & 1));
        }
        if (!format || data.IsEmpty || data.Length % (channels * 2) != 0) throw new InvalidDataException("Incomplete WAV audio.");
        int frames = data.Length / (channels * 2);
        if ((long)frames * 1000 > (long)rate * DurationMs) throw new InvalidDataException("Sound must be at most 2.4 seconds long.");
        var result = new float[(int)((long)frames * SampleRate / rate)];
        for (int i = 0; i < result.Length; i++)
        {
            int frame = (int)((long)i * rate / SampleRate);
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += BinaryPrimitives.ReadInt16LittleEndian(data[((frame * channels + c) * 2)..]) / 32768f;
            // Small edge fade prevents clicks on cut samples; no normalization to full volume.
            result[i] = sum / channels * Math.Min(1, Math.Min(i, result.Length - 1 - i) / 220f);
        }
        return result;
    }

    public float[] ReadSound(BootIntroOptions options)
    {
        var bytes = ReadAsset(options.Sound, false);
        if (bytes is not null) try { return DecodeWave(bytes); } catch (InvalidDataException) { }
        return CreateChime();
    }

    public static float[] CreateChime()
    {
        // Original four-tone motif, synthesized here; no console recording or firmware asset.
        var samples = new float[SampleRate * DurationMs / 1000];
        double[] notes = [329.63, 493.88, 622.25, 987.77];
        for (int n = 0; n < notes.Length; n++)
            for (int i = 0; i < SampleRate; i++)
            {
                int target = (int)((.24 + n * .14) * SampleRate) + i;
                double t = i / (double)SampleRate;
                double envelope = Math.Min(1, t / .012) * Math.Exp(-t * 7) * Math.Min(1, (1-t) * 40);
                samples[target] += (float)(.16 * envelope * (Math.Sin(2 * Math.PI * notes[n] * t) + .18 * Math.Sin(4 * Math.PI * notes[n] * t)));
            }
        return samples;
    }
}

public readonly record struct BootIntroFrame(float Opacity, float Scale, float OffsetY)
{
    public static BootIntroFrame At(double milliseconds)
    {
        double progress = Math.Clamp(milliseconds / 620, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);
        return new((float)Math.Min(eased, Math.Clamp((BootIntroStore.DurationMs - milliseconds) / 260, 0, 1)),
            (float)(.86 + .14 * eased), (float)(22 * (1-eased)));
    }
}
