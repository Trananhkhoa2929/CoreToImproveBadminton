using System.Collections.Generic;
using UnityEngine;

// Gắn script này lên GameObject có Collider phủ mặt vợt của BẠN (khuyến nghị Is Trigger = true).
// Khi Shuttlecock (tag) đi vào trigger, script sẽ:
// - Tùy chọn kích hoạt Slow-Motion trong thời gian shuttle NẰM TRONG vùng hit (WhileInsideOnly).
// - Trả cầu theo logic hiện có (COR / baseExitSpeed / curve). An toàn, không phá vỡ hành vi cũ.
//
// Slow-Motion tích hợp (Phase 1+):
// - Nếu triggerSlowMoOnHit = true và có TimeDilationManager + profile, khi shuttle ENTER -> BeginWindow(...)
// - Nếu slowMoWhileInsideOnly = true: Khi shuttle EXIT -> CancelWindow(token) để thoát slow-mo ngay.
//   + Điều này đảm bảo slow-mo chỉ tồn tại trong vùng hit, đúng yêu cầu của bạn.
// - Nếu respawnNewShuttle hủy shuttle ngay trong vùng, chúng ta chủ động CancelWindow trước khi Destroy để không bị "kẹt slow-mo".
//
// Quy ước trục (giữ nguyên theo repo):
// - Hướng bắn = shuttleSpawn.forward (trục Z).
// - Up cho LookRotation = shuttleSpawn.right (trục X).

[RequireComponent(typeof(Collider))]
public class RacketHitZone : MonoBehaviour
{
    [Header("Filter")]
    public string shuttlecockTag = "Shuttlecock";

    [Header("References")]
    public Transform shuttleSpawn;        // điểm xuất phát trả cầu (con của Racket bạn). Hướng bắn = shuttleSpawn.forward.
    public GameObject shuttlecockPrefab;  // cần nếu bật respawnNewShuttle

    [Header("Return Ball Settings")]
    public bool respawnNewShuttle = false;   // true = hủy & sinh mới; false = tái sử dụng trái cầu cũ
    public float spawnOffset = 0.02f;        // đẩy origin ra khỏi mặt vợt 1 đoạn
    public bool teleportIncomingToSpawn = true; // khi tái sử dụng, có thể dịch cầu cũ về shuttleSpawn trước khi bắn

    [Header("Exit Speed Model")]
    public bool useIncomingSpeed = true; // lấy speed ra dựa trên speed vào * cor
    [Range(0.0f, 1.2f)]
    public float cor = 0.9f;            // hệ số "bật" đơn giản
    public float baseExitSpeed = 20f;   // tốc độ sàn khi speed vào nhỏ
    public AnimationCurve exitSpeedByIncoming; // tùy chọn, X=inSpeed, Y=outSpeed (nếu dùng, sẽ override model cor)

    [Header("Cleanup")]
    public float rehitCooldown = 0.12f; // chống trigger nhiều lần trên 1 lần chạm
    public bool zeroAngularOnExit = true;

    [Header("Slow-Mo (optional)")]
    [Tooltip("If true, trigger slow-motion when a valid shuttle enters this zone.")]
    public bool triggerSlowMoOnHit = true;

    [Tooltip("Profile used for the slow-motion window when shuttle enters this zone.")]
    public TimeDilationSettings slowMoProfile;

    [Tooltip("If true, slow-motion will be kept ONLY while the shuttle stays inside this trigger. It cancels on exit.")]
    public bool slowMoWhileInsideOnly = true;

    [Header("Debug")]
    public bool logOnHit = true;

    private Collider zone;
    private readonly Dictionary<int, float> lastHitTime = new Dictionary<int, float>();

    // Map each shuttle instance -> active slow-mo token (for WhileInside cancellation)
    private readonly Dictionary<int, int> activeSlowMoTokens = new Dictionary<int, int>();

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
            Debug.LogWarning("[RacketHitZone] Collider is not trigger. Consider enabling IsTrigger to avoid unwanted physics push.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Start slow-mo if needed, then handle return logic
        MaybeBeginSlowMo(other);
        TryHandle(other);
    }

