using System;
using UnityEngine;

// Foto keadaan mixing + vote dari host. Satu pesan berisi semuanya, jadi client tidak pernah melihat setengah keadaan.
[Serializable]
public class MixSnapshot
{
    public int dayStamp;        // DayCycleManager.SyncStamp saat dikirim: foto ini milik klien yang mana
    public int seq;             // urutan kiriman (foto lama / dobel diabaikan)

    // mixing
    public int[] slots;
    public int[] units;
    public bool[] ready;
    public bool confirmed;
    public float rotationLeft;
    public int rotationCount;   // naik tiap rotasi pigmen: client menampilkan alert kalau angkanya naik

    // vote
    public int[] votes;
    public bool voting;
    public bool applied;
    public Color appliedColor;  // hanya berarti kalau applied
}
