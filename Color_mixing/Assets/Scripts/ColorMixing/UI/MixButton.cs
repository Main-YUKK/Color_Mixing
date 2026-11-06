using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tombol Mix/Un-Mix milik local player (toggle). Disabled saat bowl kosong / fase selesai.
[RequireComponent(typeof(Button))]
public class MixButton : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    private int Player => LocalPlayer.Index(player);   // nomor pemain di device ini
    [SerializeField] private TMP_Text label;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Toggle);
    }

    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    private void Toggle() => colorMixingController.SetReady(Player, !colorMixingController.IsReady(Player));

    private void Refresh()
    {
        bool ready = colorMixingController.IsReady(Player);
        label.text = ready ? "Un-Mix" : "Mix";
        button.interactable = colorMixingController.IsSynced
                              && !colorMixingController.IsMixConfirmed
                              && (ready || !colorMixingController.MixingBowl.IsEmpty);
    }
}