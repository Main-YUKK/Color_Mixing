using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Satu sisi tangan: tangan + botol + tombol pigmen (prefab PigmentSlot; sisi kiri = Scale X -1 di root).
// Tahan tombol = pose tuang + tetesan cat.
// Rotasi pigmen = tangan membuka, botol lalu tombol jatuh keluar layar, ganti warna, naik lagi, tangan memegang lagi.
// Mix = tangan & botol lalu tombol turun keluar layar (penanda tidak bisa menuang). Cancel Mix = naik lagi.
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
    [SerializeField] private float fallDistance = 1000f;  // jarak turun (tangan, botol, tombol harus sampai keluar layar)
    [SerializeField] private float openPause = 0.15f;     // rotasi: tangan membuka dulu sebelum botol jatuh
    [SerializeField] private float fallTime = 0.35f;
    [SerializeField] private float fallButtonLag = 0.1f;  // tombol mulai turun sekian detik setelah botol
    [SerializeField] private float bottomPause = 0.2f;    // rotasi: diam di bawah layar sebelum warna baru naik
    [SerializeField] private float riseTime = 0.35f;
    [SerializeField] private float riseButtonLag = 0f;    // tombol mulai naik sekian detik setelah botol (negatif = tombol duluan)

    // MARK: - State
    private ColorMixingController Controller => button.Controller;
    private RectTransform handRect;
    private RectTransform bottleRect;
    private RectTransform buttonRect;
    private Vector2 handHome;
    private Vector2 bottleHome;
    private Vector2 buttonHome;
    private float handY, bottleY, buttonY;   // geseran saat ini dari posisi awal (negatif = turun)
    private int shownPigment = -1;           // pigmen yang sedang terlihat (bisa beda dari state selama animasi)
    private int shownRotation;
    private bool initialized;
    private bool hidden;                     // pemain sudah Mix: semua di bawah layar
    private Coroutine routine;               // animasi yang sedang jalan (rotasi / turun / naik)
    private bool pressedPose;

    private bool Busy => routine != null;

    // MARK: - Unity Lifecycle
    private void Awake()
    {
        handRect = hand.rectTransform;
        bottleRect = bottle.rectTransform;
        buttonRect = (RectTransform)button.transform;
        handHome = handRect.anchoredPosition;
        bottleHome = bottleRect.anchoredPosition;
        buttonHome = buttonRect.anchoredPosition;
    }

    private void OnEnable()
    {
        Controller.StateChanged += Refresh;
        button.Poured += Drip;
        if (initialized) Refresh();   // kembali dari reveal (Redo): state bisa berubah saat panel tersembunyi
    }

    private void OnDisable()
    {
        Controller.StateChanged -= Refresh;
        button.Poured -= Drip;
        if (!Busy) return;
        StopCoroutine(routine);       // panel disembunyikan di tengah animasi: langsung ke posisi akhir
        routine = null;
        Snap();
    }

    private void Start()
    {
        Refresh();
        Snap();   // mulai di posisi akhir tanpa animasi (mis. masuk saat sudah Mix)
    }

    private void Update()
    {
        bool pressed = button.IsHeld && !Busy && !hidden;
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

        bool ready = Controller.IsReady(button.Player);
        if (!initialized)
        {
            initialized = true;
            hidden = ready;
            Show(CurrentPigment);
            return;
        }

        if (ready != hidden)
        {
            hidden = ready;
            Play(ready ? HideRoutine() : ReturnRoutine());
        }
        else if (rotated && !hidden && !Busy) Play(RotateRoutine());
        else if (!Busy) Show(CurrentPigment);   // termasuk saat tersembunyi: ganti warna di luar layar, tanpa animasi
    }

    private int CurrentPigment => Controller.GetPigment(button.Player, button.Slot);

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

    // MARK: - Animasi
    // Satu animasi pada satu waktu; yang baru memotong yang lama dan mulai dari posisi saat ini.
    private void Play(IEnumerator next)
    {
        if (Busy) StopCoroutine(routine);
        button.Locked = true;
        routine = StartCoroutine(Run(next));
    }

    private IEnumerator Run(IEnumerator body)
    {
        yield return body;
        routine = null;
        button.Locked = false;
    }

    private IEnumerator RotateRoutine()
    {
        SetPose(false);
        SetSprite(hand, handOpen);
        yield return new WaitForSeconds(openPause);
        yield return Move(0f, -fallDistance, -fallDistance, fallTime, fallButtonLag, easeIn: true);

        Show(CurrentPigment);   // ambil yang terbaru, bukan yang saat rotasi mulai
        yield return new WaitForSeconds(bottomPause);

        yield return Move(0f, 0f, 0f, riseTime, riseButtonLag, easeIn: false);
        SetSprite(hand, handDefault);
    }

    // Mix: tangan & botol turun, tombol menyusul.
    private IEnumerator HideRoutine()
    {
        SetPose(false);
        yield return Move(-fallDistance, -fallDistance, -fallDistance, fallTime, fallButtonLag, easeIn: true);
    }

    // Cancel Mix (atau Redo): naik lagi dengan warna terbaru.
    private IEnumerator ReturnRoutine()
    {
        SetPose(false);
        Show(CurrentPigment);
        yield return Move(0f, 0f, 0f, riseTime, riseButtonLag, easeIn: false);
    }

    // Tangan & botol bergerak bersama, tombol tertinggal buttonLag detik (negatif = tombol duluan). Durasi sama.
    private IEnumerator Move(float handTo, float bottleTo, float buttonTo, float duration, float buttonLag, bool easeIn)
    {
        float handFrom = handY, bottleFrom = bottleY, buttonFrom = buttonY;
        float pairStart = Mathf.Max(0f, -buttonLag);
        float buttonStart = Mathf.Max(0f, buttonLag);
        float total = duration + Mathf.Abs(buttonLag);

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float pair = Ease((t - pairStart) / duration, easeIn);
            float single = Ease((t - buttonStart) / duration, easeIn);
            SetOffsets(Mathf.Lerp(handFrom, handTo, pair), Mathf.Lerp(bottleFrom, bottleTo, pair), Mathf.Lerp(buttonFrom, buttonTo, single));
            yield return null;
        }
        SetOffsets(handTo, bottleTo, buttonTo);
    }

    private static float Ease(float k, bool easeIn)
    {
        k = Mathf.Clamp01(k);
        return easeIn ? k * k : 1f - (1f - k) * (1f - k);   // turun makin cepat, naik makin pelan
    }

    // Tanpa animasi: langsung ke posisi akhir sesuai status Mix.
    private void Snap()
    {
        float y = hidden ? -fallDistance : 0f;
        SetOffsets(y, y, y);
        SetPose(false);
        Show(CurrentPigment);
        button.Locked = false;
    }

    private void SetOffsets(float newHandY, float newBottleY, float newButtonY)
    {
        handY = newHandY;
        bottleY = newBottleY;
        buttonY = newButtonY;
        handRect.anchoredPosition = handHome + Vector2.up * handY;
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
