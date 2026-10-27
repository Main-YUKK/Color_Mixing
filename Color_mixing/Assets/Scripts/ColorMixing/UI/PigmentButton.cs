using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tombol pigmen milik 1 pemain + 1 slot.
// Tap = 1 unit, tahan = +1 unit tiap holdInterval. Tampilan (nama, warna, disabled) ikut state controller.
[RequireComponent(typeof(Button))]
public class PigmentButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // 0 = P1
    [SerializeField, Range(0, 1)] private int slot;
    [SerializeField] private float holdInterval = 0.4f;  // jeda antar unit saat ditahan
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color disabledTextColor = new Color(0.27f, 0.27f, 0.27f);   // teks abu saat disabled

    private Button button;
    private bool holding;
    private float holdTimer;
    private Color normalTextColor;

    private void Awake()
    {
        button = GetComponent<Button>();
        normalTextColor = label.color;   // simpan warna teks dari Inspector
    }
    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    // MARK: TAP: unit pertama, langsung masuk saat jari menyentuh tombol 
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        colorMixingController.AddPigment(player, slot);   // 1 unit (tap)
        holding = true;                        // mulai "mode tahan"
        holdTimer = holdInterval;              // hitung mundur 
    }

    // MARK: HOLD: selama masih ditahan, +1 unit setiap holdInterval 
    private void Update()
    {
        if (!holding) return;                                      // tidak ditahan → tidak ada apa-apa
        if (!button.interactable) { holding = false; return; }

        holdTimer -= Time.deltaTime;                               // hitung mundur
        if (holdTimer > 0f) return;
        colorMixingController.AddPigment(player, slot);            // +1 unit (hold)
        holdTimer += holdInterval;                                 // mulai hitung lagi
    }

    // MARK: SELESAI: jari diangkat, hold berhenti 
    public void OnPointerUp(PointerEventData eventData) => holding = false;
    private void Refresh()
    {
        label.text = colorMixingController.Palette.GetPigment(colorMixingController.GetPigment(player, slot)).pigmentName;
        button.interactable = !colorMixingController.IsReady(player);
        label.color = button.interactable ? normalTextColor : disabledTextColor;
    }
}