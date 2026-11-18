using UnityEngine;

// Ekspresi klien: netral saat datang, lalu senang (lolos) atau kesal (tidak lolos) sampai klien pergi.
// Pasang di prefab klien. Sprite netral diambil dari sprite badan prefab, jadi cukup isi Good dan Bad.
// Klien baru selalu dibuat dari prefab, jadi otomatis mulai netral lagi.
public class ClientExpression : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;   // kosong = SpriteRenderer di objek ini
    [SerializeField] private Sprite good;
    [SerializeField] private Sprite bad;

    private Sprite neutral;
    private RandomCharacter character;   // punya Body Variants = sprite (dan versi hadap kanan) diambil dari sana

    public Sprite Good => good;
    public Sprite Bad => bad;

    private void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        neutral = body.sprite;
        TryGetComponent(out character);
    }

    public void Show(bool success)
    {
        if (character != null && character.HasBodyVariants)
        {
            character.SetMood(success ? RandomCharacter.Mood.Good : RandomCharacter.Mood.Bad);
            return;
        }
        Sprite sprite = success ? good : bad;
        if (sprite != null) body.sprite = sprite;  
    }

    public void ShowNeutral()
    {
        if (character != null && character.HasBodyVariants) character.SetMood(RandomCharacter.Mood.Neutral);
        else body.sprite = neutral;
    }
}
