using UnityEngine;

[CreateAssetMenu(fileName = "TimeDilationSettings", menuName = "SuperRealife/Time Dilation Settings", order = 10)]
public class TimeDilationSettings : ScriptableObject
{
    [Header("Enable")]
    [Tooltip("Master toggle for this time dilation profile.")]
    public bool enabledProfile = true;

    [Header("Profile")]
    [Tooltip("Target time scale during the slow-motion window (0.1–0.6 typical).")]
    [Range(0.02f, 1f)]
    public float targetTimeScale = 0.25f;

    [Tooltip("Seconds to blend from current timeScale to targetTimeScale.")]
    [Min(0f)]
    public float blendInSeconds = 0.08f;

    [Tooltip("Seconds to hold at targetTimeScale (real-time seconds). Used only if you don't cancel it earlier.")]
    [Min(0f)]
    public float holdSeconds = 0.35f;

    [Tooltip("Seconds to blend back to the pre-window timeScale.")]
    [Min(0f)]
    public float blendOutSeconds = 0.15f;
}