using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tombol Mix/Un-Mix milik local player (toggle). Disabled saat bowl kosong / fase selesai.
[RequireComponent(typeof(Button))]
public class MixButton : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
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

    private void Toggle() => colorMixingController.SetReady(player, !colorMixingController.IsReady(player));

    private void Refresh()
    {
        bool ready = colorMixingController.IsReady(player);
        label.text = ready ? "Un-Mix" : "Mix";
        button.interactable = !colorMixingController.IsMixConfirmed
                              && (ready || !colorMixingController.MixingBowl.IsEmpty);
    }
}