using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pemutar suara satu-satunya di game: BGM lobby + suara tombol.
//
// - Dibuat otomatis sebelum scene pertama dari prefab Resources/AudioManager (isi klip di prefab itu),
//   lalu hidup terus lintas scene (DontDestroyOnLoad), jadi BGM tidak putus / mulai ulang saat pindah scene.
// - BGM menyala di scene menu (MainMenu, Lobby, Tutorial, SaveFiles). Begitu scene lain dimuat (gameplay) BGM di-fade lalu berhenti.
//   Kembali ke menu / lobby setelah game: BGM mulai lagi dari awal.
// - Suara tombol: setiap sentuhan / klik yang jatuh tepat di Button yang bisa ditekan, di scene menu.
//   Tombol yang dibuat belakangan (popup, alert, slot pemain) otomatis ikut, tanpa dipasangi komponen.
// - Semua suara mengikuti SoundSettings (BGM On/Off dan SFX On/Off).
public class AudioManager : MonoBehaviour
{
    private const string PrefabName = "AudioManager";
    private const float BgmFadeOutSeconds = 0.5f;

    // Scene tempat BGM berbunyi dan tombol bersuara. ClientArrival punya lagu sendiri (BgmFor). Scene lain (Init, ColorMixing) hening.
    private static readonly string[] MenuScenes = { "MainMenu", "Lobby", "Tutorial", "SaveFiles" };

    // Scene tempat tombol bersuara: scene menu, plus ClientArrival (ColorMixing dimuat additive di atasnya, jadi ikut).
    private static readonly string[] ButtonSoundScenes = { "MainMenu", "Lobby", "Tutorial", "SaveFiles", "ClientArrival" };

    public static AudioManager Instance { get; private set; }

    [Header("Klip")]
    [SerializeField] private AudioClip buttonClip;
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip clientArrivalBgmClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private AudioSource voiceSource;     // suara klien saat membacakan request: bisa dihentikan
    private AudioSource warningSource;   // klip peringatan timer: bisa dihentikan, tidak seperti PlayOneShot
    private Coroutine fade;

    public static bool IsMenuScene(string sceneName) => System.Array.IndexOf(MenuScenes, sceneName) >= 0;

    public static bool HasButtonSound(string sceneName) => System.Array.IndexOf(ButtonSoundScenes, sceneName) >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab == null)
        {
            Debug.LogError($"[AUDIO] Prefab Resources/{PrefabName} tidak ditemukan, game berjalan tanpa suara.");
            return;
        }

        var go = Instantiate(prefab);
        go.name = PrefabName;
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sfxSource = CreateSource(loop: false, sfxVolume);
        bgmSource = CreateSource(loop: true, bgmVolume);
        voiceSource = CreateSource(loop: false, sfxVolume);
        warningSource = CreateSource(loop: false, sfxVolume);
    }

    private AudioSource CreateSource(bool loop, float volume)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 0f;
        return source;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SoundSettings.Changed += ApplySettings;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SoundSettings.Changed -= ApplySettings;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ApplySettings();
        // Aman kalau Play dimulai langsung dari scene menu di Editor.
        StartBgm(BgmFor(SceneManager.GetActiveScene().name));
    }

    // Lagu untuk sebuah scene. null = hening (Init, ColorMixing, dll).
    // ClientArrival dicek lebih dulu supaya tidak ikut lagu lobby kalau suatu hari masuk MenuScenes.
    private AudioClip BgmFor(string sceneName)
    {
        if (sceneName == "ClientArrival") return clientArrivalBgmClip;
        if (IsMenuScene(sceneName)) return bgmClip;
        return null;
    }

    // ---------- BGM ----------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;   // scene additive (ColorMixing di atas ClientArrival) tidak mengubah BGM

        var clip = BgmFor(scene.name);
        if (clip != null) StartBgm(clip);
        else StopBgm();
    }

    private void StartBgm(AudioClip clip)
    {
        if (bgmSource == null || clip == null) return;

        // Masih ada fade-out berjalan (pemain cepat kembali ke menu): batalkan, lagu lanjut tanpa mulai ulang.
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
            bgmSource.volume = bgmVolume;
        }

        // Lagu berbeda (mis. lobby -> ClientArrival): ganti klip.
        if (bgmSource.clip != clip)
        {
            bgmSource.Stop();
            bgmSource.clip = clip;
        }

        // Sudah berbunyi (pindah antar scene menu): jangan diputar ulang.
        if (!bgmSource.isPlaying) bgmSource.Play();
    }

    private void StopBgm()
    {
        if (bgmSource == null || !bgmSource.isPlaying || fade != null) return;
        fade = StartCoroutine(FadeOutBgm());
    }

    private IEnumerator FadeOutBgm()
    {
        float start = bgmSource.volume;
        for (float t = 0f; t < BgmFadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(start, 0f, t / BgmFadeOutSeconds);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = bgmVolume;
        fade = null;
    }

    // BGM Off: lagu tetap berjalan tapi dibisukan, jadi saat dinyalakan lagi ia lanjut seperti biasa.
    private void ApplySettings()
    {
        if (bgmSource != null) bgmSource.mute = !SoundSettings.BgmOn;
        if (voiceSource != null) voiceSource.mute = !SoundSettings.SfxOn;
        if (warningSource != null) warningSource.mute = !SoundSettings.SfxOn;
    }

    // ---------- Suara efek ----------

    // Efek sekali putar (klien masuk / pergi, feedback, nyawa, dll). Mengikuti SFX On/Off.
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null || !SoundSettings.SfxOn) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // Suara klien saat mengucapkan request. Satu per waktu; klip baru memotong yang lama.
    public void PlayVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null) return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
    }

    // Peringatan timer: klip panjang yang berakhir tepat saat waktu habis. startAt > 0 = mulai di tengah klip
    // (waktu klien lebih pendek dari klipnya). Dibunyikan lewat source sendiri supaya bisa dihentikan.
    public void PlayTimerWarning(AudioClip clip, float startAt = 0f)
    {
        if (clip == null || warningSource == null) return;
        warningSource.Stop();
        warningSource.clip = clip;
        warningSource.time = Mathf.Clamp(startAt, 0f, Mathf.Max(0f, clip.length - 0.01f));
        warningSource.Play();
    }

    public void StopTimerWarning()
    {
        if (warningSource != null) warningSource.Stop();
    }

    // ---------- Suara tombol ----------

    private void Update()
    {
        if (buttonClip == null || !SoundSettings.SfxOn) return;
        if (!TryGetPressPosition(out Vector2 position)) return;
        if (!HasButtonSound(SceneManager.GetActiveScene().name)) return;

        if (IsInteractableButtonAt(position)) sfxSource.PlayOneShot(buttonClip, sfxVolume);
    }

    // Sentuhan / klik kiri yang baru mulai di frame ini (hanya satu per frame).
    private static bool TryGetPressPosition(out Vector2 position)
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (!touches[i].press.wasPressedThisFrame) continue;
                position = touches[i].position.ReadValue();
                return true;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    // Hanya objek UI paling atas di titik itu yang dihitung: layar loading / popup yang menutupi tombol
    // membuat tombol di bawahnya diam, sama seperti perilaku klik aslinya.
    private static readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();

    private static bool IsInteractableButtonAt(Vector2 position)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        hits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hits);
        if (hits.Count == 0) return false;

        var button = hits[0].gameObject.GetComponentInParent<Button>();
        return button != null && button.IsActive() && button.IsInteractable()
            && button.GetComponent<PigmentButton>() == null;   // tombol pigmen punya suara sendiri (AddColor)
    }
}
