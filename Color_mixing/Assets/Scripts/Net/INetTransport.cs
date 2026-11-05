using System;

// "Jalan" tempat pesan lewat. GameNet tidak peduli jalannya apa: Game Center di device,
// koneksi lokal di Mac, atau OfflineTransport (tanpa jaringan).
//
// Aturan untuk semua implementasi:
//  - Topologi bintang: client hanya mengirim ke host, host boleh mengirim ke siapa saja.
//  - DataReceived HARUS dipanggil di main thread (callback GameKit datang dari thread lain).
//  - Index pemain: host = 0, pemain lain 1 dan 2 (urutan slot dari lobby, sama di semua device).
public interface INetTransport
{
    bool IsHost { get; }
    int LocalPlayerIndex { get; }

    // (index pengirim, data mentah)
    event Action<int, byte[]> DataReceived;

    void SendToHost(byte[] data);               // dipanggil di client saja
    void SendToAll(byte[] data);                // ke semua pemain LAIN (tanpa diri sendiri)
    void SendTo(int playerIndex, byte[] data);  // ke satu pemain lain
}
