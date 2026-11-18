using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

// Jalan pesan lewat koneksi TCP di komputer sendiri (127.0.0.1), untuk tes 3 salinan game di satu Mac.
// Bentuknya sama dengan jalan Game Center: host menerima 2 client, client hanya bicara dengan host.
//
// Tanpa thread: Pump() dipanggil tiap frame dari main thread (oleh LocalNetPanel), jadi DataReceived
// otomatis berjalan di main thread seperti yang diminta INetTransport.
public class LocalTransport : INetTransport, IDisposable
{
    public const int DefaultPort = 7777;
    public const int MaxClients = PigmentAssignment.PlayerCount - 1;

    private class Peer
    {
        public TcpClient tcp;
        public NetworkStream stream;
        public readonly FrameReader reader = new FrameReader();
        public int index;
    }

    private readonly TcpListener listener;           // host saja
    private readonly Peer[] clients = new Peer[PigmentAssignment.PlayerCount];   // host: isi mulai index 1
    private Peer toHost;                             // client saja
    private bool disposed;

    public bool IsHost { get; }
    public int LocalPlayerIndex { get; private set; }    // client: -1 sampai host mengirim Welcome

    public event Action<int, byte[]> DataReceived;
    public event Action<int> PlayerJoined;           // host: client dengan index ini masuk
    public event Action<int> PlayerLeft;             // host: client dengan index ini terputus
    public event Action Disconnected;                // client: koneksi ke host putus / ditolak
    public event Action<int> PeerLost;               // sama dengan PlayerLeft / Disconnected, dalam bentuk INetTransport

    // Host: jumlah client yang sudah terhubung. Client: 1 kalau terhubung ke host.
    public int ConnectedCount
    {
        get
        {
            if (!IsHost) return toHost != null ? 1 : 0;
            int n = 0;
            for (int i = 1; i < clients.Length; i++) if (clients[i] != null) n++;
            return n;
        }
    }

    // Client sudah diterima host dan tahu index-nya. Host selalu true.
    // Host: dirinya + client yang sudah terhubung. Client tidak tahu jumlah pasti, 3 = ukuran penuh.
    public int PlayerCount => IsHost ? ConnectedCount + 1 : MaxClients + 1;

    public bool IsConnected => IsHost || (toHost != null && LocalPlayerIndex >= 0);

    public int Port => IsHost ? ((IPEndPoint)listener.LocalEndpoint).Port : -1;

    private LocalTransport(bool isHost, TcpListener listener, Peer toHost)
    {
        IsHost = isHost;
        this.listener = listener;
        this.toHost = toHost;
        LocalPlayerIndex = isHost ? 0 : -1;
    }

