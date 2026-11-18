using UnityEngine;
using UnityEngine.UI;

// Pie chart isi mangkuk: 1 irisan (Image Filled, Radial 360) per pigmen, besar irisan = rasio unit.
// Hanya rasio pigmen, BUKAN warna hasil campuran (warna hasil baru tampil di reveal).
public class BowlPieChartView : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private Image[] slices;   // urutan = urutan PigmentPalette

    private void Awake()
    {
        for (int i = 0; i < slices.Length; i++)
            slices[i].color = colorMixingController.Palette.GetPigment(i).color;
    }

    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    private void Refresh()
    {
        var units = colorMixingController.MixingBowl.PigmentUnits;
        int total = colorMixingController.MixingBowl.TotalUnits;

        float start = 0f;   // posisi awal irisan (0-1 dari lingkaran)
        for (int i = 0; i < slices.Length; i++)
        {
            float share = total == 0 ? 0f : (float)units[i] / total;
            slices[i].fillAmount = share;
            slices[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -start * 360f);
            start += share;
        }
    }
}