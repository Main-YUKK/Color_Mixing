using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ClientRequestDatabase", menuName = "Color Mixing/Client Request Database")]
public class ClientRequestDatabase : ScriptableObject
{
    [SerializeField] private List<ClientRequest> clientRequests = new List<ClientRequest>();

    // Cari request berdasarkan id, untuk sinkronisasi antar pemain saat multiplayer.
    public ClientRequest GetById(string id) => clientRequests.Find(request => request.requestId == id);

    // Ambil daftar request berdasarkan level difficulty.
    public List<ClientRequest> GetByDifficulty(RequestDifficulty difficulty) => clientRequests.FindAll(request => request.difficulty == difficulty);
    
    // TODO: Import data request dari spreadsheet (TSV). Sementara diisi manual di Inspector.
}
