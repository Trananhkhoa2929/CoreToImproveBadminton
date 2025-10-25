using System.Collections.Generic;
using UnityEngine;

// Phase 3 update:
// - Ưu tiên lấy PreparedShot từ IShotPlanProvider (ví dụ RacketShotCoordinator) nếu có (consume-once).
// - Nếu không có plan, giữ nguyên logic COR/baseExitSpeed như cũ (fallback an toàn).
//
// Quy ước trục:
// - Hướng bắn = shuttleSpawn.forward (Z).
// - "Up" cho LookRotation = shuttleSpawn.right (X).

[RequireComponent(typeof(Collider))]
public class RacketHitZone : MonoBehaviour
{
    [Header("Filter")]
    public string shuttlecockTag = "Shuttlecock";

    [Header("References")]
    public Transform shuttleSpawn;
    public GameObject shuttlecockPrefab;

    [Header("Return Ball Settings")]
    public bool respawnNewShuttle = false;
    public float spawnOffset = 0.02f;
    public bool teleportIncomingToSpawn = true;

    [Header("Exit Speed Model (fallback)")]
    public bool useIncomingSpeed = true;
    [Range(0.0f, 1.2f)]
    public float cor = 0.9f;
    public float baseExitSpeed = 20f;
    public AnimationCurve exitSpeedByIncoming;

    [Header("Cleanup")]
    public float rehitCooldown = 0.12f;
    public bool zeroAngularOnExit = true;

    [Header("Phase 3 – External Shot Plan (optional)")]
    [Tooltip("If true, this zone will try to consume a PreparedShot from the provider on contact.")]
    public bool preferShotPlanIfAvailable = true;

    [Tooltip("Object implementing IShotPlanProvider (e.g., RacketShotCoordinator on your Racket root).")]
    public MonoBehaviour shotPlanProvider;

    [Header("Debug")]
    public bool logOnHit = true;

    private Collider zone;
    private readonly Dictionary<int, float> lastHitTime = new Dictionary<int, float>();

    void Reset()
    {
        zone = GetComponent<Collider>();
        if (zone != null) zone.isTrigger = true;
    }

    void Awake()
    {
        zone = GetComponent<Collider>();
        if (zone == null)
        {
            Debug.LogError("[RacketHitZone] Missing Collider.");
            enabled = false;
            return;
        }
        if (!zone.isTrigger)
        {
            Debug.LogWarning("[RacketHitZone] Collider is not trigger. Consider enabling IsTrigger.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryHandle(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryHandle(collision.collider);
    }

    void TryHandle(Collider other)
    {
        if (!other || !other.gameObject.activeInHierarchy) return;
        if (!string.IsNullOrEmpty(shuttlecockTag) && !other.CompareTag(shuttlecockTag)) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        // Cooldown
        int id = rb.GetInstanceID();
        float now = Time.time;
        if (lastHitTime.TryGetValue(id, out float last) && now - last < rehitCooldown) return;
        lastHitTime[id] = now;

        // Basis axes
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Vector3 dir = basis.forward.normalized;
        Vector3 up = basis.right.normalized;

        // Phase 3: try external plan first
        bool appliedPlan = false;
        if (preferShotPlanIfAvailable && shotPlanProvider != null)
        {
            if (shotPlanProvider is IShotPlanProvider provider && provider.HasPlan)
            {
                if (provider.TryConsume(out PreparedShot plan))
                {
                    ApplyVelocity(rb, other.transform, plan.launchVelocity, plan.upVector.sqrMagnitude > 1e-8f ? plan.upVector : up);
                    appliedPlan = true;

                    if (logOnHit)
                    {
                        Debug.Log($"[RacketHitZone] Applied PreparedShot. speed={plan.launchVelocity.magnitude:0.00}");
                        Debug.DrawLine(rb.position, rb.position + plan.launchVelocity.normalized * 0.5f, Color.yellow, 1f);
                    }
                }
            }
        }

        if (appliedPlan) return;

        // Fallback legacy model
        float inSpeed = rb.linearVelocity.magnitude;

        float outSpeed = baseExitSpeed;
        if (exitSpeedByIncoming != null && exitSpeedByIncoming.keys != null && exitSpeedByIncoming.length > 0)
        {
            outSpeed = Mathf.Max(exitSpeedByIncoming.Evaluate(inSpeed), baseExitSpeed);
        }
        else if (useIncomingSpeed)
        {
            outSpeed = Mathf.Max(inSpeed * Mathf.Clamp01(cor), baseExitSpeed);
        }

        Vector3 vOut = dir * outSpeed;
        ApplyVelocity(rb, other.transform, vOut, up);

        if (logOnHit)
        {
            Debug.Log($"[RacketHitZone] Returned shuttle (fallback). inSpeed={inSpeed:F2} -> outSpeed={outSpeed:F2}, dir={dir}");
            Debug.DrawLine(rb.position, rb.position + dir * 0.5f, Color.green, 1f);
        }
    }

    private void ApplyVelocity(Rigidbody rb, Transform shuttleTf, Vector3 v, Vector3 upHint)
    {
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Vector3 dir = v.sqrMagnitude > 1e-8f ? v.normalized : basis.forward;

        if (teleportIncomingToSpawn)
        {
            Vector3 origin = basis.position + dir * spawnOffset;
            rb.position = origin;
        }

        rb.linearVelocity = v;
        if (zeroAngularOnExit) rb.angularVelocity = Vector3.zero;

        shuttleTf.rotation = Quaternion.LookRotation(dir, upHint);
    }

    void OnDrawGizmosSelected()
    {
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(basis.position, 0.008f);
        Gizmos.DrawLine(basis.position, basis.position + basis.forward * 0.3f);
    }
}