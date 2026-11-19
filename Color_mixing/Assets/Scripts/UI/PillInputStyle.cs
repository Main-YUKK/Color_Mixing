using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Gaya input "ganti nama" yang dipakai bersama (Lobby & Saved Files): kartu putih berbentuk pill bergaris hitam,
// teks hitam font Mister Licky Chops, caret hitam terlihat, dan ketikan langsung tampil di kartu
// (kotak input bawaan iOS di atas keyboard disembunyikan).
public static class PillInputStyle
{
    public const string Placeholder = "Ketik nama ruang...";

    // size: ukuran pill dalam unit canvas. Kalau input ada di dalam LayoutGroup, ukuran dikunci lewat LayoutElement;
    // kalau tidak, sizeDelta RectTransform yang diubah.
    public static void Apply(TMP_InputField input, TMP_FontAsset font, Vector2 size, float fontSize,
                             TextAlignmentOptions alignment, float paddingXRatio = 0.45f, float paddingYRatio = 0.05f)
    {
        if (input == null) return;

        // Kartu putih
        if (input.targetGraphic is Image bg)
        {
            bg.color = Color.white;
            if (bg.GetComponent<PillBackground>() == null) bg.gameObject.AddComponent<PillBackground>();
        }

        // Ukuran tetap, supaya kartu tidak melar mengikuti ketikan.
        var rect = (RectTransform)input.transform;
        bool inLayout = rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null;
        if (inLayout)
        {
            var le = input.GetComponent<LayoutElement>();
            if (le == null) le = input.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size.x;
            le.minHeight = le.preferredHeight = size.y;
            le.flexibleWidth = le.flexibleHeight = -1f;
        }
        else
        {
            rect.sizeDelta = size;
        }

        // Area teks: jarak dari tepi pill (kiri/kanan cukup lebar supaya tidak menabrak lengkungan).
        if (input.textViewport != null)
        {
            float padX = size.y * paddingXRatio;
            float padY = size.y * paddingYRatio;
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(padX, padY);
            input.textViewport.offsetMax = new Vector2(-padX, -padY);
            var mask = input.textViewport.GetComponent<RectMask2D>();
            if (mask != null) mask.padding = Vector4.zero;
        }

        if (font == null && input.textComponent != null) font = input.textComponent.font;

        StyleLabel(input.textComponent, font, Color.black, fontSize, alignment);
        StyleLabel(input.placeholder as TMP_Text, font, new Color(0f, 0f, 0f, 0.35f), fontSize, alignment);
        if (input.placeholder is TMP_Text ph) ph.text = Placeholder;

        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;                 // "<b>" yang diketik tampil apa adanya
        input.shouldHideMobileInput = true;     // ketikan tampil langsung di kartu, bukan di kotak iOS
        input.onFocusSelectAll = false;         // caret di akhir teks, bukan blok seleksi semua
        input.customCaretColor = true;
        input.caretColor = Color.black;
        input.caretWidth = 5;
        input.caretBlinkRate = 0.85f;
        input.selectionColor = new Color(0.66f, 0.81f, 1f, 0.75f);
        input.restoreOriginalTextOnEscape = true;
    }

    // Isi teks lalu fokuskan input, caret di akhir teks seperti text field biasa.
    // host: MonoBehaviour aktif untuk menjalankan coroutine satu frame.
    public static void BeginEdit(MonoBehaviour host, TMP_InputField input, string text, int maxLength)
    {
        if (input == null) return;

        text ??= "";
        input.text = text.Length > maxLength ? text.Substring(0, maxLength) : text;
        Canvas.ForceUpdateCanvases();   // layout pill sudah final sebelum keyboard muncul
        input.ActivateInputField();
        if (host != null && host.isActiveAndEnabled) host.StartCoroutine(MoveCaretToEndNextFrame(input));
    }

    // TMP memasang caret/seleksi di frame setelah aktivasi; geser ke akhir teks.
    private static IEnumerator MoveCaretToEndNextFrame(TMP_InputField input)
    {
        yield return null;
        if (input != null && input.isFocused) input.MoveTextEnd(false);
    }

    private static void StyleLabel(TMP_Text label, TMP_FontAsset font, Color color, float fontSize,
                                   TextAlignmentOptions alignment)
    {
        if (label == null) return;

        if (font != null)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;   // material polos (tanpa outline)
        }
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = false;
        label.raycastTarget = false;
    }
}
