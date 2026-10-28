using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Simpan progres endless di device host: satu file JSON per room di persistentDataPath/Saves.
public static class SaveService
{
    public static string Dir => Path.Combine(Application.persistentDataPath, "Saves");
    private static string PathOf(string id) => Path.Combine(Dir, id + ".json");

    // Save yang sedang dimainkan (dipilih dari Saved Files). Null = game baru, file dibuat saat hari pertama selesai.
    public static SaveFile Active { get; private set; }

    // Reload Domain dimatikan: static tidak reset sendiri saat Play berikutnya.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Active = null;

    public static void SetActive(SaveFile file) => Active = file;
    public static void ClearActive() => Active = null;

    // Semua save, yang terakhir diubah paling atas. File rusak dilewati.
    public static List<SaveFile> LoadAll()
    {
        var list = new List<SaveFile>();
        if (!Directory.Exists(Dir)) return list;

        var files = new DirectoryInfo(Dir).GetFiles("*.json");
        Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

        foreach (var f in files)
        {
            try
            {
                var save = JsonUtility.FromJson<SaveFile>(File.ReadAllText(f.FullName));
                if (save != null && !string.IsNullOrEmpty(save.id)) list.Add(save);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] File rusak dilewati: {f.Name} ({e.Message})");
            }
        }
        return list;
    }

    // Dipanggil saat satu hari selesai: catat hari itu dan hari berikutnya.
    // Belum ada save aktif = game baru, jadi file dibuat dari room saat ini.
    public static void RecordDayCompleted(int day)
    {
        // Hanya host yang menyimpan.
        if (LobbyService.Instance != null && LobbyService.Instance.Mode == LobbyMode.Client) return;

        if (Active == null) Active = CreateFromLobby();
        Active.completedDay = day;
        Active.nextDay = day + 1;
        Write(Active);
    }

    // Game over: sesi berikutnya mulai dari Day 1. File dan nama room tetap ada.
    public static void ResetActiveProgress()
    {
        if (Active == null) return;
        Active.completedDay = 0;
        Active.nextDay = 1;
        Write(Active);
    }

    public static void Rename(SaveFile file, string newName)
    {
        newName = (newName ?? "").Trim();
        if (newName.Length == 0) return;
        if (newName.Length > LobbyService.MaxRoomNameLength) newName = newName.Substring(0, LobbyService.MaxRoomNameLength);

        file.roomName = newName;
        Write(file);
    }

    public static void Delete(SaveFile file)
    {
        try { File.Delete(PathOf(file.id)); }
        catch (Exception e) { Debug.LogError("[Save] Gagal menghapus: " + e.Message); }

        if (Active != null && Active.id == file.id) Active = null;
    }

    private static SaveFile CreateFromLobby()
    {
        var lobby = LobbyService.Instance;
        var host = lobby != null ? lobby.Players.Find(p => p.isHost) : null;
        string hostName = host != null ? host.name : "Player";
        bool hasRoomName = lobby != null && !string.IsNullOrEmpty(lobby.RoomName);

        return new SaveFile
        {
            id = Guid.NewGuid().ToString("N"),
            roomName = hasRoomName ? lobby.RoomName : $"{hostName}'s Room",
            roomCode = lobby != null && lobby.RoomCode != null ? lobby.RoomCode : "",
            hostName = hostName,
        };
    }

    private static void Write(SaveFile file)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = PathOf(file.id);
            string tmp = path + ".tmp";
            // tulis ke file sementara dulu supaya save lama tidak rusak kalau app tertutup di tengah.
            File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] Gagal menyimpan: " + e.Message);
        }
    }
}
