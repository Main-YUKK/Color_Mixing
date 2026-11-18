using System.Collections.Generic;

// Catatan host: siapa saja yang sudah selesai memuat scene. Logika murni (tanpa Unity) supaya bisa dites.
public class StartHandshake
{
    private readonly bool[] ready;

    public StartHandshake(int playerCount)
    {
        ready = new bool[playerCount];
    }

    public int PlayerCount => ready.Length;

    // false kalau index tidak valid atau sudah tercatat (pesan dobel tidak mengubah apa-apa).
    public bool MarkReady(int index)
    {
        if (index < 0 || index >= ready.Length || ready[index]) return false;
        ready[index] = true;
        return true;
    }

    public bool IsReady(int index) => index >= 0 && index < ready.Length && ready[index];

    public bool AllReady
    {
        get
        {
            foreach (bool r in ready) if (!r) return false;
            return true;
        }
    }

    public List<int> Missing()
    {
        var list = new List<int>();
        for (int i = 0; i < ready.Length; i++) if (!ready[i]) list.Add(i);
        return list;
    }
}
