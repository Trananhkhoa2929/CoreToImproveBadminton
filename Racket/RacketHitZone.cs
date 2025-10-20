using System.Collections.Generic;
using UnityEngine;

// Gắn script này lên GameObject có BoxCollider (Is Trigger = true) phủ mặt vợt của BẠN.
// Khi Shuttlecock (tag) đi vào trigger, script sẽ:
// - Lấy hướng bắn theo shuttleSpawn.forward (trục Z cục bộ của Racket bạn).
// - Tính tốc độ ra theo COR đơn giản hoặc theo baseExitSpeed.
// - Chọn 1: Thay đổi trực tiếp vận tốc trái cầu hiện tại.
// - Chọn 2: Hủy trái cầu cũ, sinh trái cầu mới ở shuttleSpawn (prefab), đặt vận tốc mới.
//   (dùng respawnNewShuttle = true nếu muốn "hủy & sinh mới").
//
// Lưu ý:
// - Theo mô tả trục của bạn: forward (Z) là hướng bắn, "up" = trục X. Script set rotation = LookRotation(dir, shuttleSpawn.right).
// - Collider nên là trigger để tránh lực đẩy vật lý không mong muốn.
// - Sử dụng rb.linearVelocity cho đồng bộ với SimpleShuttlecockPhysics của bạn.

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
            Debug.LogWarning("[RacketHitZone] Collider is not trigger. Consider enabling IsTrigger to avoid unwanted physics push.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryHandle(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Cho trường hợp bạn muốn dùng collider non-trigger (ít khuyên dùng)
        TryHandle(collision.collider);
    }

    void TryHandle(Collider other)
    {
        if (!other || !other.gameObject.activeInHierarchy) return;
        if (!string.IsNullOrEmpty(shuttlecockTag) && !other.CompareTag(shuttlecockTag)) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        // Cooldown theo instanceID để tránh lặp
        int id = rb.GetInstanceID();
        float now = Time.time;
        if (lastHitTime.TryGetValue(id, out float last) && now - last < rehitCooldown) return;
        lastHitTime[id] = now;

        // Lấy hướng bắn theo trục Z của shuttleSpawn (nếu có) hoặc trục Z của Zone
        Transform basis = shuttleSpawn != null ? shuttleSpawn : transform;
        Vector3 dir = basis.forward.normalized;
        Vector3 up = basis.right.normalized; // Up theo yêu cầu là trục X

        // Tính speed vào
        float inSpeed = rb.linearVelocity.magnitude;

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
            // Hủy & sinh mới
            Vector3 origin = basis.position + dir * spawnOffset;
            Quaternion rot = Quaternion.LookRotation(dir, up);

            // Hủy cũ
            var oldGo = rb.gameObject;
            // Nếu dùng pooling sau này, hãy thay bằng trả pool
            Destroy(oldGo);

            // Tạo mới
            GameObject go = Instantiate(shuttlecockPrefab, origin, rot);
            var newRb = go.GetComponent<Rigidbody>();
            if (newRb != null)
            {
                newRb.linearVelocity = dir * outSpeed;
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
                rb.position = origin;
            }

            rb.linearVelocity = dir * outSpeed;
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