using System;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

/// <summary>
/// Ordered, reliable, bounded packet transport. Networking never calls into an
/// emulated machine: the session owner polls packets and owns all core access.
/// </summary>
public interface IOnlineLinkTransport : IDisposable, IAsyncDisposable
{
    Task Ready { get; }
    Task Completion { get; }
    bool Connected { get; }
    Exception? Fault { get; }
    bool TryReceive(out byte[] packet);
    void Send(ReadOnlySpan<byte> packet);
}
