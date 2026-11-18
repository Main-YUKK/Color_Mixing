using System;
using System.Collections.Generic;

// Sambungan ke match Game Center yang sudah dibuka LobbyService. Dipisah dari kelas GameKit supaya
// GameKitTransport bisa dites dengan jaringan palsu (tidak ada tipe Apple di sini).
public interface IGameLink
{
    bool IsHost { get; }
    string LocalName { get; }

    // Nama pemain urut: [host, client pertama, client kedua], sama di semua device (urutan slot lobby).
    IReadOnlyList<string> CurrentRoster();

    // Kirim ke SEMUA pemain lain di match (Reliable). Pemilahan tujuan dilakukan GameKitTransport.
    void SendRaw(byte[] data);

    // Data mentah masuk dari pemain (namanya, data). Selalu di main thread.
    event Action<string, byte[]> RawReceived;

    // Pemain terputus dari match (namanya). Selalu di main thread.
    event Action<string> PlayerGone;
}
