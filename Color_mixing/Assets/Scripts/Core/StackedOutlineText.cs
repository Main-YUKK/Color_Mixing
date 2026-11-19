using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartoon text border. Back to front:
///   1. shadow  - hard copy of the border, pushed down/right
///   2. plate   - optional extra border outside the stroke (e.g. white for a "sticker" look)
///   3. stroke  - thick line around the letters (merges them into one shape)
///   4. fill    - the letters themselves (the TMP text this sits on)
///
/// Add it to a TextMeshPro - Text (UI). Works in Edit mode.
///
/// The borders are built by stamping the glyphs many times around a circle, NOT with the
/// TMP outline shader, so thickness is not limited by the font atlas padding.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
[DisallowMultipleComponent]
public class StackedOutlineText : MonoBehaviour
{
    [Header("Colors")]
    public Color fillColor = Color.white;
    public Color strokeColor = Color.black;
    public Color plateColor = Color.white;
    public Color shadowColor = Color.black;

    [Header("Sizes (fraction of font size)")]
    [Tooltip("Thick line around the letters.")]
    [Range(0f, 0.3f)] public float strokeThickness = 0.12f;
    [Tooltip("Extra border outside the stroke. 0 = off.")]
    [Range(0f, 0.6f)] public float plateThickness = 0f;
    [Tooltip("Where the hard shadow goes. X = right, Y = up (negative = down).")]
    public Vector2 shadowOffset = new Vector2(0.02f, -0.07f);

    const int MaxVerts = 60000; // UI meshes are 16-bit

    TextMeshProUGUI _front;
    StackedOutlineLayer _back;
    Material _mat;
    Material _matSource;
    bool _dirty;

    // source glyph quads
    readonly List<Vector3> _sv = new List<Vector3>();
    readonly List<Vector3> _sn = new List<Vector3>();
    readonly List<Vector4> _st = new List<Vector4>();
    readonly List<Vector4> _suv0 = new List<Vector4>();
    readonly List<Vector4> _suv1 = new List<Vector4>();
    readonly List<int> _quads = new List<int>();

    // output (handed to the layer)
    readonly List<UIVertex> _verts = new List<UIVertex>();
    readonly List<int> _indices = new List<int>();

    void OnEnable()
    {
        _front = GetComponent<TextMeshProUGUI>();
        _front.color = fillColor;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        _dirty = true;
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (_back != null) Kill(_back.gameObject);
        Kill(_mat);
        _back = null; _mat = null; _matSource = null;
    }

    void OnValidate()
    {
        if (_front != null) _front.color = fillColor;
        _dirty = true;
    }

