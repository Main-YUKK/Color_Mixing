using System;
using UnityEngine;

// Pengaturan suara pemain: BGM (musik) dan SFX (efek), tersimpan antar sesi.
// Audio-nya sendiri belum ada: nanti sumber suara cukup membaca BgmOn / SfxOn dan mendengarkan Changed.
public static class SoundSettings
{
    private const string BgmKey = "sound_bgm_on";
    private const string SfxKey = "sound_sfx_on";

    // Tempat penyimpanan. Default PlayerPrefs; test memakai penyimpanan di memori supaya pengaturan asli tidak tersentuh.
    public interface IStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
    }

    private class PrefsStore : IStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
    }

    private static IStore store = new PrefsStore();

    // Reload Domain dimatikan: kembalikan ke keadaan awal tiap Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        store = new PrefsStore();
        Changed = null;
    }

    public static void UseStore(IStore newStore) => store = newStore ?? new PrefsStore();

    // Salah satu pengaturan berubah (untuk memperbarui tombol dan sumber suara).
    public static event Action Changed;

    public static bool BgmOn
    {
        get => store.GetInt(BgmKey, 1) == 1;
        set => Set(BgmKey, value);
    }

    public static bool SfxOn
    {
        get => store.GetInt(SfxKey, 1) == 1;
        set => Set(SfxKey, value);
    }

    private static void Set(string key, bool value)
    {
        if (store.GetInt(key, 1) == (value ? 1 : 0)) return;   // tidak berubah: tidak ada event
        store.SetInt(key, value ? 1 : 0);
        Changed?.Invoke();
    }
}
