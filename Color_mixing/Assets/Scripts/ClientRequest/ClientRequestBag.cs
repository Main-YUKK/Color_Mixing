using System.Collections.Generic;
using UnityEngine;

// Mengambil request acak per level, tanpa berulang dalam satu run sampai semua request di level itu sudah keluar.
public class ClientRequestBag
{
    private readonly ClientRequestDatabase _database;
    private readonly Dictionary<RequestDifficulty, List<ClientRequest>> _bags = new();

    public ClientRequestBag(ClientRequestDatabase database) => _database = database;

    private void Refill(RequestDifficulty level)
    {
        var list = _database.GetByDifficulty(level);
         
        // Fisher shuffle untuk acak urutan list. 
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        // Simpan list acak ke dictionary. 
        _bags[level] = list;
    }

    public ClientRequest Next(RequestDifficulty difficulty)
    {
        if (!_bags.TryGetValue(difficulty, out var bag) || bag.Count == 0)
        {
            Refill(difficulty);
            bag = _bags[difficulty];
        }

        // Ambil request terakhir di list, hapus dari list. Kalau habis, refill otomatis di atas.
        var request = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return request;
    }
}
