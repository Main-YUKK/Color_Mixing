using UnityEngine;
using System;

public class ColorMixingController : MonoBehaviour
{
    // referensi ke PigmentPalette
    [SerializeField] private PigmentPalette palette; 
    // interval acak ulang pigmen pemain (detik)
    [SerializeField] private float rotationInterval = 10f; 

    public MixingBowl MixingBowl { get; private set; } // referensi ke MixingBowl
    public float RotationTimerLeft { get; private set; } // waktu tersisa untuk rotasi warna target

    private int[] slots; // array untuk menyimpan indeks pigmen yang dipegang oleh pemain

    public event Action StateChanged;

    private void Awake()
    {
        MixingBowl = new MixingBowl(palette.Count);
        Rotate();
    }

    private void Update()
    {
        RotationTimerLeft -= Time.deltaTime;
        if (RotationTimerLeft <= 0f) Rotate();
    }

    // slot 0 atau 1 milik pemain -> index pigmen di palet
    public int GetPigment(int player, int slot) => slots[player * 2 + slot];

    private void Rotate()
    {
        slots = PigmentAssignment.Shuffle(palette.Count);
        RotationTimerLeft = rotationInterval;
        Debug.Log($"[MIX] Rotasi: P1 {Name(0, 0)}+{Name(0, 1)}, P2 {Name(1, 0)}+{Name(1, 1)}, P3 {Name(2, 0)}+{Name(2, 1)}");
        StateChanged?.Invoke();
    }

    private string Name(int player, int slot) => palette.GetPigment(GetPigment(player, slot)).pigmentName;
}
