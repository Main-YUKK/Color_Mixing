using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu pause: Resume, BGM, SFX, Exit. Hanya menutupi layar; Time.timeScale TIDAK diubah, karena 3 pemain
// bermain bersama dan game tidak bisa berhenti hanya untuk satu orang.
//
// Tampilan: kalau ada prefab Resources/PauseMenu dipakai itu (tombol dicari berdasarkan nama:
// ResumeButton, ExitButton, BgmButton, SfxButton), kalau tidak dibuat menu sederhana dari kode.
public class PauseMenu : MonoBehaviour
{
    private const int SortingOrder = 30000;   // di atas game, di bawah peringatan (31000) dan layar loading (32000)
    private const string PrefabName = "PauseMenu";

    public static PauseMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

    private GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PauseMenu>();
    }

    public static void Open()
    {
        if (Instance == null) return;
        Instance.EnsureBuilt();
        Instance.root.SetActive(true);
    }

    public static void Close()
    {
        if (Instance != null && Instance.root != null) Instance.root.SetActive(false);
    }

    // Exit = sama dengan keluar dari game (pemain lain mendapat peringatan).
    public static void Exit()
    {
        Close();
        MatchSession.Leave();
    }

    // Pindah scene (mis. setelah keluar): menu tidak boleh ikut terbawa.
    private void OnEnable() => SceneLoader.Loaded += Close;
    private void OnDisable() => SceneLoader.Loaded -= Close;

    // ---------- Tampilan ----------

    private void EnsureBuilt()
    {
        if (root != null) return;

        root = BuildOverlay();
        var prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab != null) WirePrefab(Instantiate(prefab, root.transform, false));
        else BuildDefaultPanel(root.transform);
        root.SetActive(false);
    }

    private GameObject BuildOverlay()
    {
        var go = new GameObject("Pause", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Lapisan gelap menangkap tap supaya layar game di bawahnya tidak bisa ditekan.
        var dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(go.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        return go;
    }

    private void WirePrefab(GameObject card)
    {
        foreach (var button in card.GetComponentsInChildren<Button>(true))
        {
            switch (button.gameObject.name)
            {
                case "ResumeButton": button.onClick.AddListener(Close); break;
                case "ExitButton": button.onClick.AddListener(Exit); break;
                case "BgmButton": EnsureToggle(button.gameObject, SoundToggleButton.Kind.Bgm); break;
                case "SfxButton": EnsureToggle(button.gameObject, SoundToggleButton.Kind.Sfx); break;
            }
        }
    }

    private static void EnsureToggle(GameObject buttonObject, SoundToggleButton.Kind kind)
    {
        // Sudah dipasang di prefab (dengan ikon / label sendiri): biarkan. Kalau belum, tambahkan yang dasar.
        if (buttonObject.GetComponent<SoundToggleButton>() != null) return;
        buttonObject.AddComponent<SoundToggleButton>().Configure(kind, null);
    }

    private void BuildDefaultPanel(Transform parent)
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 860f);
        panel.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.24f, 0.97f);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 60, 60);
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        AddLabel(panel.transform, "Paused", 72, 130f);
        MakeButton(panel.transform, "ResumeButton", "Resume", Close);

        var bgm = MakeButton(panel.transform, "BgmButton", "BGM", null);
        bgm.AddComponent<SoundToggleButton>().Configure(SoundToggleButton.Kind.Bgm, bgm.GetComponentInChildren<TMP_Text>());

        var sfx = MakeButton(panel.transform, "SfxButton", "SFX", null);
        sfx.AddComponent<SoundToggleButton>().Configure(SoundToggleButton.Kind.Sfx, sfx.GetComponentInChildren<TMP_Text>());

        MakeButton(panel.transform, "ExitButton", "Exit", Exit);
    }

    private static GameObject MakeButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.32f, 0.36f, 0.55f, 1f);
        go.GetComponent<LayoutElement>().preferredHeight = 130f;
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(onClick);

        var label = AddLabel(go.transform, text, 52, 0f);
        Stretch(label.rectTransform);
        return go;
    }

    private static TextMeshProUGUI AddLabel(Transform parent, string text, float size, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        if (height > 0f)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }
        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
