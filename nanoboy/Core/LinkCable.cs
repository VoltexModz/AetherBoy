using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace nanoboy.Core
{
    public class LinkCable : IDisposable
    {
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private Thread listenThread;
        private CancellationTokenSource cts;

        public bool IsConnected => client != null && client.Connected;
        public bool IsServer { get; private set; }

        public event Action<byte> ByteReceived;
        public event Action Connected;
        public event Action Disconnected;

        public void StartServer(int port = 8765)
        {
            Disconnect();
            IsServer = true;
            cts = new CancellationTokenSource();

            listenThread = new Thread(() =>
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (listener.Pending())
                        {
                            client = listener.AcceptTcpClient();
                            stream = client.GetStream();
                            Connected?.Invoke();
                            ReadLoop(cts.Token);
                            break;
                        }
                        Thread.Sleep(100);
                    }
                }
                catch { }
            })
            { IsBackground = true };
            listenThread.Start();
        }

        public bool ConnectClient(string ipAddress, int port = 8765)
        {
            Disconnect();
            IsServer = false;
            cts = new CancellationTokenSource();

            try
            {
                client = new TcpClient();
                client.Connect(ipAddress, port);
                stream = client.GetStream();
                Connected?.Invoke();

                listenThread = new Thread(() => ReadLoop(cts.Token)) { IsBackground = true };
                listenThread.Start();
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        private void ReadLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && stream != null && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int b = stream.ReadByte();
                        if (b != -1)
                        {
                            ByteReceived?.Invoke((byte)b);
                        }
                    }
                    Thread.Sleep(5);
                }
            }
            catch { }
            finally
            {
                Disconnected?.Invoke();
            }
        }

        public void SendByte(byte b)
        {
            if (IsConnected && stream != null)
            {
                try
                {
                    stream.WriteByte(b);
                    stream.Flush();
                }
                catch { }
            }
        }

        public void Disconnect()
        {
            cts?.Cancel();
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            try { listener?.Stop(); } catch { }

            stream = null;
            client = null;
            listener = null;
            cts = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
