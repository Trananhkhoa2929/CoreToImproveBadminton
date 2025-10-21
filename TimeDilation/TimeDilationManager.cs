using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Centralized time-dilation controller with safe blending and restoration.
/// - Supports one active window at a time and returns a token (windowId).
/// - You can cancel by token to ensure you only stop the window you started.
/// - Fires events so UI can react (e.g., show SlowMo indicator).
/// </summary>
[DefaultExecutionOrder(-100)]
public class TimeDilationManager : MonoBehaviour
{
    public static TimeDilationManager Instance { get; private set; }

    [Header("Global Toggle")]
    [Tooltip("If false, all incoming slow-motion requests will be ignored.")]
    public bool enableSlowMotionGlobally = true;

    [Header("FixedDeltaTime Handling")]
    [Tooltip("Scale Time.fixedDeltaTime proportionally to timeScale while in slow-motion.")]
    public bool scaleFixedDeltaTime = true;

    [Tooltip("Base fixed delta to scale from. If <= 0, will capture from Time.fixedDeltaTime at Awake.")]
    [Min(0f)]
    public float baseFixedDeltaTime = 0f;

    [Header("Debug")]
    public bool logWindows = true;

    // Events for UI
    public event Action<bool, float> OnWindowActiveChanged; // (active, targetTimescale)
    public event Action<float> OnTimeScaleApplied;          // (current Time.timeScale)

    private Coroutine activeRoutine;
    private float preWindowTimeScale = 1f;
    private float preWindowFixedDelta = 0.02f;
    private bool inWindow = false;

    private int nextWindowId = 1;
    private int activeWindowId = 0; // 0 = none

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TimeDilationManager] Duplicate instance found. Destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (baseFixedDeltaTime <= 0f)
        {
            baseFixedDeltaTime = Time.fixedDeltaTime; // capture engine default at startup
        }
        preWindowFixedDelta = baseFixedDeltaTime;
        preWindowTimeScale = Time.timeScale;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Safety: restore if the object is destroyed during a window
        if (inWindow)
        {
            RestoreTimeScale(preWindowTimeScale, preWindowFixedDelta);
            inWindow = false;
            activeWindowId = 0;
            OnWindowActiveChanged?.Invoke(false, 1f);
        }
    }

    /// <summary>
    /// Begin a slow-motion window with the given profile.
    /// Returns a positive token (windowId) if started, or 0 if ignored.
    /// </summary>
    public int BeginWindow(TimeDilationSettings profile, float? overrideHoldSeconds = null, string reason = "Window")
    {
        if (profile == null)
        {
            if (logWindows) Debug.LogWarning("[TimeDilationManager] BeginWindow called with null profile. Ignored.");
            return 0;
        }
        if (!enableSlowMotionGlobally || !profile.enabledProfile)
        {
            if (logWindows) Debug.Log($"[TimeDilationManager] Slow-motion disabled or profile disabled. Ignored ({reason}).");
            return 0;
        }

        // Stop any existing window and start anew
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        int windowId = nextWindowId++;
        activeRoutine = StartCoroutine(WindowRoutine(windowId, profile, overrideHoldSeconds, reason));
        return windowId;
    }

    /// <summary>
    /// Cancel the current slow-motion window (any source).
    /// </summary>
    public void CancelActiveWindow()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (inWindow)
        {
            if (logWindows) Debug.Log("[TimeDilationManager] CancelActiveWindow: restoring timescale.");
            RestoreTimeScale(preWindowTimeScale, preWindowFixedDelta);
            inWindow = false;
            OnWindowActiveChanged?.Invoke(false, 1f);
        }
        activeWindowId = 0;
    }

    /// <summary>
    /// Cancel the window only if the token matches the active window.
    /// </summary>
    public void CancelWindow(int windowId)
    {
        if (windowId <= 0) return;
        if (!inWindow || activeWindowId != windowId) return;
        if (logWindows) Debug.Log($"[TimeDilationManager] CancelWindow({windowId})");
        CancelActiveWindow();
    }

    private IEnumerator WindowRoutine(int windowId, TimeDilationSettings profile, float? overrideHoldSeconds, string reason)
    {
        inWindow = true;
        activeWindowId = windowId;

        // Snapshot pre-window scales to fully restore later
        preWindowTimeScale = Time.timeScale;
        preWindowFixedDelta = Time.fixedDeltaTime;

        float target = Mathf.Clamp(profile.targetTimeScale, 0.02f, 1f);
        float blendIn = Mathf.Max(0f, profile.blendInSeconds);
        float hold = Mathf.Max(0f, overrideHoldSeconds.HasValue ? overrideHoldSeconds.Value : profile.holdSeconds);
        float blendOut = Mathf.Max(0f, profile.blendOutSeconds);

        if (logWindows)
        {
            Debug.Log($"[TimeDilationManager] Begin slow-motion '{reason}' (id={windowId}) => target={target}, in={blendIn}s, hold={hold}s, out={blendOut}s");
        }

        OnWindowActiveChanged?.Invoke(true, target);

        // Blend in
        if (blendIn > 0f)
        {
            float start = Time.timeScale;
            float t = 0f;
            while (t < blendIn && activeWindowId == windowId)
            {
                t += Time.unscaledDeltaTime; // real-time blending
                float a = Mathf.Clamp01(t / blendIn);
                float s = Mathf.Lerp(start, target, a);
                ApplyTimeScale(s);
                yield return null;
            }
        }
        if (activeWindowId != windowId) yield break; // canceled during blend

        // Snap to ensure exact target
        ApplyTimeScale(target);

        // Hold
        float timer = 0f;
        while (timer < hold && activeWindowId == windowId)
        {
            timer += Time.unscaledDeltaTime; // real-time
            yield return null;
        }
        if (activeWindowId != windowId) yield break; // canceled during hold

        // Blend out
        if (blendOut > 0f)
        {
            float start = Time.timeScale;
            float t = 0f;
            while (t < blendOut && activeWindowId == windowId)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / blendOut);
                float s = Mathf.Lerp(start, preWindowTimeScale, a);
                ApplyTimeScale(s);
                yield return null;
            }
        }
        if (activeWindowId != windowId) yield break; // canceled during out

        // Restore and finish
        RestoreTimeScale(preWindowTimeScale, preWindowFixedDelta);
        inWindow = false;
        activeWindowId = 0;
        OnWindowActiveChanged?.Invoke(false, 1f);

        if (logWindows)
        {
            Debug.Log($"[TimeDilationManager] Slow-motion '{reason}' (id={windowId}) ended. Restored Time.timeScale={Time.timeScale}.");
        }
        activeRoutine = null;
    }

    private void ApplyTimeScale(float timeScale)
    {
        Time.timeScale = timeScale;
        if (scaleFixedDeltaTime)
        {
            Time.fixedDeltaTime = baseFixedDeltaTime * timeScale;
        }
        OnTimeScaleApplied?.Invoke(timeScale);
    }

    private void RestoreTimeScale(float timeScale, float fixedDelta)
    {
        Time.timeScale = timeScale;
        if (scaleFixedDeltaTime)
        {
            Time.fixedDeltaTime = fixedDelta > 0f ? fixedDelta : baseFixedDeltaTime;
        }
        OnTimeScaleApplied?.Invoke(Time.timeScale);
    }

    // Expose state
    public bool IsWindowActive => inWindow;
    public int ActiveWindowId => activeWindowId;
}