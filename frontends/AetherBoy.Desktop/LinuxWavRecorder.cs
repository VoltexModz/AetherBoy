using System.Text;
using System.Threading.Channels;

namespace AetherBoy.Desktop;

internal sealed class LinuxWavRecorder : IDisposable
{
    private readonly Channel<float[]> queue = Channel.CreateBounded<float[]>(128);
    private readonly Task writer;
    private readonly int rate;
    private readonly int channels;
    private long dropped;
    public long DroppedBlocks => Interlocked.Read(ref dropped);
    public string Path { get; }
    public string? Error { get; private set; }
    public bool Finished => writer.IsCompleted;

    public LinuxWavRecorder(string path, int rate, int channels)
    {
        if (rate is < 8000 or > 192000 || channels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(rate));
        this.rate = rate;
        this.channels = channels;
        Path = path;
        // Open synchronously so permissions/storage errors are reported before announcing recording.
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        writer = Task.Run(() => Write(stream));
    }

    public void Submit(float[] samples, int sampleRate, int channelCount)
    {
        if (sampleRate != rate || channelCount != channels)
        { Error = "Audio format changed; recording finished."; queue.Writer.TryComplete(); return; }
        if (!queue.Writer.TryWrite(samples)) Interlocked.Increment(ref dropped);
    }

    private async Task Write(FileStream stream)
    {
        using (stream)
        using (var binary = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            uint bytes = 0;
            try
            {
                Header(binary, 0);
                await foreach (float[] block in queue.Reader.ReadAllAsync())
                {
                    if (bytes + block.Length * 2L > 128 * 1024 * 1024)
                    { Error = "Recording reached its 128 MiB limit."; break; }
                    foreach (float sample in block) binary.Write((short)Math.Clamp((int)(sample * 32767), -32768, 32767));
                    bytes += (uint)block.Length * 2;
                }
                stream.Position = 0;
                Header(binary, bytes);
                stream.Flush(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = ex.GetType().Name; }
            finally { queue.Writer.TryComplete(); }
        }
    }

    private void Header(BinaryWriter binary, uint length)
    {
        binary.Write(Encoding.ASCII.GetBytes("RIFF")); binary.Write(length + 36);
        binary.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); binary.Write(16);
        binary.Write((short)1); binary.Write((short)channels); binary.Write(rate);
        binary.Write(rate * channels * 2); binary.Write((short)(channels * 2)); binary.Write((short)16);
        binary.Write(Encoding.ASCII.GetBytes("data")); binary.Write(length);
    }

    public void Dispose() { queue.Writer.TryComplete(); writer.GetAwaiter().GetResult(); }
}
