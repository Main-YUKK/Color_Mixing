using UnityEngine;



// 6 slot (2 per pemain) = semua pigmen + 1 duplikat acak. Pemain p memegang slots[2p] dan slots[2p + 1].
// Tidak ada pemain yang memegang 2 pigmen yang sama.
public static class PigmentAssignment
{
    public const int PlayerCount = 3;

    public static int[] Shuffle(int pigmentCount)
    {
        // Isi slot: 0,1,2,3,4 + 1 pigmen acak
        var slots = new int[pigmentCount + 1];
        for (int i = 0; i < pigmentCount; i++) slots[i] = i;
        slots[pigmentCount] = Random.Range(0, pigmentCount); //slot terakhir = duplikat acak dari salah satu pigmen

        // Acak urutan slot
        for (int i = slots.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]); 
        }

        // Kalau ada pemain yang dapat pigmen dobel, tukar slot keduanya dengan slot berikutnya.
        // Duplikatnya cuma 1, jadi pertukaran ini tidak bisa membuat dobel baru.
        for (int p = 0; p < PlayerCount; p++)
        {
            if (slots[2 * p] != slots[2 * p + 1]) continue; 
            int next = (2 * p + 2) % slots.Length; 
            (slots[2 * p + 1], slots[next]) = (slots[next], slots[2 * p + 1]);
            break;
        }
        return slots;
    }
}
