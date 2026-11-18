using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Panel setelan playtest untuk HOST, di scene Lobby: tombol kecil "Playtest" di pojok kiri bawah membuka panel.
// Isinya level kesulitan klien, lama timer klien, jeda pergantian warna (angka bebas), dan skor minimal.
// Nilainya disimpan di PlaytestSettings (dan di device host), lalu ikut terkirim ke semua pemain lewat DaySnapshot.
//
// Dibuat otomatis dari kode (tanpa prefab). Matikan seluruhnya lewat PlaytestSettings.Enabled.
public class PlaytestSettingsPanel : MonoBehaviour
{
    private const string LobbySceneName = "Lobby";
    private const int SortingOrder = 28000;   // di atas lobby, di bawah menu pause (30000)

    private static readonly Color Idle = new Color(0.30f, 0.31f, 0.38f, 1f);
    private static readonly Color Chosen = new Color(0.22f, 0.56f, 0.95f, 1f);

    public static PlaytestSettingsPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!PlaytestSettings.Enabled) return;
        var go = new GameObject("PlaytestSettingsPanel");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PlaytestSettingsPanel>();
    }

    private readonly List<(Image image, Func<bool> isChosen)> choices = new List<(Image, Func<bool>)>();
    private GameObject root;
    private GameObject launcher;
    private GameObject panel;
    private TMP_InputField rotationInput;
    private bool built;
    private bool loadedSaved;
    private float nextCheck;

    // Dicek tiap setengah detik: tombol hanya ada untuk host yang sedang di lobby.
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;

        bool show = ShouldShow();
        if (show && !built) Build();
        if (!built) return;

        launcher.SetActive(show);
        if (!show) panel.SetActive(false);
    }

    private static bool ShouldShow()
    {
        if (SceneManager.GetActiveScene().name != LobbySceneName) return false;
        var lobby = LobbyService.Instance;
        return lobby != null && lobby.Mode == LobbyMode.Host;
    }

    // ---------- Susunan ----------

    private void Build()
    {
        built = true;
        if (!loadedSaved) { PlaytestSettings.LoadSaved(); loadedSaved = true; }

        root = new GameObject("Playtest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Tombol pembuka, pojok kiri bawah.
        launcher = MakeButton(root.transform, "Playtest", Idle, () => { panel.SetActive(true); Refresh(); });
        var lr = (RectTransform)launcher.transform;
        lr.anchorMin = lr.anchorMax = lr.pivot = Vector2.zero;
        lr.anchoredPosition = new Vector2(24f, 24f);
        lr.sizeDelta = new Vector2(260f, 80f);

        BuildPanel();
        panel.SetActive(false);
    }

    private void BuildPanel()
    {
        // Lapisan gelap menangkap tap supaya lobby di bawahnya tidak ikut tertekan.
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        Stretch((RectTransform)panel.transform);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(panel.transform, false);
        var cr = (RectTransform)card.transform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(1200f, 0f);
        card.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.2f, 0.98f);
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 36, 36);
        layout.spacing = 18f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddLabel(card.transform, "Playtest Settings (Host)", 52, 80f, Color.white);

        AddChoiceRow(card.transform, "Client difficulty",
            new[] { "Normal", "Level 1", "Level 2", "Level 3" }, new[] { 0, 1, 2, 3 },
            () => PlaytestSettings.Difficulty, PlaytestSettings.SetDifficulty);

        AddChoiceRow(card.transform, "Client timer",
            new[] { "Default", "30 s", "1 min", "1 min 30 s" }, new[] { 0, 30, 60, 90 },
            () => PlaytestSettings.ClientSeconds, PlaytestSettings.SetClientSeconds);

        AddChoiceRow(card.transform, "Color accuracy (min score)",
            new[] { "Default", "70%", "80%", "85%", "90%" }, new[] { 0, 70, 80, 85, 90 },
            () => PlaytestSettings.PassingScore, PlaytestSettings.SetPassingScore);

        AddRotationRow(card.transform);

        var done = MakeButton(card.transform, "Done", Chosen, () => panel.SetActive(false));
        done.AddComponent<LayoutElement>().preferredHeight = 90f;
    }

    private void AddChoiceRow(Transform parent, string title, string[] names, int[] values, Func<int> get, Action<int> set)
    {
        var row = NewRow(parent, title);
        for (int i = 0; i < names.Length; i++)
        {
            int value = values[i];
            var button = MakeButton(row, names[i], Idle, () => { set(value); Refresh(); });
            var element = button.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.preferredHeight = 90f;
            choices.Add((button.GetComponent<Image>(), () => get() == value));
        }
    }

    private void AddRotationRow(Transform parent)
    {
        var row = NewRow(parent, $"Color switch every (seconds, {PlaytestSettings.MinRotationSeconds}-{PlaytestSettings.MaxRotationSeconds}; empty = default)");

        // Dibuat nonaktif dulu supaya TMP_InputField tidak berjalan sebelum semua isiannya terpasang.
        var go = new GameObject("RotationInput", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(row, false);
        go.SetActive(false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
        var element = go.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.preferredHeight = 90f;

        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        var vr = (RectTransform)viewport.transform;
        Stretch(vr);
        vr.offsetMin = new Vector2(24f, 8f);
        vr.offsetMax = new Vector2(-24f, -8f);

        var text = NewText(viewport.transform, "", 44, TextAlignmentOptions.MidlineLeft, Color.black);
        Stretch((RectTransform)text.transform);
        var placeholder = NewText(viewport.transform, "default", 44, TextAlignmentOptions.MidlineLeft, new Color(0f, 0f, 0f, 0.35f));
        Stretch((RectTransform)placeholder.transform);

        rotationInput = go.AddComponent<TMP_InputField>();
        rotationInput.textViewport = vr;
        rotationInput.textComponent = text;
        rotationInput.placeholder = placeholder;
        rotationInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        rotationInput.characterLimit = 3;
        rotationInput.onEndEdit.AddListener(OnRotationEntered);
        go.SetActive(true);
    }

    private void OnRotationEntered(string value)
    {
        int seconds = int.TryParse(value, out int parsed) ? parsed : 0;
        PlaytestSettings.SetRotationSeconds(seconds);
        Refresh();   // angka di luar rentang dikembalikan ke nilai yang dipakai
    }

    private void Refresh()
    {
        foreach (var (image, isChosen) in choices) image.color = isChosen() ? Chosen : Idle;
        if (rotationInput != null)
            rotationInput.SetTextWithoutNotify(PlaytestSettings.RotationSeconds > 0 ? PlaytestSettings.RotationSeconds.ToString() : "");
    }

    // ---------- Pembantu tampilan ----------

    // Baris: judul + deretan isi horizontal. Mengembalikan wadah deretannya.
    private Transform NewRow(Transform parent, string title)
    {
        var block = new GameObject("Row", typeof(RectTransform), typeof(VerticalLayoutGroup));
        block.transform.SetParent(parent, false);
        var vertical = block.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 8f;
        vertical.childControlWidth = vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        AddLabel(block.transform, title, 34, 46f, new Color(0.85f, 0.88f, 1f));

        var line = new GameObject("Options", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        line.transform.SetParent(block.transform, false);
        var horizontal = line.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 12f;
        horizontal.childControlWidth = horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = false;
        return line.transform;
    }

    private static GameObject MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var text = NewText(go.transform, label, 38, TextAlignmentOptions.Center, Color.white);
        Stretch((RectTransform)text.transform);
        return go;
    }

    private static void AddLabel(Transform parent, string text, float size, float height, Color color)
    {
        var label = NewText(parent, text, size, TextAlignmentOptions.Center, color);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private static TextMeshProUGUI NewText(Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = align;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
