using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles tap-and-hold power accumulation for Phase 3.
/// - HoldKey (default Mouse1) to charge from 0..1.
/// - Optional ping-pong charge (loop) for timing-based play.
/// - Exposes events and current power for UI.
/// Fixed:
/// - Ping-pong now smooth (uses real deltaTime).
/// - Events fire correctly for UI show/hide.
/// </summary>
public class PowerController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode holdKey = KeyCode.Mouse1;

    [Header("Charge")]
    [Tooltip("Seconds to go from 0 to 1 (one way).")]
    public float secondsToFull = 0.8f;

    [Tooltip("Loop up/down while holding (ping-pong). If false, clamps at 1.0.")]
    public bool loopPingPong = true;

    [Header("Start Value")]
    [Tooltip("Start from previous value when you hold again (for rhythm games).")]
    public bool keepLastValue = false;

    [Header("Events")]
    public UnityEvent onChargeStart;        // fires when holdKey down
    public UnityEvent<float> onCharging;    // fires every frame while charging (current power 0..1)
    public UnityEvent<float> onReleased;    // fires when holdKey up (final power 0..1)

    [Header("Debug")]
    public bool logEvents = false;

    private bool isCharging = false;
    private float power01 = 0f;
    private int dir = 1; // 1 = increasing, -1 = decreasing

    public bool IsCharging => isCharging;
    public float CurrentPower01 => power01;

    void Update()
    {
        // START charge on key down
        if (Input.GetKeyDown(holdKey))
        {
            isCharging = true;
            if (!keepLastValue)
            {
                power01 = 0f;
                dir = 1;
            }
            onChargeStart?.Invoke();
            if (logEvents) Debug.Log("[Power] Start charging");
        }

        // HOLD charge: accumulate power
        if (isCharging && Input.GetKey(holdKey))
        {
            float speed = (secondsToFull > 1e-5f ? 1f / secondsToFull : 1000f);
            float delta = speed * Time.deltaTime * dir;
            power01 += delta;

            if (loopPingPong)
            {
                // Ping-pong: reverse direction when hitting bounds
                if (power01 >= 1f)
                {
                    power01 = 1f;
                    dir = -1;
                }
                else if (power01 <= 0f)
                {
                    power01 = 0f;
                    dir = 1;
                }
            }
            else
            {
                // Clamp at 1.0
                power01 = Mathf.Clamp01(power01);
            }

            onCharging?.Invoke(power01);
        }

        // RELEASE charge on key up
        if (isCharging && Input.GetKeyUp(holdKey))
        {
            isCharging = false;
            onReleased?.Invoke(power01);
            if (logEvents) Debug.Log($"[Power] Released at {power01:0.000}");
        }
    }
}