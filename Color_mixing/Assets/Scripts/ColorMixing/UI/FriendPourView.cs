using UnityEngine;
using UnityEngine.UI;

// Botol pemain lain: nyelonong sedikit dari atas layar saat teman menuang, lalu naik lagi kalau berhenti.
// Posisi awal di Inspector = posisi tersembunyi (di luar layar).
public class FriendPourView : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // cadangan nomor pemain saat tes tanpa jaringan
    // Botol ini milik teman yang mana: 1 = pemain setelah kita (kanan atas), 2 = pemain setelahnya (kiri atas).
    // Harus sama dengan arah blob di PaintBlobGraphic (friendPour / secondFriendPour).
    [SerializeField, Range(1, 2)] private int friend = 1;
    [SerializeField] private Image bottle;
    [SerializeField] private RectTransform dropSpawn;   // mulut botol
    [SerializeField] private PaintDrops paintDrops;
    [SerializeField] private float peekDistance = 250f; // seberapa jauh botol turun ke dalam layar
    [SerializeField] private float stayTime = 0.6f;     // tetap terlihat selama ini setelah tuangan terakhir
    [SerializeField] private float moveSpeed = 2500f;
    [SerializeField] private AudioClip pourClip;         // SFX saat teman menuang (AddColor)

    private RectTransform rect;
    private Vector2 hiddenPosition;
    private float stayLeft;
    private bool dropPending;
    private Color pendingColor;

    private void Awake()
    {
        rect = bottle.rectTransform;
        hiddenPosition = rect.anchoredPosition;
    }

    private void OnEnable() => colorMixingController.Poured += OnPoured;

    private void OnDisable()
    {
        colorMixingController.Poured -= OnPoured;
        rect.anchoredPosition = hiddenPosition;
        stayLeft = 0f;
        dropPending = false;
    }

    // Satu botol per teman. Tuangan sendiri sudah digambar tangan, tuangan teman lain ditangani botol satunya.
    private void OnPoured(int pigment, int pourer)
    {
        int owner = (LocalPlayer.Index(player) + friend) % PigmentAssignment.PlayerCount;
        if (pourer < 0 || pourer / 2 != owner) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(pourClip);   // suara sendiri ada di PigmentButton

        Pigment p = colorMixingController.Palette.GetPigment(pigment);
        if (p.bottlePressed != null)
        {
            bottle.sprite = p.bottlePressed;
            bottle.SetNativeSize();
        }
        stayLeft = stayTime;
        dropPending = true;          // tetesan ditahan sampai botol selesai turun
        pendingColor = p.color;
    }

    private void Update()
    {
        stayLeft -= Time.deltaTime;
        Vector2 shown = hiddenPosition + Vector2.down * peekDistance;
        Vector2 goal = stayLeft > 0f ? shown : hiddenPosition;
        rect.anchoredPosition = Vector2.MoveTowards(rect.anchoredPosition, goal, moveSpeed * Time.deltaTime);

        // Botol sudah di posisi: baru keluarkan tetesan. Beberapa tuangan selama turun digabung jadi satu.
        if (dropPending && rect.anchoredPosition == shown)
        {
            dropPending = false;
            paintDrops.Drop(dropSpawn.position, pendingColor);
        }
    }
}
