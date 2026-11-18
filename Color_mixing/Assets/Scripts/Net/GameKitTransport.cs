using System;
using System.Collections.Generic;
using UnityEngine;

// Roster pemain yang bisa dikunci selama game berjalan.
public interface IRosterTransport
{
    IReadOnlyList<string> Roster { get; }
    void Freeze(string[] names = null);   // kunci daftar pemain (null = pakai daftar lobby saat ini)
    void Unfreeze();
}

// Jalan pesan lewat match Game Center (dipinjam dari LobbyService). Cara kerjanya:
//  - GKMatch hanya dipakai untuk "kirim ke semua"; tujuan sebenarnya ada di header tiap bingkai dan disaring penerima.
//  - Pengirim dikenali dari nama pemain GameKit -> index (host 0, client 1 dan 2, sama di semua device).
//  - Setiap bingkai bernomor (sesi + urutan): GameKit kadang mengirim pesan yang sama dua kali lewat dua event,
//    dan nomor itu membuang yang dobel sebelum sampai ke game.
//  - Topologi bintang: client hanya mau mendengar host; kiriman dari client lain dibuang.
//
// Bingkai: [0x01][tujuan: 1 byte, 0xFF = semua][sesi: int32][urutan: int32][isi NetMessage].
// Pesan lobby diawali '{' (JSON), jadi 0x01 tidak pernah bentrok dengan pesan lobby.
public class GameKitTransport : INetTransport, IRosterTransport, IDisposable
{
    public const byte FrameMarker = 0x01;
    private const byte ToAll = 0xFF;
    private const int HeaderSize = 10;

    private readonly IGameLink link;
    private readonly int sessionId = UnityEngine.Random.Range(1, int.MaxValue);
    private readonly Dictionary<string, (int session, int seq)> lastSeen = new Dictionary<string, (int, int)>();
    private int sendSeq;
    private string[] frozen;
    private bool disposed;

    public GameKitTransport(IGameLink link)
    {
        this.link = link ?? throw new ArgumentNullException(nameof(link));
        link.RawReceived += OnRaw;
        link.PlayerGone += OnGone;
    }

    // Pasang transport ini sebagai sesi game kalau belum ada (dipanggil saat masuk lobby di device).
    public static void EnsureSession(IGameLink link)
    {
        if (GameNet.Instance != null && GameNet.Instance.Transport is GameKitTransport) return;
        GameNet.Begin(new GameKitTransport(link));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        link.RawReceived -= OnRaw;
        link.PlayerGone -= OnGone;
    }

    // ---------- Daftar pemain ----------

    public IReadOnlyList<string> Roster => frozen ?? link.CurrentRoster();

    public void Freeze(string[] names = null)
    {
        var source = names != null && names.Length > 0 ? names : new List<string>(link.CurrentRoster()).ToArray();
        frozen = (string[])source.Clone();
    }

    public void Unfreeze() => frozen = null;

    private int IndexOf(string name)
    {
        var roster = Roster;
        for (int i = 0; i < roster.Count; i++) if (roster[i] == name) return i;
        return -1;
    }

    // ---------- INetTransport ----------

    public bool IsHost => link.IsHost;

    public int LocalPlayerIndex
    {
        get
        {
            if (IsHost) return 0;
            int index = IndexOf(link.LocalName);
            return index >= 0 ? index : 1;   // belum tercatat di daftar: client pertama
        }
    }

    public int PlayerCount => Math.Max(1, Roster.Count);

    public event Action<int, byte[]> DataReceived;
    public event Action<int> PeerLost;

    public void SendToHost(byte[] data)
    {
        if (IsHost) return;   // GameNet sudah menangani host -> host
        Send(0, data);
    }

    public void SendToAll(byte[] data) => Send(ToAll, data);

    public void SendTo(int playerIndex, byte[] data)
    {
        if (playerIndex < 0 || playerIndex >= ToAll) return;
        Send((byte)playerIndex, data);
    }

    private void Send(byte target, byte[] data)
    {
        if (disposed || data == null) return;

        var frame = new byte[HeaderSize + data.Length];
        frame[0] = FrameMarker;
        frame[1] = target;
        WriteInt(frame, 2, sessionId);
        WriteInt(frame, 6, ++sendSeq);
        Buffer.BlockCopy(data, 0, frame, HeaderSize, data.Length);
        link.SendRaw(frame);
    }

    // ---------- Terima ----------

    private void OnRaw(string fromName, byte[] frame)
    {
        if (disposed || frame == null || frame.Length <= HeaderSize || frame[0] != FrameMarker) return;

        int from = IndexOf(fromName);
        if (from < 0 || from == LocalPlayerIndex) return;       // pengirim tidak dikenal
        if (!IsHost && from != 0) return;                       // client hanya mendengar host

        byte target = frame[1];
        if (target != ToAll && target != LocalPlayerIndex) return;   // untuk pemain lain

        int session = ReadInt(frame, 2);
        int seq = ReadInt(frame, 6);
        if (lastSeen.TryGetValue(fromName, out var seen) && seen.session == session && seq <= seen.seq) return;   // dobel / lama
        lastSeen[fromName] = (session, seq);

        var payload = new byte[frame.Length - HeaderSize];
        Buffer.BlockCopy(frame, HeaderSize, payload, 0, payload.Length);
        DataReceived?.Invoke(from, payload);
    }

    // Hanya berarti selama game berjalan (daftar terkunci). Di lobby, LobbyService sendiri yang mengurus pemain keluar.
    private void OnGone(string name)
    {
        if (disposed || frozen == null) return;

        int index = IndexOf(name);
        if (index < 0 || index == LocalPlayerIndex) return;
        if (IsHost || index == 0) PeerLost?.Invoke(index);   // client tidak melaporkan client lain: host yang memberi tahu
    }

    // ---------- Angka ----------

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static int ReadInt(byte[] buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
}
