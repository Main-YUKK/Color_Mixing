// Aturan kapan layar loading boleh hilang: scene baru sudah termuat DAN tidak sedang ditahan.
// Ditahan = menunggu hal lain dulu (nanti: semua pemain selesai memuat scene, lalu host kirim GO).
public class LoadGate
{
    public bool Loaded { get; private set; }
    public bool Held { get; private set; }

    public bool CanHide => Loaded && !Held;

    public void Begin(bool hold)
    {
        Loaded = false;
        Held = hold;
    }

    public void MarkLoaded() => Loaded = true;

    public void Release() => Held = false;
}
