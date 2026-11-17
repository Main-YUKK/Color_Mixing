using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Daftar jumlah unit tiap pigmen di layar reveal: tetes warna + angka. Satu baris per pigmen (urutan = index palet).
public class PigmentUnitListView : MonoBehaviour
{
    [Serializable]
    private struct Row
    {
        public Image icon;
        public TMP_Text count;
    }

    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private Row[] rows;

    // Tata letak yang kamu atur di scene untuk tiap baris (baris + Icon + Count di dalamnya) = slot ke-1, 2, 3...
    // mengikuti urutan Hierarchy. Pigmen yang dipakai mengisi slot berurutan, jadi 2 warna selalu di slot 1 dan 2.
    private struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public static Pose Of(Transform t) => new Pose { position = t.localPosition, rotation = t.localRotation };
        public void ApplyTo(Transform t) => t.SetLocalPositionAndRotation(position, rotation);
    }

    private struct Slot { public Pose row, icon, count; }

    private int[] displayOrder;   // index rows[] diurutkan sesuai Hierarchy
    private Slot[] slots;

    private void Awake()
    {
        displayOrder = new int[rows.Length];
        for (int i = 0; i < rows.Length; i++) displayOrder[i] = i;
        Array.Sort(displayOrder, (a, b) => RowOf(a).GetSiblingIndex().CompareTo(RowOf(b).GetSiblingIndex()));

        slots = new Slot[rows.Length];
        for (int s = 0; s < slots.Length; s++)
        {
            int i = displayOrder[s];
            slots[s] = new Slot
            {
                row = Pose.Of(RowOf(i)),
                icon = Pose.Of(rows[i].icon.transform),
                count = Pose.Of(rows[i].count.transform),
            };
        }
    }

    private Transform RowOf(int i) => rows[i].icon.transform.parent;

    private void OnEnable()
    {
        colorMixingController.StateChanged += Refresh;
        Refresh();   // panel reveal baru dinyalakan saat voting, isi mangkuk sudah final
    }

    private void OnDisable() => colorMixingController.StateChanged -= Refresh;

    private void Refresh()
    {
        var units = colorMixingController.MixingBowl.PigmentUnits;
        for (int i = 0; i < rows.Length && i < units.Count; i++)
        {
            Sprite icon = colorMixingController.Palette.GetPigment(i).unitIcon;
            if (icon != null) rows[i].icon.sprite = icon;
            rows[i].count.text = units[i].ToString();
            rows[i].icon.transform.parent.gameObject.SetActive(units[i] > 0);   // pigmen tidak dipakai disembunyikan
        }

        int slot = 0;
        foreach (int i in displayOrder)
        {
            if (!RowOf(i).gameObject.activeSelf) continue;
            slots[slot].row.ApplyTo(RowOf(i));
            slots[slot].icon.ApplyTo(rows[i].icon.transform);
            slots[slot].count.ApplyTo(rows[i].count.transform);
            slot++;
        }
    }
}
