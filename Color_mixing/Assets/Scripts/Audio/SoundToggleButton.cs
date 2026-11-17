using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tombol nyala / mati untuk BGM atau SFX. Dipakai di menu pause dan boleh dipasang di tombol BGM / SFX
// yang sudah ada (mis. TopRightButtons): semuanya memakai pengaturan yang sama (SoundSettings).
[RequireComponent(typeof(Button))]
public class SoundToggleButton : MonoBehaviour
{
    public enum Kind { Bgm, Sfx }

    [SerializeField] private Kind kind;
    [Tooltip("Opsional: objek yang tampil saat suara MATI (mis. ikon coret).")]
    [SerializeField] private GameObject offMarker;
    [Tooltip("Opsional: teks yang diisi 'BGM: On' / 'BGM: Off'.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Opsional: gambar tombol saat suara MATI. Saat nyala dipakai gambar awal tombol.")]
    [SerializeField] private Sprite offSprite;
    [Tooltip("Tanpa Off Marker, label, dan Off Sprite: gambar tombol diredupkan saat mati.")]
    [SerializeField] private bool dimWhenOff = true;

    private Graphic graphic;
    private Color graphicColor;
    private Image image;
    private Sprite onSprite;

    public bool IsOn => kind == Kind.Bgm ? SoundSettings.BgmOn : SoundSettings.SfxOn;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        if (graphic != null) graphicColor = graphic.color;
        image = graphic as Image;
        if (image != null) onSprite = image.sprite;
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        SoundSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => SoundSettings.Changed -= Refresh;

    // Dipakai saat tombol dibuat dari kode (field Inspector tidak tersedia).
    public void Configure(Kind newKind, TMP_Text newLabel)
    {
        kind = newKind;
        label = newLabel;
        Refresh();
    }

    public void Toggle()
    {
        if (kind == Kind.Bgm) SoundSettings.BgmOn = !SoundSettings.BgmOn;
        else SoundSettings.SfxOn = !SoundSettings.SfxOn;
    }

    private void Refresh()
    {
        bool on = IsOn;
        if (offMarker != null) offMarker.SetActive(!on);
        if (label != null) label.text = $"{(kind == Kind.Bgm ? "BGM" : "SFX")}: {(on ? "On" : "Off")}";

        if (image != null && offSprite != null) image.sprite = on ? onSprite : offSprite;

        if (dimWhenOff && offMarker == null && label == null && offSprite == null && graphic != null)
        {
            Color c = graphicColor;
            c.a = graphicColor.a * (on ? 1f : 0.45f);
            graphic.color = c;
        }
    }
}
