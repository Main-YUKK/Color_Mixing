using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Background video untuk UI (Canvas Screen Space - Overlay).
// Video Player + Render Texture dibuat otomatis saat runtime, jadi cukup isi Clip di Inspector.
// Taruh objek ini sebagai anak dari Image background lama: gambar lama tetap terlihat
// sampai frame pertama video siap (tidak ada layar hitam), lalu tertutup video.
[RequireComponent(typeof(RectTransform))]
public class VideoBackground : MonoBehaviour
{
    [SerializeField] private VideoClip clip;
    [SerializeField] private bool loop = true;
    [Tooltip("Matikan suara video supaya tidak bentrok dengan musik lobby.")]
    [SerializeField] private bool mute = true;
    [Tooltip("Video selalu memenuhi layar; bagian yang lebih akan terpotong di pinggir, bukan ditarik/gepeng.")]
    [SerializeField] private bool fillScreen = true;

    private RawImage rawImage;
    private VideoPlayer player;
    private RenderTexture renderTexture;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage == null) rawImage = gameObject.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.enabled = false;   // tampil setelah frame pertama siap

        if (clip == null)
        {
            Debug.LogWarning("[VideoBackground] Clip belum diisi, background gambar lama tetap dipakai.", this);
            return;
        }

        int width = Mathf.Max(1, (int)clip.width);
        int height = Mathf.Max(1, (int)clip.height);

        renderTexture = new RenderTexture(width, height, 0) { name = $"{clip.name}_RT" };
        renderTexture.Create();
        rawImage.texture = renderTexture;

        if (fillScreen)
        {
            var fitter = GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)width / height;
        }

        player = GetComponent<VideoPlayer>();
        if (player == null) player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.isLooping = loop;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTexture;
        player.aspectRatio = VideoAspectRatio.Stretch;   // ukuran diatur AspectRatioFitter
        player.audioOutputMode = mute ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;

        player.sendFrameReadyEvents = true;
        player.frameReady += OnFirstFrameReady;
        player.prepareCompleted += OnPrepared;
        player.errorReceived += OnError;
        player.Prepare();
    }

    private void OnPrepared(VideoPlayer source) => source.Play();

    private void OnFirstFrameReady(VideoPlayer source, long frameIndex)
    {
        rawImage.enabled = true;
        // Event per frame cukup sekali; dimatikan supaya tidak membebani.
        source.frameReady -= OnFirstFrameReady;
        source.sendFrameReadyEvents = false;
    }

    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogError($"[VideoBackground] Gagal memutar video: {message}", this);
        rawImage.enabled = false;   // kembali ke gambar background lama
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.frameReady -= OnFirstFrameReady;
            player.prepareCompleted -= OnPrepared;
            player.errorReceived -= OnError;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
