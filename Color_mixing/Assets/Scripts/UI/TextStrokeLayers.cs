using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Garis tepi ganda ala desain hi-fi: isi huruf -> garis dalam (putih) -> garis luar (hitam).
//
// Outline bawaan TMP digambar per huruf, jadi kalau garisnya tebal, garis huruf berikutnya menimpa
// huruf sebelumnya. Di desain, garis putih semua huruf MENYATU lalu dibungkus satu garis hitam.
// Komponen ini menirunya dengan 3 salinan teks yang ditumpuk (dibuat otomatis sebagai child tersembunyi,
// tidak ikut tersimpan ke scene/prefab):
//   1. lapisan luar  : huruf digemukkan sebesar innerWidth + outerWidth, warna outerColor
//   2. lapisan dalam : huruf digemukkan sebesar innerWidth, warna innerColor
//   3. lapisan isi   : huruf apa adanya, warna teks aslinya
//
// Cara pakai: pasang di objek TextMeshProUGUI. Skrip lain tetap cukup mengubah .text di teks aslinya.
// Lebar garis ditulis dalam unit canvas (pada reference resolution 2622x1206: 1 pt Figma = 3 unit).
//
// PENTING: font asset teksnya harus punya padding atlas besar ("... SDF - Stroke"). Lebar garis total
// maksimum kira-kira = padding / samplingPointSize * fontSize. Kalau lebih dari itu, garis dipotong
// dan muncul warning di Console.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextStrokeLayers : MonoBehaviour
{
    private const string LayerPrefix = "__StrokeLayer";

    [Header("Garis dalam (menempel di huruf)")]
    [Tooltip("Lebar dalam unit canvas.")]
    [SerializeField, Min(0f)] private float innerWidth = 6f;
    [SerializeField] private Color innerColor = Color.white;

    [Header("Garis luar (membungkus garis dalam)")]
    [Tooltip("Lebar dalam unit canvas.")]
    [SerializeField, Min(0f)] private float outerWidth = 6f;
    [SerializeField] private Color outerColor = Color.black;

    private TextMeshProUGUI source;
    private TextMeshProUGUI outerLayer;
    private TextMeshProUGUI innerLayer;
    private TextMeshProUGUI faceLayer;

    private Material baseMaterial;    // material teks asli yang jadi cetakan
    private Material outerMaterial;
    private Material innerMaterial;
    private bool warnedClamp;

    private void OnEnable()
    {
        source = GetComponent<TextMeshProUGUI>();
        RemoveStaleLayers();

        outerLayer = CreateLayer("Outer");
        innerLayer = CreateLayer("Inner");
        faceLayer = CreateLayer("Face");

        Sync();
    }

    private void OnDisable()
    {
        DestroySafe(outerLayer != null ? outerLayer.gameObject : null);
        DestroySafe(innerLayer != null ? innerLayer.gameObject : null);
        DestroySafe(faceLayer != null ? faceLayer.gameObject : null);
        DestroySafe(outerMaterial);
        DestroySafe(innerMaterial);

        outerLayer = innerLayer = faceLayer = null;
        outerMaterial = innerMaterial = null;
        baseMaterial = null;
    }

    // LateUpdate jalan sebelum canvas di-rebuild, jadi perubahan teks di frame ini langsung ikut tergambar.
    private void LateUpdate()
    {
        Sync();
    }

    private void Sync()
    {
        if (source == null || outerLayer == null || innerLayer == null || faceLayer == null) return;

        TMP_FontAsset font = source.font;
        Material material = source.fontSharedMaterial;
        if (font == null || material == null) return;

        if (material != baseMaterial || outerMaterial == null || innerMaterial == null)
            RebuildMaterials(material);

        bool outerChanged = ApplyDilate(outerMaterial, font, innerWidth + outerWidth);
        bool innerChanged = ApplyDilate(innerMaterial, font, innerWidth);

        CopySettings(outerLayer, font, outerMaterial, WithSourceAlpha(outerColor), true);
        CopySettings(innerLayer, font, innerMaterial, WithSourceAlpha(innerColor), true);
        CopySettings(faceLayer, font, material, source.color, false);

        // Lebar garis berubah -> ukuran quad tiap huruf harus dihitung ulang supaya garis tidak terpotong.
        if (outerChanged) RefreshPadding(outerLayer);
        if (innerChanged) RefreshPadding(innerLayer);
    }

    private static void RefreshPadding(TextMeshProUGUI layer)
    {
        layer.UpdateMeshPadding();
        layer.SetVerticesDirty();
    }

    private void RebuildMaterials(Material material)
    {
        DestroySafe(outerMaterial);
        DestroySafe(innerMaterial);

        baseMaterial = material;
        outerMaterial = CreateStrokeMaterial(material, "Outer");
        innerMaterial = CreateStrokeMaterial(material, "Inner");
        warnedClamp = false;
    }

    private static Material CreateStrokeMaterial(Material template, string suffix)
    {
        var material = new Material(template)
        {
            name = template.name + " (" + suffix + " Stroke)",
            hideFlags = HideFlags.HideAndDontSave
        };

        // Lapisan garis hanya butuh isi huruf polos; warnanya datang dari vertex color.
        material.DisableKeyword(ShaderUtilities.Keyword_Outline);
        material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        material.DisableKeyword(ShaderUtilities.Keyword_Glow);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        return material;
    }

    // Mengubah lebar (unit canvas) menjadi _FaceDilate. Return true kalau nilainya berubah.
    //
    // Di atlas SDF TMP, FaceDilate = 1 menggeser tepi huruf sejauh (GradientScale - 1) texel = padding atlas
    // (selama bobot bold + dilate <= 1, sehingga _ScaleRatioA tidak ikut mengecil).
    private bool ApplyDilate(Material material, TMP_FontAsset font, float widthInCanvasUnits)
    {
        float gradientScale = material.GetFloat(ShaderUtilities.ID_GradientScale);
        float atlasPadding = Mathf.Max(1f, gradientScale - 1f);

        float fontSize = Mathf.Max(0.01f, source.fontSize);
        float texelsPerUnit = font.faceInfo.pointSize / (fontSize * Mathf.Max(0.0001f, font.faceInfo.scale));
        float dilate = widthInCanvasUnits * texelsPerUnit / atlasPadding;

        float boldWeight = Mathf.Max(material.GetFloat(ShaderUtilities.ID_WeightNormal),
                                     material.GetFloat(ShaderUtilities.ID_WeightBold)) / 4f;
        float maxDilate = Mathf.Max(0f, 1f - boldWeight);

        if (dilate > maxDilate)
        {
            if (!warnedClamp)
            {
                warnedClamp = true;
                float maxWidth = maxDilate * atlasPadding / texelsPerUnit;
                Debug.LogWarning($"[TextStrokeLayers] Garis {widthInCanvasUnits:0.#} unit terlalu tebal untuk font asset " +
                                 $"'{font.name}' pada ukuran {fontSize:0.#} (maksimum {maxWidth:0.#} unit). " +
                                 "Pakai font asset dengan padding atlas lebih besar.", this);
            }
            dilate = maxDilate;
        }

        if (Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_FaceDilate), dilate)) return false;

        material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
        ShaderUtilities.UpdateShaderRatios(material);
        return true;
    }

    private void CopySettings(TextMeshProUGUI layer, TMP_FontAsset font, Material material, Color color, bool isStroke)
    {
        // Urutan penting: mengganti font me-reset material ke material bawaan font.
        if (layer.font != font) layer.font = font;
        if (layer.fontSharedMaterial != material) layer.fontSharedMaterial = material;
        if (layer.text != source.text) layer.text = source.text;

        layer.richText = source.richText;
        layer.parseCtrlCharacters = source.parseCtrlCharacters;
        layer.isRightToLeftText = source.isRightToLeftText;
        layer.fontStyle = source.fontStyle;
        layer.fontWeight = source.fontWeight;
        layer.alignment = source.alignment;
        layer.textWrappingMode = source.textWrappingMode;
        layer.overflowMode = source.overflowMode;
        layer.characterSpacing = source.characterSpacing;
        layer.wordSpacing = source.wordSpacing;
        layer.lineSpacing = source.lineSpacing;
        layer.paragraphSpacing = source.paragraphSpacing;
        layer.margin = source.margin;
        layer.extraPadding = source.extraPadding;
        layer.maskable = source.maskable;

        // Auto-size disalin apa adanya: rect + pengaturan sama -> hasil ukuran sama dengan teks asli.
        layer.enableAutoSizing = source.enableAutoSizing;
        if (source.enableAutoSizing)
        {
            layer.fontSizeMin = source.fontSizeMin;
            layer.fontSizeMax = source.fontSizeMax;
        }
        else
        {
            layer.fontSize = source.fontSize;
        }

        layer.color = color;
        layer.overrideColorTags = isStroke;   // tag <color> di teks tidak boleh mewarnai garis

        RectTransform sourceRect = source.rectTransform;
        RectTransform layerRect = layer.rectTransform;
        if (layerRect.pivot != sourceRect.pivot) layerRect.pivot = sourceRect.pivot;
    }

    private Color WithSourceAlpha(Color color)
    {
        color.a *= source.color.a;
        return color;
    }

    private TextMeshProUGUI CreateLayer(string suffix)
    {
        var go = new GameObject(LayerPrefix + suffix, typeof(RectTransform))
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = gameObject.layer
        };

        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layer = go.AddComponent<TextMeshProUGUI>();
        layer.raycastTarget = false;
        layer.fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>(source.fontFeatures);
        return layer;
    }

    // Sisa lapisan dari sesi sebelumnya (mis. setelah domain reload) dibuang dulu supaya tidak dobel.
    private void RemoveStaleLayers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(LayerPrefix))
                DestroySafe(child.gameObject);
        }
    }

    private static void DestroySafe(Object target)
    {
        if (target == null) return;

        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