    // Can fire in the middle of a canvas rebuild: only flag it, never touch the layer here.
    void OnTextChanged(Object obj)
    {
        if (obj != _front) return;
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void LateUpdate()
    {
        if (_front == null || transform.parent == null) return;

        EnsureBack();
        EnsureMaterial();
        FollowFront();

        // text was changed this frame (e.g. from Update): build it now so the border isn't a frame late
        if (_front.havePropertiesChanged) _front.ForceMeshUpdate();

        if (_dirty)
        {
            _dirty = false;
            Rebuild();
        }
    }

    // ------------------------------------------------------------------ setup

    void EnsureBack()
    {
        if (_back != null) return;

        var go = new GameObject(name + " (Stacked Outline)", typeof(RectTransform), typeof(CanvasRenderer));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = gameObject.layer;
        go.transform.SetParent(transform.parent, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        _back = go.AddComponent<StackedOutlineLayer>();
        _back.raycastTarget = false;
        _matSource = null; // re-apply material to the new layer
        _dirty = true;
    }

    void EnsureMaterial()
    {
        var src = _front.fontSharedMaterial;
        if (src == null || (src == _matSource && _mat != null)) return;

        Kill(_mat);
        _matSource = src;
        _mat = new Material(src) { hideFlags = HideFlags.HideAndDontSave };

        // plain face only: color comes from the vertex colors we write
        if (_mat.HasProperty(ShaderUtilities.ID_FaceColor)) _mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        if (_mat.HasProperty(ShaderUtilities.ID_OutlineWidth)) _mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        if (_mat.HasProperty(ShaderUtilities.ID_FaceDilate)) _mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        _mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        _mat.DisableKeyword(ShaderUtilities.Keyword_Glow);
        _mat.DisableKeyword(ShaderUtilities.Keyword_Bevel);

        _back.SetAtlas(_mat.HasProperty(ShaderUtilities.ID_MainTex) ? _mat.GetTexture(ShaderUtilities.ID_MainTex) : null);
        _back.material = _mat;
        _dirty = true;
    }

    // Keep the hidden layer exactly on the text's rect and directly behind it in draw order.
    void FollowFront()
    {
        var src = (RectTransform)transform;
        var rt = (RectTransform)_back.transform;

        if (rt.parent != src.parent) rt.SetParent(src.parent, false);

        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta;
        rt.anchoredPosition3D = src.anchoredPosition3D;
        rt.localRotation = src.localRotation;
        rt.localScale = src.localScale;

        int fi = src.GetSiblingIndex();
        int bi = rt.GetSiblingIndex();
        if (bi != fi - 1) rt.SetSiblingIndex(bi < fi ? fi - 1 : fi);

        if (_back.enabled != _front.enabled) _back.enabled = _front.enabled;
    }

    // ------------------------------------------------------------------ geometry

    void Rebuild()
    {
        if (_back == null) return;

        _verts.Clear(); _indices.Clear(); _quads.Clear();

        Mesh source = _front.mesh;

        // Teks kosong: TMP tidak membersihkan mesh-nya, glyph lama masih ada di sana. Jangan dipakai.
        var info = _front.textInfo;
        int used = string.IsNullOrEmpty(_front.text) || info == null || info.characterCount == 0 || info.meshInfo.Length == 0
            ? 0
            : info.meshInfo[0].vertexCount;

        if (source != null && used >= 4)
        {
            source.GetVertices(_sv);
            source.GetNormals(_sn);
            source.GetTangents(_st);
            source.GetUVs(0, _suv0);
            source.GetUVs(1, _suv1);

            // TMP keeps spare, zeroed quads in its mesh: only keep the real ones
            // hanya quad milik teks sekarang, bukan sisa teks sebelumnya yang lebih panjang
            for (int i = 0; i + 3 < used && i + 3 < _sv.Count; i += 4)
                if ((_sv[i] - _sv[i + 2]).sqrMagnitude > 1e-8f) _quads.Add(i);
        }

        if (_quads.Count > 0)
        {
            float em = _front.fontSize;
            float alpha = _front.alpha;
            float stroke = strokeThickness * em;
            float plate = stroke + plateThickness * em;
            Vector2 shadow = shadowOffset * em;

            if (shadowColor.a > 0f && shadow != Vector2.zero) Stamp(plate, shadow, shadowColor, alpha, em);
            if (plateThickness > 0f && plateColor.a > 0f) Stamp(plate, Vector2.zero, plateColor, alpha, em);
            if (strokeThickness > 0f && strokeColor.a > 0f) Stamp(stroke, Vector2.zero, strokeColor, alpha, em);
        }

        _back.SetGeometry(_verts, _indices);
    }

    // Fills a disc of radius `radius` with copies of the glyphs = the glyphs grown by `radius`.
    void Stamp(float radius, Vector2 offset, Color color, float alpha, float em)
    {
        color.a *= alpha;
        Color32 c32 = color;

        if (!AddCopy(offset, c32) || radius <= 0f) return;

        float step = Mathf.Max(em * 0.05f, 0.5f);         // gap between stamps that still overlaps letter strokes
        float tolerance = Mathf.Max(em * 0.004f, 0.15f);  // how bumpy the outer edge may be
        int rings = Mathf.CeilToInt(radius / step);

        for (int k = 1; k <= rings; k++)
        {
            float r = radius * k / rings;
            int n = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / step));
            if (k == rings)
            {
                float smooth = Mathf.PI / Mathf.Acos(1f - Mathf.Min(0.9f, tolerance / r));
                n = Mathf.Clamp(Mathf.CeilToInt(smooth), n, 72);
            }

            float phase = (k & 1) == 0 ? Mathf.PI / n : 0f;
            for (int i = 0; i < n; i++)
            {
                float a = phase + 2f * Mathf.PI * i / n;
                if (!AddCopy(offset + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c32)) return;
            }
        }
    }

    bool AddCopy(Vector2 delta, Color32 color)
    {
        if (_verts.Count + _quads.Count * 4 > MaxVerts) return false;

        var d = new Vector3(delta.x, delta.y, 0f);
        bool hasN = _sn.Count == _sv.Count;
        bool hasT = _st.Count == _sv.Count;
        bool hasUv0 = _suv0.Count == _sv.Count;
        bool hasUv1 = _suv1.Count == _sv.Count;

        for (int q = 0; q < _quads.Count; q++)
        {
            int s = _quads[q];
            int b = _verts.Count;
            for (int j = 0; j < 4; j++)
            {
                var v = UIVertex.simpleVert;
                v.position = _sv[s + j] + d;
                if (hasN) v.normal = _sn[s + j];
                if (hasT) v.tangent = _st[s + j];
                if (hasUv0) v.uv0 = _suv0[s + j];
                if (hasUv1) v.uv1 = _suv1[s + j];
                v.color = color;
                _verts.Add(v);
            }
            _indices.Add(b); _indices.Add(b + 1); _indices.Add(b + 2);
            _indices.Add(b + 2); _indices.Add(b + 3); _indices.Add(b);
        }
        return true;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
