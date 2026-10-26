using UnityEngine;
using System.Collections.Generic;
// Digunakan untuk menampung pigmen yang akan dicampur.
public class MixingBowl
{
    private readonly int[] pigmentUnits; // jumlah unit dari setiap pigmen yang ditambahkan ke dalam mangkuk pencampuran

    public MixingBowl(int pigmentCount)
    {
        pigmentUnits = new int[pigmentCount];
    }
    public IReadOnlyList<int> PigmentUnits => pigmentUnits;
    public int TotalUnits {get; private set;} 
        public bool IsEmpty => TotalUnits == 0; 

    public void AddPigment(int pigmentIndex, int amount = 1) //1 default, bisa juga tambahkan lebih dari 1 unit sekaligus
    {
        pigmentUnits[pigmentIndex] += amount;
        TotalUnits += amount;
    }
    

    // Untuk Redo: menghapus semua pigmen dari mangkuk pencampuran, mengatur ulang jumlah unit pigmen menjadi 0, dan mengatur TotalUnits menjadi 0.
    public  void Clear()
    {
        System.Array.Clear(pigmentUnits, 0, pigmentUnits.Length);
        TotalUnits = 0;
    }
}
