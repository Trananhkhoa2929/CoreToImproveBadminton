using UnityEngine;

/// <summary>
/// Bridges Phase 2 targeting and Phase 3 power to produce a one-shot PreparedShot.
/// - Reads "perfect solution" (direction+speed) from TargetingController if present.
/// - Computes sweet spot (perfectSpeed / maxLaunchSpeed).
/// - On power release, builds PreparedShot with manual offsets (small yaw/pitch) and stores for HitZone to consume.
/// Attach to the Racket root. Assign references in Inspector.
/// </summary>
[DefaultExecutionOrder(-5)]
public class RacketShotCoordinator : MonoBehaviour, IShotPlanProvider
{
    [Header("References")]
    public Transform shuttleSpawn;              // basis for up/right reference
    public PowerController power;               // Phase 3 input
    public MonoBehaviour targetingController;   // optional: a TargetingController; keep as MonoBehaviour to avoid hard dep

    [Header("Planner Settings")]
    public float maxLaunchSpeed = 35f;          // cap speed for mapping power01 -> speed
    public float sweetSpotWidth01 = 0.06f;      // visual sweet band width

    [Header("Manual Override (small angles)")]
    public KeyCode overrideKey = KeyCode.LeftShift;
    public float yawSensitivityDeg = 0.6f;      // deg per mouse unit
    public float pitchSensitivityDeg = 0.6f;
    public float maxYawDeg = 8f;
    public float maxPitchDeg = 8f;

    [Header("Debug")]
    public bool logPlan = true;

    // Runtime
    private PreparedShot? armedShot;            // prepared & waiting for HitZone
    private float currentYaw, currentPitch;     // small manual offsets
    private float perfectSpeedCache;            // for UI sweet spot
    private Vector3 baseDirectionCache = Vector3.forward;

    // Public interface for UI
    public bool HasPlan => armedShot.HasValue;
    public bool TryConsume(out PreparedShot plan)
    {
        if (armedShot.HasValue)
        {
            plan = armedShot.Value;
            armedShot = null;
            return true;
        }
        plan = default;
        return false;
    }

    public float GetSweetSpot01() => Mathf.Clamp01(maxLaunchSpeed > 1e-3f ? (perfectSpeedCache / maxLaunchSpeed) : 0f);
    public float GetSweetWidth01() => sweetSpotWidth01;

    void Awake()
    {
        if (power != null)
        {
            power.onReleased.AddListener(OnPowerReleased);
        }
    }

    void OnDestroy()
    {
        if (power != null)
        {
            power.onReleased.RemoveListener(OnPowerReleased);
        }
    }

    void Update()
    {
        // 1) Read "perfect" direction+speed from targeting if available (Phase 2).
        //    We avoid a hard type dependency: expect a component with fields/properties:
        //    - bool HasLockedTarget
        //    - ShotValidator.Result CurrentPlan (with chosen.launchVelocity)
        //    - Vector3 Origin (spawn pos)
        Vector3 perfectV = Vector3.zero;

        if (targetingController != null)
        {
            var t = targetingController;
            var tType = t.GetType();

            var hasLockedProp = tType.GetProperty("HasLockedTarget");
            var planProp = tType.GetProperty("CurrentPlan");

            if (planProp != null)
            {
                var plan = planProp.GetValue(t, null);
                if (plan != null)
                {
                    // plan.chosen.launchVelocity
                    var chosen = plan.GetType().GetField("chosen");
                    if (chosen != null)
                    {
                        var chosenVal = chosen.GetValue(plan);
                        var lvField = chosenVal.GetType().GetField("launchVelocity");
                        if (lvField != null)
                        {
                            perfectV = (Vector3)lvField.GetValue(chosenVal);
                        }
                    }
                }
            }
        }

        // Fallback: if no targeting/plan, use spawn.forward as direction and set a mid perfect speed
        if (perfectV.sqrMagnitude < 1e-6f)
        {
            Vector3 dir = (shuttleSpawn != null ? shuttleSpawn.forward : transform.forward);
            perfectV = dir.normalized * (0.6f * maxLaunchSpeed);
        }

        perfectSpeedCache = perfectV.magnitude;
        baseDirectionCache = perfectV.sqrMagnitude > 1e-6f ? perfectV.normalized : Vector3.forward;

        // 2) Manual override (small angles) while holding overrideKey (does not arm a shot by itself).
        if (Input.GetKey(overrideKey))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            currentYaw = Mathf.Clamp(currentYaw + dx * yawSensitivityDeg, -maxYawDeg, maxYawDeg);
            currentPitch = Mathf.Clamp(currentPitch - dy * pitchSensitivityDeg, -maxPitchDeg, maxPitchDeg);
        }
        else
        {
            // Optional: slowly relax to 0 when not overriding
            currentYaw = Mathf.MoveTowards(currentYaw, 0f, 30f * Time.deltaTime);
            currentPitch = Mathf.MoveTowards(currentPitch, 0f, 30f * Time.deltaTime);
        }
    }

    void OnPowerReleased(float power01)
    {
        // Build final shot with current small offsets
        Vector3 upBasis = (shuttleSpawn != null ? shuttleSpawn.right : transform.right); // your convention
        var shot = ShotPlanner.BuildPreparedShot(
            origin: (shuttleSpawn != null ? shuttleSpawn.position : transform.position),
            baseDirection: baseDirectionCache,
            power01: power01,
            maxLaunchSpeed: maxLaunchSpeed,
            yawOffsetDeg: currentYaw,
            pitchOffsetDeg: currentPitch,
            upBasis: upBasis
        );

        armedShot = shot;

        if (logPlan)
        {
            Debug.Log($"[ShotCoordinator] Armed shot: speed={shot.launchVelocity.magnitude:0.00}, " +
                      $"dir={shot.launchVelocity.normalized}, yaw={currentYaw:0.0}°, pitch={currentPitch:0.0}°, power={power01:0.000}");
        }
    }
}