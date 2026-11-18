using System;
using System.Text;
using UnityEngine;

// Jenis pesan game. Pesan lobby (HELLO, ACK, ROOM, READY, KICK, REFORM) memakai LobbyMessage sendiri,
// jadi nama di sini tidak boleh sama dengan itu. Jenis baru cukup ditambah di sini, format pesan tidak berubah.
public static class NetMessageType
{
    public const string Start = "START";             // host: mulai game (isi: hari awal)
    public const string SceneReady = "SCENE_READY";  // pemain: scene sudah selesai dimuat
    public const string Go = "GO";                   // host: semua siap, game jalan
    public const string Leave = "LEAVE";             // pemain: aku keluar
    public const string PlayerLeft = "PLAYER_LEFT";  // host -> client lain: pemain ini keluar (isi: index)
    public const string DayState = "DAY_STATE";      // host: foto keadaan hari (DaySnapshot)
    public const string MixAdd = "MIX_ADD";          // client -> host: tambah pigmen (isi: slot, jumlah)
    public const string MixReady = "MIX_READY";      // client -> host: Mix / Un-Mix
    public const string MixVote = "MIX_VOTE";        // client -> host: vote Apply / Redo
    public const string MixPull = "MIX_PULL";        // client -> host: kirim ulang keadaan mixing (scene client baru selesai dimuat)
    public const string MixState = "MIX_STATE";      // host -> semua: foto keadaan mixing + vote (MixSnapshot)
    public const string ClientTimeout = "CLIENT_TIMEOUT";   // host -> semua: waktu klien habis
    public const string Heartbeat = "HB";            // semua: aku masih terhubung (tiap detik selama game)
    public const string Ping = "PING";               // detak: aku masih terhubung
}

// Satu pesan antar-device. Isi khusus (payload) berupa JSON dari struct / class [Serializable] apa pun.
[Serializable]
public class NetMessage
{
    public string type;
    public int from = -1;      // index pengirim. DIISI PENERIMA dari transport, nilai kiriman diabaikan (tidak bisa dipalsukan)
    public string payload;     // JSON dari data khusus, boleh kosong

    public static NetMessage Create(string type) => new NetMessage { type = type };

    // T harus class / struct [Serializable] (bukan int / string langsung, JsonUtility tidak bisa).
    public static NetMessage Create<T>(string type, T data) =>
        new NetMessage { type = type, payload = JsonUtility.ToJson(data) };

    public T Read<T>() => string.IsNullOrEmpty(payload) ? default : JsonUtility.FromJson<T>(payload);

    public byte[] Encode() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

    // false kalau data rusak / bukan pesan game. Tidak pernah melempar exception.
    public static bool TryDecode(byte[] data, out NetMessage message)
    {
        message = null;
        if (data == null || data.Length == 0) return false;

        try
        {
            message = JsonUtility.FromJson<NetMessage>(Encoding.UTF8.GetString(data));
        }
        catch (Exception)
        {
            message = null;
            return false;
        }

        if (message == null || string.IsNullOrEmpty(message.type))
        {
            message = null;
            return false;
        }
        return true;
    }
}
