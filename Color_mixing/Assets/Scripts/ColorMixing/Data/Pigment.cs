using UnityEngine;

[System.Serializable]
public class Pigment
{
    public string pigmentName;
    public Color color; // warna RGB untuk preview di UI
    public Vector3 ryb; // nilai RYB untuk perhitungan pencampuran warna 
    public Sprite icon;   // gambar untuk tombol
    public Sprite bottle;         // botol cat di tangan
    public Sprite bottlePressed;  // botol saat ditekan (menuang)
    public Sprite unitIcon;       // tetes kecil di daftar jumlah unit (reveal)

}
