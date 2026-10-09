using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace StickMayhem {
    public sealed class Message {
        public string Kind, Fingerprint, Text;
        public int Protocol = 1;
        public MatchOptions Options;
        public InputFrame Input;
        public MatchState State;
    }
    public static class Wire {
        public const int Port = 42673, MaxFrame = 196608;
        public static bool Private(IPAddress ip) {
            if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            return b[0] == 127 || b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 169 && b[1] == 254;
        }
        public static IPEndPoint Endpoint(string text, int fallback) {
            string[] parts = text.Trim().Split(':'); IPAddress ip; int port = fallback;
            if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out ip) || !Private(ip) ||
                parts.Length == 2 && !Int32.TryParse(parts[1], out port) || port < 1024 || port > 65535)
                throw new InvalidOperationException("请输入局域网 IPv4 地址，如 192.168.1.8:42673。");
            return new IPEndPoint(ip, port);
        }
        public static string[] Addresses(int port) {
            try {
                var values = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => Private(a) && !IPAddress.IsLoopback(a)).Select(a => a + ":" + port).Distinct().ToArray();
                return values.Length > 0 ? values : new[] { "127.0.0.1:" + port };
            } catch (SocketException) { return new[] { "127.0.0.1:" + port }; }
        }
        public static string Fingerprint() {
            using (var sha = SHA256.Create()) return Rules.Fingerprint() + ":" + Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(typeof(Wire).Assembly.Location)));
        }
        public static void Write(Stream stream, string text) {
            byte[] data = Encoding.UTF8.GetBytes(text);
            if (data.Length < 1 || data.Length > MaxFrame) throw new InvalidDataException("消息过长。");
            byte[] size = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length)); stream.Write(size, 0, 4); stream.Write(data, 0, data.Length);
        }
        static void Exact(Stream stream, byte[] data) {
            int offset = 0;
            while (offset < data.Length) { int n = stream.Read(data, offset, data.Length - offset); if (n == 0) throw new EndOfStreamException("对方已离开。"); offset += n; }
        }
        public static string Read(Stream stream) {
            byte[] size = new byte[4]; Exact(stream, size); int n = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(size, 0));
            if (n < 1 || n > MaxFrame) throw new InvalidDataException("消息大小无效。");
            byte[] data = new byte[n]; Exact(stream, data); return new UTF8Encoding(false, true).GetString(data);
        }
        static bool Number(double x, double min, double max) { return Rules.Finite(x) && x >= min && x <= max; }
        public static bool Valid(MatchState s) {
            if (s == null || String.IsNullOrEmpty(s.Session) || s.Session.Length > 64 || s.Tick < 0 || s.Map < 0 || s.Map >= Rules.Arenas.Length ||
                s.Winner < -2 || s.Winner > 1 || s.Players == null || s.Players.Length != 2 ||
                s.Shots == null || s.Shots.Count > 170 || s.Grenades == null || s.Grenades.Count > 8 || s.Crates == null || s.Crates.Count > 4 ||
                s.Powerups == null || s.Powerups.Count > 3 || s.Bursts == null || s.Bursts.Count > 16 || s.Log == null || s.Log.Count > 60 ||
                !Number(s.NextCrate, -1, 11) || !Number(s.NextPower, -1, 11)) return false;
            for (int i = 0; i < 2; i++) {
                var p = s.Players[i];
                if (p == null || p.Seat != i || p.Lives < 0 || p.Lives > 10 || p.Starter < 0 || p.Starter >= Rules.StarterCount || p.Weapon < 0 || p.Weapon >= Rules.Weapons.Length ||
                    p.Magazine < 0 || p.Magazine > 180 || p.Reserve < -1 || p.Reserve > 180 || p.Jumps < 0 || p.Jumps > 2 || p.Bombs < 0 || p.Bombs > 3 ||
                    !Number(p.X, -200, 1200) || !Number(p.Y, -800, 740) || !Number(p.Vx, -1300, 1300) || !Number(p.Vy, -1200, 1600) ||
                    !Number(p.AimX, -300, 1300) || !Number(p.AimY, -250, 850) || !Number(p.Impact, 0, 180) ||
                    !Number(p.Cooldown, 0, 1.3) || !Number(p.ReloadTime, 0, 3.1) || !Number(p.BombCooldown, 0, .8) || !Number(p.Invulnerable, 0, 2) ||
                    !Number(p.Respawn, 0, 1.2) || !Number(p.DropTime, 0, .3) || !Number(p.HitStun, 0, .2) ||
                    !Number(p.ShieldTime, 0, 6.1) || !Number(p.JetFuel, 0, 3.1) || !Number(p.InvisibleTime, 0, 5.1) || !Number(p.GatlingTime, 0, 30.1) ||
                    !Number(p.PopupTime, 0, 1.7) || p.Popup == null || p.Popup.Length > 30) return false;
            }
            foreach (var shot in s.Shots) if (shot == null || shot.Owner < 0 || shot.Owner > 1 || !Number(shot.X, -160, 1160) || !Number(shot.Y, -230, 750) ||
                !Number(shot.Vx, -2400, 2400) || !Number(shot.Vy, -2400, 2400) || !Number(shot.Push, 0, 800) || !Number(shot.Time, 0, 2.3)) return false;
            foreach (var g in s.Grenades) if (g == null || g.Owner < 0 || g.Owner > 1 || !Number(g.X, -200, 1200) || !Number(g.Y, -800, 800) ||
                !Number(g.Vx, -1000, 1000) || !Number(g.Vy, -900, 1800) || !Number(g.Fuse, 0, 1.4)) return false;
            foreach (var c in s.Crates) if (c == null || c.Weapon < 0 || c.Weapon >= Rules.Weapons.Length || !Number(c.X, 0, 1000) || !Number(c.Y, -40, 740) ||
                !Number(c.Age, 0, 25) || !Number(c.Vy, 0, 120)) return false;
            foreach (var p in s.Powerups) if (p == null || p.Kind < 0 || p.Kind > 2 || !Number(p.X, 0, 1000) || !Number(p.Y, -40, 740) ||
                !Number(p.Age, 0, 19) || !Number(p.Vy, 0, 110)) return false;
            foreach (var b in s.Bursts) if (b == null || !Number(b.X, -200, 1200) || !Number(b.Y, -850, 850) || !Number(b.Radius, 0, 200) || !Number(b.Time, 0, .3)) return false;
            return s.Log.All(l => l != null && l.Length < 180);
        }
    }
    public sealed class Peer : IDisposable {
        public event Action Connected;
        public event Action<Message> Received;
        public event Action<string> Disconnected;
        readonly BlockingCollection<string> outgoing = new BlockingCollection<string>(90);
        TcpListener listener; TcpClient client; Timer heartbeat;
        int disposed;
        public bool ConnectedNow { get; private set; }
        public int ListeningPort { get; private set; }
        public bool Closed { get { return Interlocked.CompareExchange(ref disposed, 0, 0) != 0; } }
        public void Host(int port) {
            listener = new TcpListener(IPAddress.Any, port); listener.Start(1); ListeningPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            Run(delegate { client = listener.AcceptTcpClient(); listener.Stop();
                if (!Wire.Private(((IPEndPoint)client.Client.RemoteEndPoint).Address)) throw new InvalidDataException("仅支持局域网连接。"); Connection(); });
        }
        public void Join(IPEndPoint endpoint) {
            Run(delegate {
                var next = new TcpClient(); client = next; if (Closed) { next.Close(); return; }
                var pending = next.BeginConnect(endpoint.Address, endpoint.Port, null, null);
                using (pending.AsyncWaitHandle) {
                    if (!pending.AsyncWaitHandle.WaitOne(5000)) throw new IOException("连接超时，请检查地址和防火墙。");
                    next.EndConnect(pending);
                }
                Connection();
            });
        }
        void Run(Action action) {
            new Thread(delegate() { try { action(); } catch (Exception ex) {
                if (!Closed) { var handler = Disconnected; Dispose(); if (handler != null) handler(ex is SocketException ? "连接已断开。" : ex.Message); }
            } }) { IsBackground = true }.Start();
        }
        void Connection() {
            if (Closed) { client.Close(); return; }
            client.NoDelay = true; client.ReceiveTimeout = 8000; client.SendTimeout = 2000;
            var stream = client.GetStream(); ConnectedNow = true;
            Run(delegate { foreach (string text in outgoing.GetConsumingEnumerable()) { if (Closed) break; Wire.Write(stream, text); } });
            heartbeat = new Timer(delegate { if (!Closed) Send(new Message { Kind = "ping" }); }, null, 1500, 1500);
            var ready = Connected; if (ready != null) ready();
            while (!Closed) {
                var m = Rules.Serializer().Deserialize<Message>(Wire.Read(stream)); if (m == null) throw new InvalidDataException("消息无效。");
                if (m.Kind == "ping") continue; var receive = Received; if (receive != null) receive(m);
            }
        }
        public bool Send(Message m) {
            if (Closed) return false; string text = Rules.Serializer().Serialize(m);
            if (Encoding.UTF8.GetByteCount(text) > Wire.MaxFrame) return false;
            try { return outgoing.TryAdd(text); } catch (InvalidOperationException) { return false; }
        }
        public void Dispose() {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            ConnectedNow = false; if (heartbeat != null) heartbeat.Dispose(); if (listener != null) listener.Stop(); if (client != null) client.Close();
            outgoing.CompleteAdding();
        }
    }
}
