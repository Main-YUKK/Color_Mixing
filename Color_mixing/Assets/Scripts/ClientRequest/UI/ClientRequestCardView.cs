using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Kartu request klien.
// Dengan Typewriter nyala: teks muncul huruf demi huruf saat kartu tampil.
//   Tap pertama  -> teks langsung tampil semua + "tekan manapun untuk lanjut" muncul.
//   Tap berikut  -> ContinueRequested (ClientArrivalSequence pindah ke layar berikutnya).
// Kalau teks selesai sendiri, petunjuk lanjut juga muncul dan tap berikutnya langsung lanjut.
public class ClientRequestCardView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text descriptionText;   // teks deskripsi request
    [Tooltip("Aktifkan di scene yang dimuat setelah request dipilih (mis. ColorMixing): kartu langsung menampilkan GameSession.CurrentRequest.")]
    [SerializeField] private bool showCurrentOnStart;

    [Header("Typewriter + tap untuk lanjut")]
    [SerializeField] private bool typewriter;
    [Tooltip("Huruf per detik.")]
    [SerializeField] private float charsPerSecond = 40f;
    [Tooltip("Jeda tambahan (detik) setelah tanda baca . , ! ? supaya enak dibaca.")]
    [SerializeField] private float punctuationPause = 0.12f;
    [Tooltip("Petunjuk \"tekan manapun untuk lanjut\". Disembunyikan sampai teks tampil semua.")]
    [SerializeField] private GameObject continueHint;
    [Tooltip("Petunjuk berdenyut pelan supaya terlihat bisa ditekan.")]
    [SerializeField] private bool pulseHint = true;

    // Pemain tap setelah teks tampil semua: waktunya pindah layar.
    public event Action ContinueRequested;

    public bool UsesTapToContinue => typewriter;

    private int shown;          // huruf yang sudah tampil
    private int total;
    private float clock;        // waktu menuju huruf berikutnya
    private bool complete;
    private bool continued;
    private int enabledFrame;
    private Vector3 hintScale = Vector3.one;
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    private void Awake()
    {
        if (continueHint != null) hintScale = continueHint.transform.localScale;
    }

    private void Start()
    {
        if (showCurrentOnStart && GameSession.CurrentRequest != null) Show(GameSession.CurrentRequest);
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        Restart();   // kartu muncul: mulai mengetik dari huruf pertama
    }

    public void Show(ClientRequest request)
    {
        descriptionText.text = request.description;
        Restart();
    }

    private void Restart()
    {
        complete = false;
        continued = false;
        clock = 0f;
        shown = 0;
        if (!typewriter)
        {
            descriptionText.maxVisibleCharacters = int.MaxValue;
            if (continueHint != null) continueHint.SetActive(false);
            return;
        }
        descriptionText.ForceMeshUpdate();
        total = descriptionText.textInfo.characterCount;
        descriptionText.maxVisibleCharacters = 0;
        SetHint(false);
    }

    private void Update()
    {
        if (!typewriter || continued) return;
        if (Time.timeScale <= 0f) return;   // game dijeda: jangan mengetik / menerima tap

        if (!complete) Type(Time.deltaTime);
        else if (pulseHint && continueHint != null)
            continueHint.transform.localScale = hintScale * (1f + 0.05f * Mathf.Sin(Time.time * 4f));

        if (Time.frameCount == enabledFrame || !Tapped()) return;

        if (!complete) Complete();          // tap pertama: tampilkan semua
        else
        {
            continued = true;               // tap kedua: lanjut
            ContinueRequested?.Invoke();
        }
    }

    private void Type(float dt)
    {
        clock -= dt;
        while (clock <= 0f && shown < total)
        {
            shown++;
            clock += 1f / Mathf.Max(1f, charsPerSecond);
            char c = descriptionText.textInfo.characterInfo[shown - 1].character;
            if (c == '.' || c == ',' || c == '!' || c == '?') clock += punctuationPause;
        }
        descriptionText.maxVisibleCharacters = shown;
        if (shown >= total) Complete();
    }

    private void Complete()
    {
        complete = true;
        shown = total;
        descriptionText.maxVisibleCharacters = int.MaxValue;
        SetHint(true);
    }

    private void SetHint(bool on)
    {
        if (continueHint == null) return;
        continueHint.transform.localScale = hintScale;
        continueHint.SetActive(on);
    }

    // Tap / klik di mana saja, kecuali di atas tombol (mis. tombol pengaturan di HUD).
    private static bool Tapped()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return false;

        var es = EventSystem.current;
        if (es == null) return true;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pointer.position.ReadValue() }, hits);
        foreach (var hit in hits)
            if (hit.gameObject.GetComponentInParent<Selectable>() != null) return false;
        return true;
    }
}
