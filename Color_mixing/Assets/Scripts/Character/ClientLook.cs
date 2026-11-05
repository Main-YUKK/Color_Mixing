using System;
using UnityEngine;

// Rupa rambut satu klien, semua hasil acak sudah "dikunci" jadi angka supaya bisa dikirim host ke client.
[Serializable]
public class HairLook
{
    public int style;          // index di RandomCharacter.hairStyles
    public int variant;        // 0 = sheet dasar, n = colorVariants[n - 1]
    public float jitterX;
    public float jitterY;
    public bool front;         // rambut di depan badan
    public Color color = Color.white;
}

// Rupa satu klien: prefab mana + rambutnya. Host yang mengacak, client hanya memakai.
[Serializable]
public class ClientLook
{
    public bool valid;         // false = tidak ada data (JsonUtility tidak punya null untuk objek bersarang)
    public int prefabIndex;    // index di CharacterSpawner.characterPrefabs
    public bool hasHair;
    public HairLook hair;
}