    void OnTriggerExit(Collider other)
    {
        MaybeCancelSlowMo(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Trong trường hợp dùng collider non-trigger (ít khuyến nghị), cố gắng mô phỏng hành vi tương tự
        MaybeBeginSlowMo(collision.collider);
        TryHandle(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        MaybeCancelSlowMo(collision.collider);
    }

    void MaybeBeginSlowMo(Collider other)
    {
        if (!triggerSlowMoOnHit || slowMoProfile == null) return;
        if (TimeDilationManager.Instance == null) return;
        if (!IsValidShuttle(other, out var rb)) return;

        int id = rb.GetInstanceID();
        // Nếu đã có token (đang ở trong vùng), không mở lại
        if (activeSlowMoTokens.ContainsKey(id)) return;

        // Bắt đầu window. Nếu WhileInsideOnly, chúng ta vẫn dùng holdSeconds như dự phòng (fallback),
        // nhưng sẽ chủ động cancel ngay khi EXIT.
        int token = TimeDilationManager.Instance.BeginWindow(slowMoProfile, reason: "HitZone:WhileInside");
        if (token > 0)
        {
            activeSlowMoTokens[id] = token;
        }
    }

    void MaybeCancelSlowMo(Collider other)
    {
        if (TimeDilationManager.Instance == null) return;
        if (!IsValidShuttle(other, out var rb)) return;

        int id = rb.GetInstanceID();
        if (activeSlowMoTokens.TryGetValue(id, out int token))
        {
            if (slowMoWhileInsideOnly)
            {
                TimeDilationManager.Instance.CancelWindow(token);
            }
            activeSlowMoTokens.Remove(id);
        }
    }

    bool IsValidShuttle(Collider other, out Rigidbody rb)
    {
        rb = null;
        if (!other || !other.gameObject.activeInHierarchy) return false;
        if (!string.IsNullOrEmpty(shuttlecockTag) && !other.CompareTag(shuttlecockTag)) return false;
        rb = other.attachedRigidbody;
        return rb != null;
    }

    void TryHandle(Collider other)
    {
        if (!IsValidShuttle(other, out var rb)) return;

        // Cooldown theo instanceID để tránh lặp quá nhanh
        int id = rb.GetInstanceID();
        float now = Time.time;
        if (lastHitTime.TryGetValue(id, out float last) && now - last < rehitCooldown) return;
        lastHitTime[id] = now;

        // Lấy hướng bắn theo trục Z của shuttleSpawn (nếu có) hoặc trục Z của Zone
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Vector3 dir = basis.forward.normalized;
        Vector3 up = basis.right.normalized; // Up theo yêu cầu là trục X

        // Tính speed vào
#if UNITY_6000_0_OR_NEWER
        float inSpeed = rb.linearVelocity.magnitude;
#else
        float inSpeed = rb.velocity.magnitude;
#endif

        // Tính speed ra
        float outSpeed = baseExitSpeed;
        if (exitSpeedByIncoming != null && exitSpeedByIncoming.keys != null && exitSpeedByIncoming.length > 0)
        {
            outSpeed = Mathf.Max(exitSpeedByIncoming.Evaluate(inSpeed), baseExitSpeed);
        }
        else if (useIncomingSpeed)
        {
            outSpeed = Mathf.Max(inSpeed * Mathf.Clamp01(cor), baseExitSpeed);
        }

        if (respawnNewShuttle)
        {
            // Nếu WhileInsideOnly: hủy slow-mo của shuttle này trước khi Destroy để tránh kẹt
            if (activeSlowMoTokens.TryGetValue(id, out int token))
            {
                if (slowMoWhileInsideOnly && TimeDilationManager.Instance != null)
                {
                    TimeDilationManager.Instance.CancelWindow(token);
                }
                activeSlowMoTokens.Remove(id);
            }

            // Hủy & sinh mới
            Vector3 origin = basis.position + dir * spawnOffset;
            Quaternion rot = Quaternion.LookRotation(dir, up);

            var oldGo = rb.gameObject;
            Destroy(oldGo);

            GameObject go = Instantiate(shuttlecockPrefab, origin, rot);
            var newRb = go.GetComponent<Rigidbody>();
            if (newRb != null)
            {
#if UNITY_6000_0_OR_NEWER
                newRb.linearVelocity = dir * outSpeed;
#else
                newRb.velocity = dir * outSpeed;
#endif
                if (zeroAngularOnExit) newRb.angularVelocity = Vector3.zero;
            }

            if (logOnHit)
            {
                Debug.Log($"[RacketHitZone] Respawned shuttle. inSpeed={inSpeed:F2} -> outSpeed={outSpeed:F2}, dir={dir}");
                Debug.DrawLine(origin, origin + dir * 0.5f, Color.cyan, 1f);
            }
        }
        else
        {
            // Tái sử dụng trái cầu hiện tại
            if (teleportIncomingToSpawn)
            {
                Vector3 origin = basis.position + dir * spawnOffset;
#if UNITY_6000_0_OR_NEWER
                rb.position = origin;
#else
                rb.position = origin;
#endif
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = dir * outSpeed;
#else
            rb.velocity = dir * outSpeed;
#endif
            if (zeroAngularOnExit) rb.angularVelocity = Vector3.zero;

            // Căn lại orientation của shuttle (tùy chọn, chỉ để nhìn cho đúng)
            other.transform.rotation = Quaternion.LookRotation(dir, up);

            if (logOnHit)
            {
                Debug.Log($"[RacketHitZone] Returned shuttle (reuse). inSpeed={inSpeed:F2} -> outSpeed={outSpeed:F2}, dir={dir}");
                Debug.DrawLine(rb.position, rb.position + dir * 0.5f, Color.green, 1f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(basis.position, 0.008f);
        Gizmos.DrawLine(basis.position, basis.position + basis.forward * 0.3f);
    }
}