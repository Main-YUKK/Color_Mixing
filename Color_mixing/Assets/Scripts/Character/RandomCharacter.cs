using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomCharacter : MonoBehaviour
{
    [Serializable]
    public class HairStyle
    {
        public Sprite sprite;
        public Vector2 position;                // local position relative to the duck
        public float scale = 1f;
        public HairLayer layer;                 // Back is the default, so existing styles keep behaving the same
    }

    public enum HairLayer { Back, Front, Random }

    [Serializable]
    public class ColorVariant
    {
        public Sprite[] sprites;
    }

    [Header("References")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer hairRenderer;

    [Header("Hair options")]
    [Tooltip("Drop all 8 sliced sprites here, then use the context menu 'Build Styles From Sprites'.")]
    public Sprite[] hairSprites;
    public HairStyle[] hairStyles;
    [Tooltip("Optional: the other color sheets. Each one needs its sprites in the same order (_0 to _7) as Hair Sprites.")]
    public ColorVariant[] colorVariants;
    [Tooltip("Turn off when using color variants, so the gradient tint isn't multiplied on top of the sheet's own color.")]
    public bool tintHair = true;
    public Gradient hairColors;                // random color is sampled from this
    public float positionJitter = 0.05f;        // small random wobble on top of each style's position
    public int hairSortingOffset = -1;          // sorting order offset used for "Back" hair
    public int frontSortingOffset = 1;          // sorting order offset used for "Front" hair

    [Header("Behaviour")]
    [Tooltip("Off: hair is chosen once when the character appears and stays. On: hair keeps coming and going on the timers below.")]
    public bool cycleHair = false;

    [Header("Timing (seconds, only used when Cycle Hair is on)")]
    public Vector2 hairVisibleTime = new Vector2(2f, 6f);
    public Vector2 hairHiddenTime = new Vector2(1f, 3f);
    public float popDuration = 0.25f;

    [Header("Tuning (edit mode)")]
    [Tooltip("Pick a style here to preview it on the duck, move/scale the Hair object, then use the context menu 'Save Hair Transform To Style'.")]
    public int previewIndex;

    float currentScale = 1f;
    int currentIndex;

    void Awake()
    {
        if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        hairRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        hairRenderer.transform.localScale = Vector3.zero;
    }

    void Start()
    {
        if (cycleHair) StartCoroutine(HairLoop());
        else Randomize();
    }

    /// <summary>Re-roll the whole character right now (also callable from a button/UnityEvent).</summary>
    [ContextMenu("Randomize Now")]
    public void Randomize()
    {
        RandomizeHair();
        hairRenderer.transform.localScale = Vector3.one * currentScale;
    }

    // Same shape (same index), random color sheet. Falls back to the style's own sprite if a variant is missing.
    Sprite PickSprite(int index)
    {
        var s = hairStyles[index].sprite;
        int count = (colorVariants?.Length ?? 0) + 1;
        int pick = Random.Range(0, count);
        if (pick == 0) return s;
        var v = colorVariants[pick - 1].sprites;
        return v != null && index < v.Length && v[index] != null ? v[index] : s;
    }

    void ApplyStyle(int index, float jitter)
    {
        currentIndex = index;
        var s = hairStyles[index];
        hairRenderer.sprite = PickSprite(index);
        currentScale = s.scale;
        bool front = s.layer == HairLayer.Front || (s.layer == HairLayer.Random && Random.value > 0.5f);
        hairRenderer.sortingOrder = bodyRenderer.sortingOrder + (front ? frontSortingOffset : hairSortingOffset);
        hairRenderer.transform.localPosition = new Vector3(
            s.position.x + Random.Range(-jitter, jitter),
            s.position.y + Random.Range(-jitter, jitter), 0f);
    }

    void RandomizeHair()
    {
        if (hairStyles == null || hairStyles.Length == 0) return;
        ApplyStyle(Random.Range(0, hairStyles.Length), positionJitter);
        if (!tintHair) hairRenderer.color = Color.white;
        else hairRenderer.color = hairColors != null ? hairColors.Evaluate(Random.value) : Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.6f, 1f);
    }

    /// <summary>Tint the hair with a hex string like "FF8800" or "#FF8800". Returns false if it isn't valid.</summary>
    public bool SetHairColor(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (!ColorUtility.TryParseHtmlString(hex, out Color c)) return false;
        hairRenderer.sprite = hairStyles[currentIndex].sprite;   // base (white) sheet, so the tint gives the exact color
        hairRenderer.color = c;
        return true;
    }

    IEnumerator HairLoop()
    {
        while (true)
        {
            // hair comes
            RandomizeHair();
            yield return Scale(0f, 1f);
            yield return new WaitForSeconds(Random.Range(hairVisibleTime.x, hairVisibleTime.y));

            // hair goes
            yield return Scale(1f, 0f);
            yield return new WaitForSeconds(Random.Range(hairHiddenTime.x, hairHiddenTime.y));
        }
    }

    IEnumerator Scale(float from, float to)
    {
        for (float t = 0; t < popDuration; t += Time.deltaTime)
        {
            hairRenderer.transform.localScale = Vector3.one * (currentScale * Mathf.SmoothStep(from, to, t / popDuration));
            yield return null;
        }
        hairRenderer.transform.localScale = Vector3.one * (currentScale * to);
    }

#if UNITY_EDITOR
    // Show the selected style on the duck while editing (not playing).
    void OnValidate()
    {
        if (Application.isPlaying || hairRenderer == null || hairStyles == null || hairStyles.Length == 0) return;
        previewIndex = Mathf.Clamp(previewIndex, 0, hairStyles.Length - 1);
        var s = hairStyles[previewIndex];
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (hairRenderer == null) return;
            hairRenderer.sprite = s.sprite;
            hairRenderer.sortingOrder = bodyRenderer.sortingOrder + (s.layer == HairLayer.Front ? frontSortingOffset : hairSortingOffset);
            hairRenderer.transform.localPosition = new Vector3(s.position.x, s.position.y, 0f);
            hairRenderer.transform.localScale = Vector3.one * s.scale;
        };
    }

    // Creates one HairStyle per sprite in hairSprites, keeping saved position/scale for sprites already listed.
    [ContextMenu("Build Styles From Sprites")]
    void BuildStyles()
    {
        UnityEditor.Undo.RecordObject(this, "Build hair styles");
        var old = hairStyles ?? new HairStyle[0];
        hairStyles = new HairStyle[hairSprites.Length];
        for (int i = 0; i < hairSprites.Length; i++)
        {
            var existing = Array.Find(old, s => s.sprite == hairSprites[i]);
            hairStyles[i] = existing ?? new HairStyle { sprite = hairSprites[i] };
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Move/scale the Hair object in the Scene view, then run this to store it in the previewed style.
    [ContextMenu("Save Hair Transform To Style")]
    void SaveHairTransform()
    {
        UnityEditor.Undo.RecordObject(this, "Save hair transform");
        var s = hairStyles[previewIndex];
        s.position = hairRenderer.transform.localPosition;
        s.scale = hairRenderer.transform.localScale.x;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