    // port 0 = pilih port bebas sendiri (dipakai test), lihat Port.
    public static LocalTransport StartHost(int port = DefaultPort)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);   // hanya bisa dicapai dari komputer ini
        listener.Start();
        return new LocalTransport(true, listener, null);
    }

    // Melempar exception kalau host tidak ada (SocketException).
    public static LocalTransport StartClient(string address = "127.0.0.1", int port = DefaultPort)
    {
        var tcp = new TcpClient { NoDelay = true };
        tcp.Connect(address, port);
        return new LocalTransport(false, null, new Peer { tcp = tcp, stream = tcp.GetStream() });
    }

    // ---------- Kirim ----------

    public void SendToHost(byte[] data)
    {
        if (IsHost) return;   // GameNet sudah menangani host -> host
        Write(toHost, Frame.Data, data);
    }

    public void SendToAll(byte[] data)
    {
        if (!IsHost)
        {
            Debug.LogWarning("[NET] Hanya host yang boleh mengirim ke semua pemain");
            return;
        }
        for (int i = 1; i < clients.Length; i++) Write(clients[i], Frame.Data, data);
    }

    public void SendTo(int playerIndex, byte[] data)
    {
        if (IsHost)
        {
            if (playerIndex > 0 && playerIndex < clients.Length) Write(clients[playerIndex], Frame.Data, data);
            return;
        }

        if (playerIndex == 0) Write(toHost, Frame.Data, data);
        else Debug.LogWarning("[NET] Client hanya boleh mengirim ke host");
    }

    private void Write(Peer peer, byte kind, byte[] payload)
    {
        if (peer == null) return;
        try
        {
            byte[] frame = Frame.Build(kind, payload);
            peer.stream.Write(frame, 0, frame.Length);
        }
        catch (Exception)
        {
            // Terputus: dibersihkan di Pump berikutnya lewat deteksi koneksi mati.
        }
    }

    // ---------- Terima (panggil tiap frame di main thread) ----------

    public void Pump()
    {
        if (disposed) return;

        if (IsHost)
        {
            AcceptPending();
            for (int i = 1; i < clients.Length; i++) PumpPeer(clients[i]);
        }
        else if (toHost != null)
        {
            PumpPeer(toHost);
        }
    }

    private void AcceptPending()
    {
        while (listener.Pending())
        {
            TcpClient tcp = listener.AcceptTcpClient();
            tcp.NoDelay = true;

            int index = FreeIndex();
            if (index < 0)
            {
                tcp.Close();   // lobby penuh
                continue;
            }

            var peer = new Peer { tcp = tcp, stream = tcp.GetStream(), index = index };
            clients[index] = peer;
            Write(peer, Frame.Welcome, new[] { (byte)index });
            PlayerJoined?.Invoke(index);
        }
    }

    private int FreeIndex()
    {
        for (int i = 1; i < clients.Length; i++) if (clients[i] == null) return i;
        return -1;
    }

    private void PumpPeer(Peer peer)
    {
        if (peer == null) return;

        bool closed = false;
        try
        {
            var chunk = new byte[4096];
            while (peer.stream.DataAvailable)
            {
                int n = peer.stream.Read(chunk, 0, chunk.Length);
                if (n == 0) { closed = true; break; }
                peer.reader.Append(chunk, 0, n);
            }

            // Tidak ada data tapi socket terbaca = lawan sudah menutup koneksi.
            if (!closed && !peer.stream.DataAvailable && peer.tcp.Client.Poll(0, SelectMode.SelectRead) && peer.tcp.Client.Available == 0)
                closed = true;

            while (peer.reader.TryRead(out byte kind, out byte[] payload))
            {
                Handle(peer, kind, payload);
                if (disposed) return;
            }
        }
        catch (Exception e) when (e is System.IO.IOException || e is SocketException || e is ObjectDisposedException || e is FormatException)
        {
            closed = true;
        }

        if (closed) Drop(peer);
    }

    private void Handle(Peer peer, byte kind, byte[] payload)
    {
        if (IsHost)
        {
            if (kind == Frame.Data) DataReceived?.Invoke(peer.index, payload);   // index dari koneksi, bukan dari isi pesan
            return;
        }

        if (kind == Frame.Welcome && payload.Length == 1) LocalPlayerIndex = payload[0];
        else if (kind == Frame.Data) DataReceived?.Invoke(0, payload);
    }

    private void Drop(Peer peer)
    {
        try { peer.tcp.Close(); } catch (Exception) { }

        if (IsHost)
        {
            if (clients[peer.index] != peer) return;
            clients[peer.index] = null;
            PlayerLeft?.Invoke(peer.index);
            PeerLost?.Invoke(peer.index);
        }
        else
        {
            if (toHost != peer) return;
            toHost = null;
            LocalPlayerIndex = -1;
            Disconnected?.Invoke();
            PeerLost?.Invoke(0);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        if (IsHost)
        {
            for (int i = 1; i < clients.Length; i++)
            {
                try { clients[i]?.tcp.Close(); } catch (Exception) { }
                clients[i] = null;
            }
            try { listener.Stop(); } catch (Exception) { }
        }
        else
        {
            try { toHost?.tcp.Close(); } catch (Exception) { }
            toHost = null;
        }
    }
}
