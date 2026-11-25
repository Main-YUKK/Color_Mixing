using UnityEngine;

// Getar HP saat timer klien hampir habis. Pasang di objek yang sama dengan ClientTimer
// (ClientTimer memasangnya sendiri kalau belum ada; tambahkan manual di prefab kalau mau mengubah nilainya).
// Handheld.Vibrate: Android + iPhone saja, satu getaran dengan kekuatan tetap.
// Di Editor, iPad, dan Simulator tidak melakukan apa-apa. Tidak perlu izin atau setelan tambahan.
[DisallowMultipleComponent]
public class TimerHaptics : MonoBehaviour
{
    [SerializeField] private ClientTimer timer;
    [SerializeField] private bool vibrate = true;
    [Tooltip("Berapa detik sebelum waktu habis HP mulai bergetar.")]
    [SerializeField, Min(0f)] private float secondsBeforeEnd = 5f;
    [Tooltip("Jeda antar getaran (detik) sampai waktu habis. 0 = getar sekali saja.")]
    [SerializeField, Min(0f)] private float interval = 1f;

    private bool started;       // getaran pertama sudah terjadi untuk timer yang sedang berjalan
    private float nextAt = -1f; // sisa waktu saat getaran berikutnya; < 0 = tidak ada lagi

    private void OnEnable()
    {
        if (timer == null) timer = GetComponent<ClientTimer>();
        if (timer != null) timer.Started += ResetState;
    }

    private void OnDisable()
    {
        if (timer != null) timer.Started -= ResetState;
    }

    private void ResetState()
    {
        started = false;
        nextAt = -1f;
    }

    private void Update()
    {
        if (!vibrate || timer == null || !timer.IsRunning || timer.InIntro) return;
        float left = timer.SecondsLeft;

        if (!started)
        {
            if (left > secondsBeforeEnd) return;
            started = true;
            Buzz();
            nextAt = interval > 0f ? left - interval : -1f;
        }
        else if (nextAt >= 0f && left <= nextAt)
        {
            Buzz();
            nextAt -= interval;
        }
    }

    private static void Buzz()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
