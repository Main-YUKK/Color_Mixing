using System;

// Tanpa jaringan: device ini host sekaligus satu-satunya pemain. Semua kiriman dibuang.
// Untuk Editor dan test saja. Di build asli TIDAK dipakai sebagai cadangan kalau koneksi putus.
public class OfflineTransport : INetTransport
{
    public bool IsHost => true;
    public int LocalPlayerIndex => 0;

#pragma warning disable CS0067   // tidak pernah dipanggil: tidak ada pemain lain
    public event Action<int, byte[]> DataReceived;
#pragma warning restore CS0067

    public void SendToHost(byte[] data) { }
    public void SendToAll(byte[] data) { }
    public void SendTo(int playerIndex, byte[] data) { }
}
