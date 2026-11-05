using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Satu-satunya jalan pindah scene (mode single): layar loading menutup layar, scene dimuat, layar dibuka lagi.
// Muat additive (mis. ColorMixing di atas ClientArrival) TIDAK lewat sini, supaya tidak menutupi game.
//
// Tampilan: kalau ada prefab Resources/LoadingScreen (akar punya CanvasGroup) dipakai itu,
// kalau tidak dibuat layar hitam sederhana dari kode.
public class SceneLoader : MonoBehaviour
{
    private const float FadeInSeconds = 0.2f;
    private const float FadeOutSeconds = 0.25f;
    private const float HoldTimeoutSeconds = 60f;   // pengaman: tahanan yang lupa dilepas tidak boleh menutup layar selamanya
    private const string PrefabName = "LoadingScreen";

    public static SceneLoader Instance { get; private set; }

    // Scene baru sudah aktif (layar loading mungkin masih tampil, kalau ditahan).
    public static event Action Loaded;

    public static bool IsLoading => Instance != null && Instance.loading;

    private readonly LoadGate gate = new LoadGate();
    private CanvasGroup group;
    private TMP_Text label;
    private bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        Instance = null;
        Loaded = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("SceneLoader");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SceneLoader>();
    }

    // ---------- API ----------

    // hold = layar loading tetap tampil setelah scene termuat, sampai Release() dipanggil.
    // false = scene tidak ada di Build Settings, atau masih ada pemuatan lain yang berjalan.
    public static bool Load(string sceneName, bool hold = false)
    {
        if (Instance == null)
        {
            Debug.LogError("[LOAD] SceneLoader belum ada.");
            return false;
        }
        return Instance.Begin(sceneName, hold);
    }

    public static void Release()
    {
        if (Instance != null) Instance.gate.Release();
    }

    // ---------- Proses ----------

    private bool Begin(string sceneName, bool hold)
    {
        if (loading)
        {
            Debug.LogWarning($"[LOAD] '{sceneName}' diabaikan: masih memuat scene lain");
            return false;
        }
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LOAD] Scene '{sceneName}' tidak ditemukan. Cek nama dan Build Profiles.");
            return false;
        }

        loading = true;
        gate.Begin(hold);
        StartCoroutine(Run(sceneName));
        return true;
    }

    private IEnumerator Run(string sceneName)
    {
        EnsureScreen();
        group.gameObject.SetActive(true);
        group.blocksRaycasts = true;       // layar di bawah tidak bisa ditekan selama loading
        yield return Fade(1f, FadeInSeconds);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        gate.MarkLoaded();
        RaiseLoaded();
        yield return null;                 // beri scene baru satu frame untuk menjalankan Start()

        float waited = 0f;
        while (!gate.CanHide)
        {
            waited += Time.unscaledDeltaTime;
            if (waited > HoldTimeoutSeconds)
            {
                Debug.LogWarning("[LOAD] Layar loading ditahan terlalu lama, dilepas paksa");
                gate.Release();
                break;
            }
            yield return null;
        }

        yield return Fade(0f, FadeOutSeconds);
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
        loading = false;
    }

    private static void RaiseLoaded()
    {
        var evt = Loaded;
        if (evt == null) return;
        foreach (Action handler in evt.GetInvocationList())
        {
            try { handler(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private IEnumerator Fade(float target, float seconds)
    {
        float start = group.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        group.alpha = target;
    }

    private void Update()
    {
        // Titik-titik berjalan supaya terlihat tidak macet.
        if (!loading || label == null) return;
        int dots = (int)(Time.unscaledTime * 2f) % 4;
        label.text = "Loading" + new string('.', dots);
    }

    // ---------- Tampilan ----------

    private void EnsureScreen()
    {
        if (group != null) return;

        var prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab != null)
        {
            var instance = Instantiate(prefab, transform);
            instance.name = PrefabName;
            group = instance.GetComponent<CanvasGroup>();
            if (group == null) group = instance.AddComponent<CanvasGroup>();
            label = instance.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            BuildDefaultScreen();
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private void BuildDefaultScreen()
    {
        var root = new GameObject(PrefabName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // di atas semua canvas game

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = Color.black;

        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(root.transform, false);
        Stretch(text.GetComponent<RectTransform>());
        label = text.GetComponent<TextMeshProUGUI>();
        label.text = "Loading";
        label.fontSize = 56;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        group = root.GetComponent<CanvasGroup>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
