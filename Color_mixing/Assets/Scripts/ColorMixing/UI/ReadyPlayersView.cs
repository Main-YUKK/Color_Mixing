using UnityEngine;

// Ikon orang di bawah tombol Mix: jumlah ikon yang tampil = jumlah pemain yang sudah Mix.
public class ReadyPlayersView : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private GameObject[] icons;   // 3 ikon (1 per pemain)

    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    private void Refresh()
    {
        int count = colorMixingController.ReadyCount;
        for (int i = 0; i < icons.Length; i++)
            icons[i].SetActive(i < count);
    }
}