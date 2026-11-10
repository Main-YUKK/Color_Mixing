using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Satu sisi tangan: tangan + botol + tombol pigmen (prefab PigmentSlot; sisi kiri = Scale X -1 di root).
// Tahan tombol = pose tuang + tetesan cat.
// Rotasi pigmen = tangan membuka, botol & tombol jatuh keluar layar, ganti warna, naik lagi, tangan memegang lagi.
public class PigmentSlotView : MonoBehaviour
{
    // MARK: - Inspector
    [SerializeField] private PigmentButton button;        // sumber controller, player, slot
    [SerializeField] private Image hand;
    [SerializeField] private Image bottle;
    [SerializeField] private RectTransform dropSpawn;     // mulut botol (anak PigmentBottle, ikut bergerak)
    [SerializeField] private PaintDrops paintDrops;
    [SerializeField] private Sprite handDefault;
    [SerializeField] private Sprite handPressed;
    [SerializeField] private Sprite handOpen;             // HandColorChanged
    [SerializeField] private float fallDistance = 1000f;  // jarak jatuh botol & tombol (harus sampai keluar layar)
    [SerializeField] private float openPause = 0.15f;     // tangan membuka dulu sebelum botol jatuh
    [SerializeField] private float fallTime = 0.35f;
    [SerializeField] private float fallButtonLag = 0.1f;  // tombol mulai jatuh sekian detik setelah botol
    [SerializeField] private float bottomPause = 0.2f;    // diam di bawah layar sebelum warna baru naik
    [SerializeField] private float riseTime = 0.35f;
    [SerializeField] private float riseButtonLag = 0f;    // tombol mulai naik sekian detik setelah botol (negatif = tombol duluan)

    // MARK: - State
    private ColorMixingController Controller => button.Controller;
    private RectTransform bottleRect;
    private RectTransform buttonRect;
    private Vector2 bottleHome;
    private Vector2 buttonHome;
    private int shownPigment = -1;   // pigmen yang sedang terlihat (bisa beda dari state selama animasi rotasi)
    private int shownRotation;
    private bool initialized;
    private bool rotating;
    private bool pressedPose;

    // MARK: - Unity Lifecycle
    private void Awake()
    {
        bottleRect = bottle.rectTransform;
        buttonRect = (RectTransform)button.transform;
        bottleHome = bottleRect.anchoredPosition;
        buttonHome = buttonRect.anchoredPosition;
    }

    private void OnEnable()
    {
        Controller.StateChanged += Refresh;
        button.Poured += Drip;
    }

    private void OnDisable()
    {
        Controller.StateChanged -= Refresh;
        button.Poured -= Drip;
        if (!rotating) return;
        StopAllCoroutines();   // panel disembunyikan di tengah animasi: kembalikan ke posisi normal
        FinishRotation();
    }

    private void Start() => Refresh();

    private void Update()
    {
        bool pressed = button.IsHeld && !rotating;
        if (pressed != pressedPose) SetPose(pressed);
    }

    // MARK: - Refresh
    private void Refresh()
    {
        if (!Controller.IsSynced) return;   // client sebelum foto pertama: pembagian pigmen belum asli

        // RotationCount naik = rotasi. StateChanged datang sebelum Rotated, jadi warna jangan langsung diganti.
        int rotation = Controller.RotationCount;
        bool rotated = initialized && rotation != shownRotation;
        shownRotation = rotation;
        initialized = true;

        if (rotated && !rotating) StartCoroutine(RotateRoutine());
        else if (!rotating) Show(Controller.GetPigment(button.Player, button.Slot));
    }

    private void Show(int pigment)
    {
        if (pigment == shownPigment || pigment < 0) return;
        shownPigment = pigment;
        Pigment p = Controller.Palette.GetPigment(pigment);
        SetSprite(bottle, pressedPose ? p.bottlePressed : p.bottle);
        button.Icon.sprite = p.icon;
    }

    // MARK: - Tuang
    private void SetPose(bool pressed)
    {
        pressedPose = pressed;
        SetSprite(hand, pressed ? handPressed : handDefault);
        if (shownPigment < 0) return;
        Pigment p = Controller.Palette.GetPigment(shownPigment);
        SetSprite(bottle, pressed ? p.bottlePressed : p.bottle);
    }

    private void Drip()
    {
        if (shownPigment < 0) return;
        paintDrops.Drop(dropSpawn.position, Controller.Palette.GetPigment(shownPigment).color);
    }

    // MARK: - Rotasi
    private IEnumerator RotateRoutine()
    {
        rotating = true;
        button.Locked = true;
        SetPose(false);
        SetSprite(hand, handOpen);

        yield return new WaitForSeconds(openPause);
        yield return Slide(0f, -fallDistance, fallTime, fallButtonLag, easeIn: true);

        Show(Controller.GetPigment(button.Player, button.Slot));   // ambil yang terbaru, bukan yang saat rotasi mulai
        yield return new WaitForSeconds(bottomPause);

        yield return Slide(-fallDistance, 0f, riseTime, riseButtonLag, easeIn: false);
        FinishRotation();
    }

    private void FinishRotation()
    {
        Offset(0f);
        rotating = false;
        SetPose(false);
        Show(Controller.GetPigment(button.Player, button.Slot));
        button.Locked = false;
    }

    // Botol dan tombol bergerak dengan durasi sama, tombol tertinggal buttonLag detik (negatif = tombol duluan).
    private IEnumerator Slide(float from, float to, float duration, float buttonLag, bool easeIn)
    {
        float bottleStart = Mathf.Max(0f, -buttonLag);
        float buttonStart = Mathf.Max(0f, buttonLag);
        float total = duration + Mathf.Abs(buttonLag);
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            Offset(Ease(from, to, (t - bottleStart) / duration, easeIn), Ease(from, to, (t - buttonStart) / duration, easeIn));
            yield return null;
        }
        Offset(to);
    }

    private static float Ease(float from, float to, float k, bool easeIn)
    {
        k = Mathf.Clamp01(k);
        k = easeIn ? k * k : 1f - (1f - k) * (1f - k);   // jatuh makin cepat, naik makin pelan
        return Mathf.Lerp(from, to, k);
    }

    // Tangan tetap di tempat; botol dan tombol digeser.
    private void Offset(float y) => Offset(y, y);

    private void Offset(float bottleY, float buttonY)
    {
        bottleRect.anchoredPosition = bottleHome + Vector2.up * bottleY;
        buttonRect.anchoredPosition = buttonHome + Vector2.up * buttonY;
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (sprite == null) return;
        image.sprite = sprite;
        image.SetNativeSize();   // sprite pressed / open ukurannya beda
    }
}
