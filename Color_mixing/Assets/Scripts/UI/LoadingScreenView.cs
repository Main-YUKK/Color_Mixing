using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Tampilan layar loading: video latar yang berulang + ikon yang berputar sendiri-sendiri.
// Pasang di akar prefab Resources/LoadingScreen (SceneLoader yang menampilkan / menyembunyikannya, dan mengurus
// teks "Loading...." berjalan). Semua isian di bawah boleh kosong; yang kosong dilewati.
//
// Waktu pakai unscaled, jadi tetap jalan walau Time.timeScale = 0.
public class LoadingScreenView : MonoBehaviour
{
    [Header("Video latar")]
    [SerializeField] private VideoClip clip;
    [Tooltip("RawImage layar penuh tempat video tampil.")]
    [SerializeField] private RawImage videoImage;
    [Tooltip("Lebar maksimum tekstur video (menghemat memori kalau videonya besar).")]
    [SerializeField] private int maxTextureWidth = 1280;

    [Header("Ikon berputar")]
    [SerializeField] private RectTransform[] spinners;
    [Tooltip("Derajat per detik. Tiap ikon mendapat kecepatan acak di rentang ini, arah acak (searah / berlawanan jarum jam).")]
    [SerializeField] private Vector2 speedRange = new Vector2(90f, 360f);
    [Tooltip("Kalau dicentang, kecepatan dan arah tiap ikon berganti perlahan secara acak selama loading.")]
    [SerializeField] private bool changeOverTime = true;
    [Tooltip("Selang acak (detik) antar pergantian kecepatan, kalau Change Over Time dicentang.")]
    [SerializeField] private Vector2 changeInterval = new Vector2(1.5f, 3.5f);
    [Tooltip("Seberapa cepat kecepatan mendekati targetnya (makin besar makin cepat berubah).")]
    [SerializeField] private float smoothing = 2f;

    private VideoPlayer player;
    private RenderTexture texture;
    private bool frameShown;

    private float[] speed;
    private float[] targetSpeed;
    private float[] nextChange;

    private void Awake()
    {
        SetUpVideo();
    }

    private void OnEnable()
    {
        RandomizeSpinners();

        if (player != null)
        {
            // Gambar baru tampil setelah frame pertama siap, supaya tidak ada kotak hitam.
            if (videoImage != null && !frameShown) videoImage.enabled = false;
            player.Play();
        }
    }

    private void OnDisable()
    {
        if (player != null) player.Pause();   // Pause (bukan Stop) supaya tetap siap untuk tampil berikutnya
    }

    private void OnDestroy()
    {
        if (player != null) player.frameReady -= OnFrameReady;
        if (texture != null)
        {
            if (videoImage != null) videoImage.texture = null;
            texture.Release();
            Destroy(texture);
        }
    }

    // ---------- Video ----------

    private void SetUpVideo()
    {
        if (clip == null || videoImage == null) return;

        int width = Mathf.Clamp((int)clip.width, 16, Mathf.Max(16, maxTextureWidth));
        int height = Mathf.Max(16, Mathf.RoundToInt(width * (float)clip.height / Mathf.Max(1, clip.width)));
        texture = new RenderTexture(width, height, 0) { name = "LoadingScreenVideo" };
        videoImage.texture = texture;

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.skipOnDrop = true;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.sendFrameReadyEvents = true;
        player.frameReady += OnFrameReady;
        player.Prepare();
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        if (frameShown) return;
        frameShown = true;
        player.sendFrameReadyEvents = false;
        player.frameReady -= OnFrameReady;
        if (videoImage != null) videoImage.enabled = true;
    }

    // ---------- Ikon berputar ----------

    private void RandomizeSpinners()
    {
        int n = spinners != null ? spinners.Length : 0;
        speed = new float[n];
        targetSpeed = new float[n];
        nextChange = new float[n];

        for (int i = 0; i < n; i++)
        {
            speed[i] = targetSpeed[i] = RandomSpeed();
            nextChange[i] = Time.unscaledTime + Random.Range(changeInterval.x, changeInterval.y);
        }
    }

    private float RandomSpeed()
    {
        float magnitude = Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    private void Update()
    {
        if (spinners == null) return;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < spinners.Length; i++)
        {
            if (spinners[i] == null) continue;

            if (changeOverTime && Time.unscaledTime >= nextChange[i])
            {
                targetSpeed[i] = RandomSpeed();
                nextChange[i] = Time.unscaledTime + Random.Range(changeInterval.x, changeInterval.y);
            }
            if (changeOverTime) speed[i] = Mathf.Lerp(speed[i], targetSpeed[i], 1f - Mathf.Exp(-smoothing * dt));

            spinners[i].Rotate(0f, 0f, speed[i] * dt);
        }
    }
}
