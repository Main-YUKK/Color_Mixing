using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Counts a percentage up from 0 with some juice:
///   1. count  - number rises (fast first, slows near the end) and the text grows as the number gets higher
///   2. lift   - tiny pop up when the final number is reached
///   3. slam   - squashes down below normal size, like a stamp hitting paper
///   4. settle - springs back to normal size
///
/// Put it on the TMP text and call Play(value). Animates in Update, so it waits
/// if the object is inactive when Play is called and starts once it becomes visible.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class PercentCountUp : MonoBehaviour
{
    [Header("Count")]
    [Tooltip("Seconds to count from 0 to the value.")]
    [SerializeField] private float countDuration = 1.1f;
    [Tooltip("Extra scale at 100%. The text grows in proportion to the number while counting.")]
    [SerializeField] private float growAt100 = 0.6f;

    [Header("Stamp")]
    [Tooltip("Extra pop on top of the grown size right before the slam.")]
    [SerializeField] private float liftScale = 1.12f;
    [SerializeField] private float liftDuration = 0.08f;
    [Tooltip("How small it gets when it hits (1 = normal size).")]
    [SerializeField] private float slamScale = 0.82f;
    [SerializeField] private float slamDuration = 0.07f;
    [SerializeField] private float settleDuration = 0.25f;

    [Tooltip("Fired the moment the stamp hits: hook a sound or screen shake here.")]
    public UnityEvent Stamped;

    private enum Phase { Idle, Count, Lift, Slam, Settle }

    private TMP_Text _text;
    private Vector3 _baseScale = Vector3.one;
    private Phase _phase = Phase.Idle;
    private float _t;
    private float _target;
    private float _grown;    // scale reached at the end of counting
    private int _shown = -1;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        // hidden mid-animation: snap to the final state so it never gets stuck big
        if (_phase != Phase.Idle) Finish();
    }

    /// <summary>Start counting from 0 to <paramref name="value"/> (0-100).</summary>
    public void Play(float value)
    {
        if (_text == null) Awake();
        _target = Mathf.Clamp(value, 0f, 100f);
        _phase = Phase.Count;
        _t = 0f;
        _shown = -1;
        SetNumber(0);
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        if (_phase == Phase.Idle) return;
        _t += Time.deltaTime;

        switch (_phase)
        {
            case Phase.Count:
            {
                float p = countDuration > 0f ? Mathf.Clamp01(_t / countDuration) : 1f;
                float value = _target * EaseOutCubic(p);
                SetNumber(Mathf.RoundToInt(value));
                SetScale(1f + growAt100 * value / 100f);
                if (p >= 1f)
                {
                    _grown = 1f + growAt100 * _target / 100f;
                    Next(Phase.Lift);
                }
                break;
            }
            case Phase.Lift:
            {
                float p = Progress(liftDuration);
                SetScale(Mathf.Lerp(_grown, _grown * liftScale, EaseOutCubic(p)));
                if (p >= 1f) Next(Phase.Slam);
                break;
            }
            case Phase.Slam:
            {
                float p = Progress(slamDuration);
                SetScale(Mathf.Lerp(_grown * liftScale, slamScale, p * p));   // accelerate into the hit
                if (p >= 1f)
                {
                    Stamped?.Invoke();
                    Next(Phase.Settle);
                }
                break;
            }
            case Phase.Settle:
            {
                float p = Progress(settleDuration);
                SetScale(Mathf.LerpUnclamped(slamScale, 1f, EaseOutBack(p)));
                if (p >= 1f) Finish();
                break;
            }
        }
    }

    private void Finish()
    {
        _phase = Phase.Idle;
        SetNumber(Mathf.RoundToInt(_target));
        transform.localScale = _baseScale;
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        _t = 0f;
    }

    private float Progress(float duration) => duration > 0f ? Mathf.Clamp01(_t / duration) : 1f;

    private void SetScale(float s) => transform.localScale = _baseScale * s;

    private void SetNumber(int n)
    {
        if (n == _shown) return;   // only touch the text when the number changes
        _shown = n;
        _text.text = n + "%";
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
