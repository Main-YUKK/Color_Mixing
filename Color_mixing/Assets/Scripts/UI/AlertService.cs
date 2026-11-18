using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Peringatan singkat di atas semua layar (pemain keluar, host keluar, dst). Menutup sendiri setelah beberapa detik,
// atau saat tombol "OkayButton" di prefab ditekan. Waktu pakai unscaled, jadi tetap jalan walau game dijeda.
//
// Prefab dicari di Resources/Alerts/<nama> (pindahkan prefab dari Prefabs/Alerts ke sana).
// Kalau prefab tidak ada, dipakai kotak sederhana dari kode supaya alur keluar tetap jalan.
public class AlertService : MonoBehaviour
{
    private const int SortingOrder = 31000;   // di atas game, di bawah layar loading (32000)

    private static readonly Regex HostToken = new Regex(@"\[Host\]", RegexOptions.Compiled);
    private static readonly Regex UsernameToken = new Regex(@"\[username\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PlayerToken = new Regex(@"\[Player \d+\]", RegexOptions.Compiled);

    public static AlertService Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("AlertService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AlertService>();
    }

    // prefabName = nama di Resources/Alerts. playerNumber (1 = host) mengganti "[Player 2]" di teks prefab.
    // onClosed selalu dipanggil tepat satu kali.
    // hostName mengganti "[Host]" di teks prefab (mis. "Please wait for [Host] to start the room again").
    // playerName (nama Game Center) mengganti "[username]" di teks prefab; kosong = "Player N".
    public static void Show(string prefabName, int playerNumber, Action onClosed, float seconds = 3f, string fallbackText = null, string hostName = "Host", string playerName = null)
    {
        if (Instance == null) { onClosed?.Invoke(); return; }
        Instance.StartCoroutine(Instance.Run(prefabName, playerNumber, onClosed, seconds, fallbackText, hostName, playerName));
    }

    private IEnumerator Run(string prefabName, int playerNumber, Action onClosed, float seconds, string fallbackText, string hostName, string playerName)
    {
        var overlay = BuildOverlay();
        bool closed = false;

        var prefab = Resources.Load<GameObject>("Alerts/" + prefabName);
        if (prefab != null)
        {
            var card = Instantiate(prefab, overlay.transform, false);
            string shownName = string.IsNullOrEmpty(playerName) ? $"Player {playerNumber}" : playerName;
            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
            {
                string t = UsernameToken.Replace(text.text, m => shownName);
                t = PlayerToken.Replace(t, $"[Player {playerNumber}]");
                text.text = HostToken.Replace(t, hostName ?? "Host");
            }

            foreach (var button in card.GetComponentsInChildren<Button>(true))
                if (button.gameObject.name == "OkayButton") button.onClick.AddListener(() => closed = true);
        }
        else
        {
            Debug.LogWarning($"[ALERT] Prefab 'Resources/Alerts/{prefabName}' tidak ditemukan, dipakai kotak sederhana");
            AddFallbackLabel(overlay.transform, string.IsNullOrEmpty(fallbackText) ? prefabName : fallbackText);
        }

        for (float t = 0f; t < seconds && !closed; t += Time.unscaledDeltaTime) yield return null;

        Destroy(overlay);
        onClosed?.Invoke();
    }

    // Layar penuh: kanvas di atas semua + lapisan gelap yang menangkap tap supaya layar di bawah tidak bisa ditekan.
    private GameObject BuildOverlay()
    {
        var root = new GameObject("Alert", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root.transform, false);
        var rt = dim.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return root;
    }

    private static void AddFallbackLabel(Transform parent, string message)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = message;
        label.fontSize = 52;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
    }
}
