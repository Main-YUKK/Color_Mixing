using UnityEngine;

// Simpan progres endless di device host (PlayerPrefs).
public static class SaveService
{
    private const string Key = "endless_save";

    public static bool HasSave() => PlayerPrefs.HasKey(Key);

    public static SaveData Load()
    {
        if (!HasSave()) return new SaveData();
        return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key)) ?? new SaveData();
    }

    public static void Save(SaveData data)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // Kembali ke Day 1, rekor hari tertinggi tetap.
    public static void ResetProgress()
    {
        Save(new SaveData { nextDay = 1, bestDay = Load().bestDay });
    }
}
