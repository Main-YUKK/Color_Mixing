using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tombol pigmen milik 1 pemain + 1 slot. Sumber nilai player/slot untuk PigmentSlotView di atasnya.
// Tap = 1 unit, tahan = +1 unit tiap holdInterval. Gambar icon diatur PigmentSlotView (ikut animasi rotasi), tombol ini hanya menggelapkannya saat disabled.
[RequireComponent(typeof(Button))]
public class PigmentButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // 0 = P1
    [SerializeField, Range(0, 1)] private int slot;
    [SerializeField] private float holdInterval = 0.2f;  // jeda antar unit saat ditahan
    [SerializeField] private Image icon;                 // splat warna pigmen
    [SerializeField] private Color disabledIconColor = new Color(0.5f, 0.5f, 0.5f);   // icon digelapkan saat disabled

    private Button button;
    private bool holding;
    private float holdTimer;
    private bool locked;

    public ColorMixingController Controller => colorMixingController;
    public int Player => LocalPlayer.Index(player);   // nomor pemain di device ini
    public int Slot => slot;
    public Image Icon => icon;
    public bool IsHeld => holding;
    public event Action Poured;   // tiap 1 unit dari tombol ini (tap / tahan), untuk animasi tetesan

    // Dikunci selama animasi rotasi: pigmen di state sudah baru, tapi botolnya belum terlihat.
    public bool Locked
    {
        get => locked;
        set { locked = value; Refresh(); }
    }

    private void Awake() => button = GetComponent<Button>();
    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    // MARK: TAP: unit pertama, langsung masuk saat jari menyentuh tombol
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        Pour();                                // 1 unit (tap)
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
        Pour();                                                    // +1 unit (hold)
        holdTimer += holdInterval;                                 // mulai hitung lagi
    }

    // MARK: SELESAI: jari diangkat, hold berhenti
    public void OnPointerUp(PointerEventData eventData) => holding = false;

    private void Pour()
    {
        colorMixingController.AddPigment(Player, slot);
        Poured?.Invoke();
    }

    private void Refresh()
    {
        button.interactable = colorMixingController.IsSynced && !colorMixingController.IsReady(Player) && !locked;
        icon.color = button.interactable ? Color.white : disabledIconColor;
    }
}
