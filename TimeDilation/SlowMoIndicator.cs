using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual indicator for slow-motion windows:
/// - Fades a fullscreen overlay (Image/CanvasGroup) when slow-mo is active.
/// - Optionally modulates intensity by current Time.timeScale.
/// Setup:
/// - Create a Screen Space - Overlay Canvas, add a full-rect Image (black or color with alpha 0).
/// - Assign that Image to overlayImage (or a CanvasGroup to overlayGroup).
/// </summary>
public class SlowMoIndicator : MonoBehaviour
{
    [Header("References")]
    public Image overlayImage;         // Optional: color overlay (set alpha)
    public CanvasGroup overlayGroup;   // Optional: fade group (alpha)

    [Header("Visuals")]
    [Tooltip("Max alpha when slow-mo is fully active.")]
    [Range(0f, 1f)] public float maxAlpha = 0.25f;
    [Tooltip("Color for overlay when slow-mo is active (alpha will be controlled).")]
    public Color overlayColor = new Color(0f, 1f, 0.65f, 1f); // teal-ish

    [Tooltip("Seconds to fade in/out the overlay.")]
    [Min(0f)] public float fadeSeconds = 0.08f;

    [Header("Modulate by TimeScale")]
    [Tooltip("If true, alpha will scale with (1 - Time.timeScale).")]
    public bool modulateByTimeScale = true;

    private Coroutine fadeRoutine;
    private float targetAlpha = 0f;

    void OnEnable()
    {
        TryHook();
        // Initialize hidden
        ApplyAlphaImmediate(0f);
    }

    void OnDisable()
    {
        Unhook();
        ApplyAlphaImmediate(0f);
    }

    void TryHook()
    {
        if (TimeDilationManager.Instance != null)
        {
            TimeDilationManager.Instance.OnWindowActiveChanged += OnWindowActiveChanged;
            TimeDilationManager.Instance.OnTimeScaleApplied += OnTimeScaleApplied;
        }
        else
        {
            Debug.LogWarning("[SlowMoIndicator] No TimeDilationManager in scene. Indicator will not react.");
        }
    }

    void Unhook()
    {
        if (TimeDilationManager.Instance != null)
        {
            TimeDilationManager.Instance.OnWindowActiveChanged -= OnWindowActiveChanged;
            TimeDilationManager.Instance.OnTimeScaleApplied -= OnTimeScaleApplied;
        }
    }

    void OnWindowActiveChanged(bool active, float targetScale)
    {
        if (active)
        {
            // Start fade in toward maxAlpha (or modulated)
            float alpha = maxAlpha;
            if (modulateByTimeScale)
            {
                alpha = maxAlpha * Mathf.Clamp01(1f - targetScale);
            }
            SetTargetAlpha(alpha);
        }
        else
        {
            // Fade out
            SetTargetAlpha(0f);
        }
    }

    void OnTimeScaleApplied(float currentScale)
    {
        if (!modulateByTimeScale) return;
        float alpha = TimeDilationManager.Instance != null && TimeDilationManager.Instance.IsWindowActive
            ? maxAlpha * Mathf.Clamp01(1f - currentScale)
            : 0f;
        SetTargetAlpha(alpha);
    }

    void SetTargetAlpha(float a)
    {
        targetAlpha = Mathf.Clamp01(a);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(targetAlpha));
    }

    IEnumerator FadeTo(float a)
    {
        float start = GetCurrentAlpha();
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime; // fade with real-time
            float k = (fadeSeconds > 0f) ? Mathf.Clamp01(t / fadeSeconds) : 1f;
            float v = Mathf.Lerp(start, a, k);
            ApplyAlphaImmediate(v);
            yield return null;
        }
        ApplyAlphaImmediate(a);
        fadeRoutine = null;
    }

    float GetCurrentAlpha()
    {
        if (overlayGroup != null) return overlayGroup.alpha;
        if (overlayImage != null) return overlayImage.color.a;
        return 0f;
    }

    void ApplyAlphaImmediate(float a)
    {
        if (overlayGroup != null)
        {
            overlayGroup.alpha = a;
        }
        if (overlayImage != null)
        {
            var c = overlayColor;
            c.a = a;
            overlayImage.color = c;
        }
    }
}