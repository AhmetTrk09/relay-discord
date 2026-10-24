using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Relay
{
    public sealed class TlsSplitProxy : IDisposable
    {
        public static readonly Dictionary<string, string> ProxyHostMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "discord.com", "162.159.135.232" },
            { "www.discord.com", "162.159.136.232" },
            { "ptb.discord.com", "162.159.137.232" },
            { "canary.discord.com", "162.159.138.232" },
            { "updates.discord.com", "162.159.138.232" },
            { "status.discord.com", "162.159.138.232" },
            { "gateway.discord.gg", "162.159.134.234" },
            { "discord.gg", "162.159.130.234" },
            { "discordapp.com", "162.159.133.233" },
            { "cdn.discordapp.com", "162.159.134.233" },
            { "discordcdn.com", "162.159.134.233" },
            { "media.discordapp.net", "162.159.130.232" },
            { "images-ext-1.discordapp.net", "162.159.128.232" },
            { "images-ext-2.discordapp.net", "162.159.134.232" },
            { "discordapp.net", "162.159.129.232" },
            { "dl.discordapp.net", "104.18.52.172" }
        };

        public static readonly Dictionary<string, string> DirectHostMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "stable.dl2.discordapp.net", "34.126.226.51" },
            { "discord.media", "162.159.129.235" },
            { "latency.discord.media", "162.159.138.234" }
        };

        private readonly object _socketLock = new object();
        private readonly HashSet<Socket> _sockets = new HashSet<Socket>();
        private int _generation;
        private sealed class ClientSession
        {
            public Socket Socket;
            public int Generation;
        }
        private Socket _listener;
        private readonly int _port;
        public TlsSplitProxy() : this(443) { }
        internal TlsSplitProxy(int port) { _port = port; }
        internal int ListeningPort { get { return _listener == null ? 0 : ((IPEndPoint)_listener.LocalEndPoint).Port; } }
        private volatile bool _running;
        private int _totalSessions;
        private int _activeSessions;
        private long _totalBytes;
        private string _lastHost = "-";
        private readonly Dictionary<string, DateTime> _lastLogPerHost = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public event Action<string> LogMessage;

        public bool IsRunning { get { return _running; } }
        public int TotalSessions { get { return _totalSessions; } }
        public int ActiveSessions { get { return _activeSessions; } }
        public long TotalBytes { get { return Interlocked.Read(ref _totalBytes); } }
        public string LastHost { get { return _lastHost; } }

        public bool Start()
        {
            if (_running) return true;

            try
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.ExclusiveAddressUse = true;
                _listener.Bind(new IPEndPoint(IPAddress.Loopback, _port));
                _listener.Listen(128);
                _running = true;

                int generation = Interlocked.Increment(ref _generation);
                Socket listener = _listener;
                Thread acceptThread = new Thread(delegate() { AcceptLoop(listener, generation); });
                acceptThread.IsBackground = true;
                acceptThread.Name = "TlsSplitAccept";
                acceptThread.Start();

                EmitLog("TLS-Split katmanı 127.0.0.1:443 üzerinde dinlemede.");
                return true;
            }
            catch (Exception ex)
            {
                Stop();
                EmitLog("127.0.0.1:443 başlatılamadı: " + ex.Message);
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            Interlocked.Increment(ref _generation);
            lock (_socketLock)
            {
                foreach (Socket socket in _sockets) { try { socket.Close(); } catch (SocketException) { } }
                _sockets.Clear();
            }
            Socket s = _listener;
            _listener = null;
            if (s != null)
            {
                try { s.Close(); } catch { }
            }
        }

        private void AcceptLoop(Socket listener, int generation)
        {
            while (_running && generation == _generation)
            {
                Socket client = null;
                try
                {
                    client = listener.Accept();
                    if (!TrackSocket(client, generation)) break;
                    ThreadPool.QueueUserWorkItem(HandleClient, new ClientSession { Socket = client, Generation = generation });
                }
                catch
                {
                    if (client != null)
                    {
                        try { client.Close(); } catch { }
                    }
                    if (!_running || generation != _generation) break;
                }
            }
        }

        private void HandleClient(object state)
        {
            ClientSession session = (ClientSession)state;
            Socket client = session.Socket;
            Socket upstream = null;
            Interlocked.Increment(ref _activeSessions);
            Interlocked.Increment(ref _totalSessions);

            try
            {
                if (!_running || session.Generation != _generation || _activeSessions > 128)
                {
                    client.Close();
                    return;
                }

                client.ReceiveTimeout = 8000;
                client.NoDelay = true;

                byte[] firstChunk = ReadFirstTlsRecord(client);

                string sni = ExtractSni(firstChunk);
                if (string.IsNullOrEmpty(sni)) return;
                string targetIp = "162.159.135.232";
                if (!string.IsNullOrEmpty(sni))
                {
                    string mapped;
                    if (!ProxyHostMap.TryGetValue(sni, out mapped) && !IsAllowedDiscordDomain(sni))
                    {
                        client.Close();
                        return;
                    }
                    if (!string.IsNullOrEmpty(mapped))
                        targetIp = mapped;
                    _lastHost = sni;
                    LogHostOncePerWindow(sni, targetIp);
                }

                upstream = ConnectUpstreamWithFailover(targetIp);
                if (!TrackSocket(upstream, session.Generation)) return;

                byte[] fragmented = FragmentTlsRecordSni(firstChunk);
                SendAll(upstream, fragmented, fragmented.Length);
                Interlocked.Add(ref _totalBytes, fragmented.Length);

                client.ReceiveTimeout = 0;
                upstream.ReceiveTimeout = 0;

                Thread upstreamToClient = new Thread(() => Pipe(upstream, client, session.Generation));
                upstreamToClient.IsBackground = true;
                upstreamToClient.Start();

                Pipe(client, upstream, session.Generation);
                upstreamToClient.Join(200);
            }
            catch
            {
                try { client.Close(); } catch { }
                if (upstream != null)
                {
                    try { upstream.Close(); } catch { }
                }
            }
            finally
            {
                lock (_socketLock) { _sockets.Remove(client); if (upstream != null) _sockets.Remove(upstream); }
                try { client.Close(); } catch (SocketException) { }
                if (upstream != null) { try { upstream.Close(); } catch (SocketException) { } }
                Interlocked.Decrement(ref _activeSessions);
            }
        }

        private static Socket ConnectUpstreamWithFailover(string primaryIp)
        {
            string[] candidates = new string[]
            {
                primaryIp,
                "162.159.135.232",
                "162.159.136.232",
                "162.159.137.232",
                "162.159.138.232"
            };

            Exception lastEx = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string ip = candidates[i];
                if (i > 0 && string.Equals(ip, primaryIp, StringComparison.Ordinal))
                    continue;

                Socket s = null;
                try
                {
                    s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    s.NoDelay = true;
                    IAsyncResult ar = s.BeginConnect(IPAddress.Parse(ip), 443, null, null);
                    using (WaitHandle wait = ar.AsyncWaitHandle)
                    {
                        if (!wait.WaitOne(3500)) throw new SocketException((int)SocketError.TimedOut);
                        s.EndConnect(ar);
                    }
                    return s;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (s != null)
                    {
                        try { s.Close(); } catch { }
                    }
                }
            }

            throw lastEx ?? new SocketException((int)SocketError.TimedOut);
        }

        private void Pipe(Socket src, Socket dst, int generation)
        {
            byte[] buf = new byte[32768];
            try
            {
                while (_running && generation == _generation)
                {
                    int n = src.Receive(buf);
                    if (n <= 0) break;
                    SendAll(dst, buf, n);
                    Interlocked.Add(ref _totalBytes, n);
                }
            }
            catch
            {
            }
            finally
            {
                try { src.Shutdown(SocketShutdown.Both); } catch { }
                try { dst.Shutdown(SocketShutdown.Both); } catch { }
                try { src.Close(); } catch { }
                try { dst.Close(); } catch { }
            }
        }

        private bool TrackSocket(Socket socket, int generation)
        {
            lock (_socketLock)
            {
                if (!_running || generation != _generation) { socket.Close(); return false; }
                _sockets.Add(socket);
                return true;
            }
        }

        internal static byte[] ReadFirstTlsRecord(Socket socket)
        {
            byte[] header = new byte[5];
            ReceiveExactly(socket, header, 0, header.Length);
            int length = (header[3] << 8) | header[4];
            if (header[0] != 0x16 || header[1] != 3 || length < 4 || length > 18432)
                throw new IOException("Geçersiz TLS başlangıcı.");
            byte[] record = new byte[5 + length];
            Buffer.BlockCopy(header, 0, record, 0, 5);
            ReceiveExactly(socket, record, 5, length);
            return record;
        }

        private static void ReceiveExactly(Socket socket, byte[] data, int offset, int count)
        {
            while (count > 0)
            {
                int received = socket.Receive(data, offset, count, SocketFlags.None);
                if (received == 0) throw new IOException("TLS başlangıcı tamamlanmadan bağlantı kapandı.");
                offset += received;
                count -= received;
            }
        }

        internal static void SendAll(Socket socket, byte[] data, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int sent = socket.Send(data, offset, count - offset, SocketFlags.None);
                if (sent == 0) throw new IOException("Veri aktarılırken bağlantı kapandı.");
                offset += sent;
            }
        }

        private void LogHostOncePerWindow(string sni, string ip)
        {
            lock (_lastLogPerHost)
            {
                DateTime last;
                if (_lastLogPerHost.TryGetValue(sni, out last))
                {
                    if ((DateTime.UtcNow - last).TotalSeconds < 6)
                        return;
                }
                _lastLogPerHost[sni] = DateTime.UtcNow;
            }
            EmitLog("TLS-Split -> " + sni + " (" + ip + ")");
        }

        private static string ExtractSni(byte[] data)
        {
            try
            {
                if (data.Length < 43 || data[0] != 0x16 || data[5] != 0x01)
                    return null;

                int pos = 9 + 2 + 32;
                if (pos >= data.Length) return null;
                int sidLen = data[pos];
                pos += 1 + sidLen;

                if (pos + 2 > data.Length) return null;
                int csLen = (data[pos] << 8) | data[pos + 1];
                pos += 2 + csLen;

                if (pos + 1 > data.Length) return null;
                int compLen = data[pos];
                pos += 1 + compLen;

                if (pos + 2 > data.Length) return null;
                int extTotal = (data[pos] << 8) | data[pos + 1];
                pos += 2;

                int end = Math.Min(data.Length, pos + extTotal);
                while (pos + 4 <= end)
                {
                    int extType = (data[pos] << 8) | data[pos + 1];
                    int extLen = (data[pos + 2] << 8) | data[pos + 3];
                    pos += 4;
                    if (extType == 0x0000 && pos + 5 <= end)
                    {
                        int nameLen = (data[pos + 3] << 8) | data[pos + 4];
                        if (pos + 5 + nameLen <= end)
                            return Encoding.ASCII.GetString(data, pos + 5, nameLen).ToLowerInvariant();
                    }
                    pos += extLen;
                }
            }
            catch
            {
            }
            return null;
        }

        public static byte[] FragmentTlsRecordSni(byte[] data)
        {
            if (data.Length > 20 && data[0] == 0x16 && data[1] == 0x03 && data[5] == 0x01)
            {
                int recLen = (data[3] << 8) | data[4];
                if (data.Length >= 5 + recLen && recLen > 16)
                {
                    int splitPos = FindAsciiCaseInsensitive(data, 5, recLen, "discord");
                    int remLen = data.Length - (5 + recLen);

                    if (splitPos > 9)
                    {
                        int relSplit = (splitPos - 5) + 3; // 'dis' | 'cord'
                        int p1Len = 4;
                        int p2Len = relSplit - 4;
                        int p3Len = recLen - relSplit;

                        byte[] outBuf = new byte[5 + p1Len + 5 + p2Len + 5 + p3Len + remLen];
                        int o = 0;

                        // Record 1
                        outBuf[o++] = data[0]; outBuf[o++] = data[1]; outBuf[o++] = data[2];
                        outBuf[o++] = 0; outBuf[o++] = (byte)p1Len;
                        Buffer.BlockCopy(data, 5, outBuf, o, p1Len);
                        o += p1Len;

                        // Record 2
                        outBuf[o++] = data[0]; outBuf[o++] = data[1]; outBuf[o++] = data[2];
                        outBuf[o++] = (byte)(p2Len >> 8); outBuf[o++] = (byte)(p2Len & 0xFF);
                        Buffer.BlockCopy(data, 5 + p1Len, outBuf, o, p2Len);
                        o += p2Len;

                        // Record 3
                        outBuf[o++] = data[0]; outBuf[o++] = data[1]; outBuf[o++] = data[2];
                        outBuf[o++] = (byte)(p3Len >> 8); outBuf[o++] = (byte)(p3Len & 0xFF);
                        Buffer.BlockCopy(data, 5 + relSplit, outBuf, o, p3Len);
                        o += p3Len;

                        if (remLen > 0)
                            Buffer.BlockCopy(data, 5 + recLen, outBuf, o, remLen);

                        return outBuf;
                    }
                    else
                    {
                        int p1Len = 4;
                        int p2Len = recLen - 4;
                        byte[] outBuf = new byte[5 + p1Len + 5 + p2Len + remLen];
                        int o = 0;

                        outBuf[o++] = data[0]; outBuf[o++] = data[1]; outBuf[o++] = data[2];
                        outBuf[o++] = 0; outBuf[o++] = (byte)p1Len;
                        Buffer.BlockCopy(data, 5, outBuf, o, p1Len);
                        o += p1Len;

                        outBuf[o++] = data[0]; outBuf[o++] = data[1]; outBuf[o++] = data[2];
                        outBuf[o++] = (byte)(p2Len >> 8); outBuf[o++] = (byte)(p2Len & 0xFF);
                        Buffer.BlockCopy(data, 5 + p1Len, outBuf, o, p2Len);
                        o += p2Len;

                        if (remLen > 0)
                            Buffer.BlockCopy(data, 5 + recLen, outBuf, o, remLen);

                        return outBuf;
                    }
                }
            }
            return data;
        }

        private static int FindAsciiCaseInsensitive(byte[] buf, int offset, int count, string pattern)
        {
            int end = offset + count - pattern.Length;
            for (int i = offset; i <= end; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    char c = (char)buf[i + j];
                    if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                    if (c != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static bool IsAllowedDiscordDomain(string sni)
        {
            return sni.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase) ||
                   sni.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase) ||
                   sni.EndsWith(".discordapp.net", StringComparison.OrdinalIgnoreCase) ||
                   sni.EndsWith(".discord.gg", StringComparison.OrdinalIgnoreCase) ||
                   sni.EndsWith(".discord.media", StringComparison.OrdinalIgnoreCase) ||
                   sni.EndsWith(".discordcdn.com", StringComparison.OrdinalIgnoreCase);
        }

        private void EmitLog(string msg)
        {
            Action<string> h = LogMessage;
            if (h != null) h(msg);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
