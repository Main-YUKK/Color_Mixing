using System;
using System.Collections.Generic;
using UnityEngine;

// Pintu kirim / terima pesan game. Kode game hanya bicara dengan GameNet, tidak tahu jalannya apa.
//
// Kirim ke host dari host sendiri tetap "terkirim" (ke diri sendiri): jalur tap -> host -> hasil
// sama untuk host dan client, dan mode tanpa jaringan tidak butuh kode khusus.
public class GameNet
{
    // ---------- Instance aktif ----------
    // Null = belum ada sesi. Di build asli sesi dimulai saat game dimulai dari lobby (Begin),
    // dan diakhiri saat keluar (End). Putus koneksi TIDAK membuat sesi offline diam-diam.
    public static GameNet Instance { get; private set; }

    public static void Begin(INetTransport transport)
    {
        End();
        Instance = new GameNet(transport);
    }

    public static void BeginOffline() => Begin(new OfflineTransport());

    public static void End()
    {
        Instance?.Dispose();
        Instance = null;
    }

    // Reload Domain dimatikan: static tidak reset sendiri saat Play berikutnya.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

#if UNITY_EDITOR
    // Hanya di Editor: scene gameplay boleh di-Play sendirian tanpa lobby, jadi mulai sebagai host sendirian.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EditorFallback()
    {
        if (Instance == null) BeginOffline();
    }
#endif

    // ---------- Instance ----------
    private readonly INetTransport transport;
    private readonly Dictionary<string, Action<NetMessage>> handlers = new Dictionary<string, Action<NetMessage>>();

    public GameNet(INetTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.transport.DataReceived += OnData;
    }

    public bool IsHost => transport.IsHost;
    public int LocalPlayerIndex => transport.LocalPlayerIndex;
    public bool IsOffline => transport is OfflineTransport;

    private void Dispose()
    {
        transport.DataReceived -= OnData;
        handlers.Clear();
    }

    // ---------- Terima ----------

    public void Subscribe(string type, Action<NetMessage> handler)
    {
        handlers.TryGetValue(type, out var existing);
        handlers[type] = existing + handler;
    }

    public void Unsubscribe(string type, Action<NetMessage> handler)
    {
        if (!handlers.TryGetValue(type, out var existing)) return;

        existing -= handler;
        if (existing == null) handlers.Remove(type);
        else handlers[type] = existing;
    }

    private void OnData(int from, byte[] data)
    {
        if (!NetMessage.TryDecode(data, out var message))
        {
            Debug.LogWarning($"[NET] Pesan dari pemain {from} tidak terbaca, diabaikan");
            return;
        }

        message.from = from;   // dari transport, bukan dari isi pesan
        Dispatch(message);
    }

    private void Dispatch(NetMessage message)
    {
        if (!handlers.TryGetValue(message.type, out var handler) || handler == null) return;

        // Satu handler yang error tidak boleh menghentikan handler lain.
        foreach (Action<NetMessage> h in handler.GetInvocationList())
        {
            try { h(message); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // ---------- Kirim ----------

    // Client: ke host. Host: ke diri sendiri (diterima lewat Subscribe seperti pesan dari client).
    public void SendToHost(NetMessage message)
    {
        if (IsHost) DeliverToSelf(message);
        else transport.SendToHost(message.Encode());
    }

    // Ke semua pemain LAIN. Pengirim tidak menerima pesannya sendiri.
    public void SendToAll(NetMessage message) => transport.SendToAll(message.Encode());

    public void SendTo(int playerIndex, NetMessage message)
    {
        if (playerIndex == LocalPlayerIndex) DeliverToSelf(message);
        else transport.SendTo(playerIndex, message.Encode());
    }

    // Lewat encode + decode juga, supaya pesan ke diri sendiri sama persis dengan pesan dari jaringan
    // (tidak ada objek yang dipakai bersama).
    private void DeliverToSelf(NetMessage message) => OnData(LocalPlayerIndex, message.Encode());
}
