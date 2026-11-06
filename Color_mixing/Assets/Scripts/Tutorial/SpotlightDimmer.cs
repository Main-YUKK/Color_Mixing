using UnityEngine;
using UnityEngine.UI;

// Membuat lubang spotlight bulat yang halus di Image dimmer (shader UI/SpotlightDimmer).
// Setiap RectTransform di "holes" jadi satu lubang: posisinya = pusat lingkaran,
// diameternya = sisi terpendek antara Width dan Height. Lubang ikut berubah langsung di Edit Mode.
[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public class SpotlightDimmer : MonoBehaviour, IMaterialModifier
{
    private const int MaxHoles = 8; // Harus sama dengan MAX_HOLES di shader.

    private static readonly int HolesId = Shader.PropertyToID("_Holes");
    private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");

    [Tooltip("RectTransform lubang spotlight (maksimal 8).")]
    [SerializeField] private RectTransform[] holes;

    private readonly Vector4[] holeData = new Vector4[MaxHoles];
    private Graphic graphic;
    private Material instance;

    private void OnEnable()
    {
        graphic = GetComponent<Graphic>();
        graphic.SetMaterialDirty();
    }

    private void OnDisable()
    {
        if (graphic != null)
            graphic.SetMaterialDirty();
        DestroyInstance();
    }

    private void LateUpdate()
    {
        UpdateHoles();
    }

    private void OnValidate()
    {
        if (graphic != null)
            graphic.SetMaterialDirty();
    }

    // Dipanggil Graphic sebelum render: pakai salinan material supaya data lubang tidak tersimpan ke asset .mat.
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || baseMaterial == null)
            return baseMaterial;

        if (instance == null || instance.shader != baseMaterial.shader)
        {
            DestroyInstance();
            instance = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }
        else
        {
            instance.CopyPropertiesFromMaterial(baseMaterial);
        }

        UpdateHoles();
        return instance;
    }

    private void UpdateHoles()
    {
        if (instance == null || graphic == null || graphic.canvas == null)
            return;

        // Vertex UI di shader memakai koordinat lokal canvas, jadi data lubang dikonversi ke ruang yang sama.
        Transform canvasSpace = graphic.canvas.transform;
        int count = 0;

        if (holes != null)
        {
            foreach (RectTransform hole in holes)
            {
                if (count >= MaxHoles)
                    break;
                if (hole == null || !hole.gameObject.activeInHierarchy)
                    continue;

                Rect rect = hole.rect;
                float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                Vector3 center = canvasSpace.InverseTransformPoint(hole.TransformPoint(rect.center));
                Vector3 edge = canvasSpace.InverseTransformPoint(hole.TransformPoint(rect.center + new Vector2(radius, 0f)));

                holeData[count++] = new Vector4(center.x, center.y, Vector2.Distance(center, edge), 0f);
            }
        }

        for (int i = count; i < MaxHoles; i++)
            holeData[i] = Vector4.zero;

        instance.SetVectorArray(HolesId, holeData);
        instance.SetFloat(HoleCountId, count);
    }

    private void DestroyInstance()
    {
        if (instance == null)
            return;

        if (Application.isPlaying)
            Destroy(instance);
        else
            DestroyImmediate(instance);
        instance = null;
    }
}
