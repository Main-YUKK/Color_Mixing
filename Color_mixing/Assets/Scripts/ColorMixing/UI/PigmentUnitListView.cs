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
        }
    }
}
