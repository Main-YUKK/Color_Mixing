using UnityEngine;
using UnityEngine.UI;

// Foto klien untuk UI: salinan karakter klien aktif (prefab + rambut + garis tepi, sama persis dengan di ClientArrival)
// dipasang jauh di luar layar, direkam kamera khusus ke RenderTexture, lalu ditampilkan di RawImage.
// Semua RawImage di Views memakai RenderTexture yang sama (mis. tombol kecil dan kartu besar).
public class ClientPortrait : MonoBehaviour
{
    [SerializeField] private RawImage[] views;
    [Tooltip("Dipakai kalau CharacterSpawner tidak ada (ColorMixing dites sendirian). Urutan sama dengan Character Prefabs di spawner.")]
    [SerializeField] private GameObject[] fallbackPrefabs;
    [Tooltip("Tes tanpa ClientArrival: index di Fallback Prefabs yang ditampilkan. -1 = acak.")]
    [SerializeField] private int testPrefabIndex = -1;
    [SerializeField] private int textureSize = 512;

    [Header("Framing (bisa diubah saat Play)")]
    [Tooltip("Makin kecil makin dekat (zoom in).")]
    [SerializeField] private float zoom = 3.4f;
    [Tooltip("Hanya untuk prefab TANPA child 'PortraitFocus': geser bidikan relatif ke pivot karakter, dalam unit dunia.")]
    [SerializeField] private Vector2 offset = new Vector2(0.5f, 3.2f);
    [SerializeField] private bool faceRight = true;

    private static readonly Vector3 Stage = new Vector3(1000f, 1000f, 0f);   // jauh dari kamera lain
    // Child kosong di prefab klien, diletakkan di tengah wajah: kamera membidik titik ini, jadi tiap karakter pas.
    private const string FocusName = "PortraitFocus";

    private Camera cam;
    private RenderTexture texture;
    private GameObject copy;
    private Transform focus;

    private void Start()
    {
        copy = SpawnCopy();
        if (copy == null) return;
        focus = copy.transform.Find(FocusName);

        texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32) { name = "ClientPortrait" };
        cam = new GameObject("ClientPortraitCamera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.targetTexture = texture;
        foreach (var v in views) if (v != null) v.texture = texture;
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        cam.orthographicSize = zoom;
        Vector3 target = focus != null ? focus.position : Stage + (Vector3)offset;   // focus ikut flip karakter
        cam.transform.position = new Vector3(target.x, target.y, Stage.z - 10f);
    }

    private GameObject SpawnCopy()
    {
        var spawner = FindFirstObjectByType<CharacterSpawner>();
        var prefabs = spawner != null ? spawner.characterPrefabs : fallbackPrefabs;
        if (prefabs == null || prefabs.Length == 0) return null;

        // Tanpa spawner (ColorMixing dites sendirian): CurrentLook bisa sisa Play sebelumnya (Reload Domain mati),
        // jadi abaikan dan pakai Test Prefab Index. Rambut diacak sendiri oleh RandomCharacter.
        var look = spawner != null ? GameSession.CurrentLook : null;
        if (look == null || !look.valid || look.prefabIndex < 0 || look.prefabIndex >= prefabs.Length)
        {
            int index = testPrefabIndex >= 0 && testPrefabIndex < prefabs.Length ? testPrefabIndex : Random.Range(0, prefabs.Length);
            look = new ClientLook { valid = true, prefabIndex = index };
        }

        var prefab = prefabs[look.prefabIndex];
        if (prefab == null) return null;

        var go = Instantiate(prefab, Stage, Quaternion.identity);
        go.name = prefab.name + " (Portrait)";
        if (look.hasHair && go.TryGetComponent(out RandomCharacter hair)) hair.ApplyLook(look.hair);

        // Prefab menghadap arah datang (kiri); di ClientArrival dibalik saat tiba.
        if (faceRight)
        {
            var s = go.transform.localScale;
            s.x = -s.x;
            go.transform.localScale = s;
        }
        return go;
    }

    private void OnDestroy()
    {
        if (copy != null) Destroy(copy);
        if (cam != null) Destroy(cam.gameObject);
        if (texture != null) texture.Release();
    }
}
