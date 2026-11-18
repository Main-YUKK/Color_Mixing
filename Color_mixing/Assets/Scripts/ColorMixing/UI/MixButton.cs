using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tombol Campur / Batal Campur milik local player (toggle). Disabled saat bowl kosong / fase selesai.
// Tampilan Campur = sprite & Sprite State yang terpasang di Image/Button (Inspector). Batal Campur = field cancel di bawah.
[RequireComponent(typeof(Button))]
public class MixButton : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    private int Player => LocalPlayer.Index(player);   // nomor pemain di device ini
    [SerializeField] private TMP_Text label;
    [SerializeField] private string mixText = "Campur";
    [SerializeField] private string cancelText = "Batal Campur";
    [SerializeField] private Sprite cancelSprite;                  // BatalCampur
    [SerializeField] private SpriteState cancelSpriteState;        // sprite pressed/disabled saat Batal Campur (kosong = tidak ganti)

    private Button button;
    private Image image;
    private Sprite mixSprite;
    private SpriteState mixSpriteState;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        mixSprite = image.sprite;                 // tampilan Campur diambil dari Inspector
        mixSpriteState = button.spriteState;
        button.onClick.AddListener(Toggle);
    }

    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    private void Toggle() => colorMixingController.SetReady(Player, !colorMixingController.IsReady(Player));

    private void Refresh()
    {
        bool ready = colorMixingController.IsReady(Player);
        label.text = ready ? cancelText : mixText;
        image.sprite = ready && cancelSprite != null ? cancelSprite : mixSprite;
        button.spriteState = ready ? cancelSpriteState : mixSpriteState;
        button.interactable = colorMixingController.IsSynced
                              && !colorMixingController.IsMixConfirmed
                              && (ready || !colorMixingController.MixingBowl.IsEmpty);
    }
}
