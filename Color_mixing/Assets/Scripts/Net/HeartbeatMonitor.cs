using System;
using System.Collections.Generic;

public enum PeerHealth { Good, Unstable, Lost }

// Mencatat kapan terakhir mendengar tiap pemain, dan menilai apakah koneksinya masih sehat.
// Logika murni (waktu diberikan dari luar) supaya bisa dites tanpa menunggu.
public class HeartbeatMonitor
{
    public const float DefaultUnstableAfter = 4f;    // diam selama ini = koneksi tidak stabil
    public const float DefaultLostAfter = 10f;       // diam selama ini = dianggap putus

    private class Peer
    {
        public float lastHeard;
        public PeerHealth health;
    }

    private readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
    private readonly float unstableAfter;
    private readonly float lostAfter;

    public HeartbeatMonitor(float unstableAfter = DefaultUnstableAfter, float lostAfter = DefaultLostAfter)
    {
        this.unstableAfter = unstableAfter;
        this.lostAfter = lostAfter;
    }

    public event Action<int> Unstable;    // mulai tidak terdengar
    public event Action<int> Recovered;   // terdengar lagi sebelum dianggap putus

    public int TrackedCount => peers.Count;

    public void Track(int index, float now) => peers[index] = new Peer { lastHeard = now, health = PeerHealth.Good };
    public void Untrack(int index) => peers.Remove(index);
    public void Clear() => peers.Clear();

    public PeerHealth HealthOf(int index) => peers.TryGetValue(index, out var p) ? p.health : PeerHealth.Good;

    // Ada kabar dari pemain ini. Pemain yang sudah dianggap putus tidak dihidupkan lagi (keputusan sudah diambil).
    public void Heard(int index, float now)
    {
        if (!peers.TryGetValue(index, out var p) || p.health == PeerHealth.Lost) return;

        p.lastHeard = now;
        if (p.health == PeerHealth.Unstable)
        {
            p.health = PeerHealth.Good;
            Recovered?.Invoke(index);
        }
    }

    // Device ini sendiri sempat macet (scene berat, app di latar): jangan salahkan pemain lain.
    public void Resync(float now)
    {
        foreach (var p in peers.Values)
            if (p.health != PeerHealth.Lost) p.lastHeard = now;
    }

    // Periksa semua pemain. Mengembalikan yang BARU dianggap putus pada pemeriksaan ini (sekali saja per pemain).
    public List<int> Check(float now)
    {
        var newlyLost = new List<int>();
        foreach (var pair in peers)
        {
            var p = pair.Value;
            if (p.health == PeerHealth.Lost) continue;

            float silence = now - p.lastHeard;
            if (silence >= lostAfter)
            {
                p.health = PeerHealth.Lost;
                newlyLost.Add(pair.Key);
            }
            else if (silence >= unstableAfter && p.health == PeerHealth.Good)
            {
                p.health = PeerHealth.Unstable;
                Unstable?.Invoke(pair.Key);
            }
        }
        return newlyLost;
    }
}
