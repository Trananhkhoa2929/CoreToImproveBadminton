//using System.Collections;
//using UnityEngine;

//// SimpleOpponentFeeder
//// - Bắn theo duy nhất một hướng (không target, không jitter).
//// - Điều chỉnh hướng bắn bằng:
////   + Xoay trực tiếp Transform spawnPoint trong Editor (khuyến nghị).
////   + Hoặc bật useAngleAdjustments và chỉnh pan/tilt theo trục cục bộ của spawnPoint.
//// - Điều chỉnh lực bắn bằng launchSpeed.
//// - Có thể bắn tự động theo chu kỳ (autoFire) hoặc bắn thủ công bằng phím (launchKey).
////
//// Quy ước trục theo mô tả của bạn:
//// - Hướng bắn mặc định = spawnPoint.forward (trục Z).
//// - "Up" mong muốn = spawnPoint.right (trục X), dùng làm tham chiếu khi set rotation để nhìn đúng mặt vợt.

//public class SimpleOpponentFeeder : MonoBehaviour
//{
//    [Header("Prefab & Spawn")]
//    public GameObject shuttlecockPrefab;
//    public Transform spawnPoint;

//    [Tooltip("Đẩy origin ra khỏi mặt vợt một đoạn nhỏ để tránh cấn collider.")]
//    public float spawnOffset = 0.02f;

//    [Header("Shot Power")]
//    [Tooltip("Tốc độ bắn (m/s). Tăng/giảm để chỉnh lực.")]
//    public float launchSpeed = 22f;

//    [Header("Aim (One Direction)")]
//    [Tooltip("Mặc định dùng hướng spawnPoint.forward (trục Z).")]
//    public bool useAngleAdjustments = false;

//    [Tooltip("Xoay trái/phải quanh trục cục bộ 'up' của spawnPoint (thường là trục Y của nó).")]
//    public float panLeftRightDeg = 0f;

//    [Tooltip("Ngửa/úp mặt vợt quanh trục cục bộ 'right' của spawnPoint (trục X).")]
//    public float tiltUpDownDeg = 0f;

//    [Header("Fire Control")]
//    public bool autoFire = true;
//    public float intervalSeconds = 1.5f;

//    public bool enableManualKey = true;
//    public KeyCode launchKey = KeyCode.Return; // Enter

//    [Header("Debug")]
//    public bool logEachShot = false;
//    public Color gizmoColor = Color.magenta;

//    private Coroutine loop;

//    void Start()
//    {
//        if (autoFire) StartFeeding();
//    }

//    void OnDisable()
//    {
//        StopFeeding();
//    }

//    void Update()
//    {
//        if (enableManualKey && Input.GetKeyDown(launchKey))
//        {
//            FireOnce();
//        }
//    }

//    public void StartFeeding()
//    {
//        if (loop != null) StopCoroutine(loop);
//        loop = StartCoroutine(FeedLoop());
//    }

//    public void StopFeeding()
//    {
//        if (loop != null)
//        {
//            StopCoroutine(loop);
//            loop = null;
//        }
//    }

//    IEnumerator FeedLoop()
//    {
//        var wait = new WaitForSeconds(intervalSeconds);
//        while (true)
//        {
//            FireOnce();
//            yield return wait;
//        }
//    }

//    public void FireOnce()
//    {
//        if (shuttlecockPrefab == null || spawnPoint == null)
//        {
//            Debug.LogWarning("[SimpleOpponentFeeder] Missing prefab or spawnPoint.");
//            return;
//        }

//        // Hướng bắn cơ sở: trục Z của spawnPoint
//        Vector3 dir = spawnPoint.forward;

//        // Tùy chọn chỉnh góc bằng pan/tilt quanh trục cục bộ
//        if (useAngleAdjustments)
//        {
//            Quaternion qPan = Quaternion.AngleAxis(panLeftRightDeg, spawnPoint.up);     // quay quanh 'up' cục bộ
//            Quaternion qTilt = Quaternion.AngleAxis(tiltUpDownDeg, spawnPoint.right);   // quay quanh 'right' cục bộ (X)
//            dir = (qPan * qTilt) * dir;
//        }
//        dir.Normalize();

//        // Origin hơi nhô ra theo hướng bắn để tránh cấn mặt vợt
//        Vector3 origin = spawnPoint.position + dir * spawnOffset;

//        // Theo quy ước của bạn: 'up' mong muốn = trục X (right) của spawnPoint
//        Quaternion rot = Quaternion.LookRotation(dir, spawnPoint.right);

//        // Tạo cầu và đặt vận tốc
//        GameObject go = Instantiate(shuttlecockPrefab, origin, rot);
//        var rb = go.GetComponent<Rigidbody>();
//        if (rb != null)
//        {
//            // Bạn đang dùng SimpleShuttlecockPhysics => set linearVelocity để đồng bộ
//            rb.linearVelocity = dir * launchSpeed;
//        }
//        else
//        {
//            Debug.LogWarning("[SimpleOpponentFeeder] Shuttlecock prefab missing Rigidbody.");
//        }

//        if (logEachShot)
//        {
//            Debug.Log($"[SimpleOpponentFeeder] Fire dir={dir}, speed={launchSpeed}");
//            Debug.DrawLine(origin, origin + dir * 0.6f, Color.magenta, 1f);
//        }
//    }

//    void OnDrawGizmosSelected()
//    {
//        if (spawnPoint == null) return;
//        Gizmos.color = gizmoColor;

//        // Tính lại dir giống lúc bắn để preview
//        Vector3 dir = spawnPoint.forward;
//        if (useAngleAdjustments)
//        {
//            Quaternion qPan = Quaternion.AngleAxis(panLeftRightDeg, spawnPoint.up);
//            Quaternion qTilt = Quaternion.AngleAxis(tiltUpDownDeg, spawnPoint.right);
//            dir = (qPan * qTilt) * dir;
//        }
//        dir.Normalize();

//        Vector3 origin = spawnPoint.position + dir * spawnOffset;
//        Gizmos.DrawSphere(origin, 0.01f);
//        Gizmos.DrawLine(origin, origin + dir * 0.4f);
//    }
//}
using UnityEngine;

// SimpleOpponentFeeder: 1 máy bắn 1 hướng cố định, 1 lực cố định.
// - Không tự bắn theo vòng lặp. Chỉ bắn khi Director gọi FireOnce().
// - Hướng bắn mặc định = spawnPoint.forward (trục Z).
// - "Up" để set rotation = spawnPoint.right (trục X) theo quy ước bạn mô tả.
// - Điều chỉnh launchSpeed riêng cho từng máy (instance).

public class SimpleOpponentFeeder : MonoBehaviour
{
    [Header("Prefab & Spawn")]
    public GameObject shuttlecockPrefab;
    public Transform spawnPoint;
    [Tooltip("Đẩy origin ra khỏi mặt vợt một đoạn nhỏ để tránh cấn collider.")]
    public float spawnOffset = 0.02f;

    [Header("Shot Power (fixed per feeder)")]
    [Tooltip("Tốc độ bắn (m/s) của máy này.")]
    public float launchSpeed = 22f;

    [Header("Debug")]
    public bool logOnFire = false;
    public Color gizmoColor = Color.magenta;

    // Cho Director kiểm tra feeder đã sẵn sàng chưa
    public bool IsReady()
    {
        return enabled
            && gameObject.activeInHierarchy
            && shuttlecockPrefab != null
            && spawnPoint != null;
    }

    // Bắn 1 quả (được Director gọi)
    public void FireOnce()
    {
        if (!IsReady())
        {
            Debug.LogWarning($"[SimpleOpponentFeeder] Not ready on {name} (check prefab/spawnPoint).");
            return;
        }

        Vector3 dir = spawnPoint.forward.normalized;
        Vector3 origin = spawnPoint.position + dir * spawnOffset;
        Quaternion rot = Quaternion.LookRotation(dir, spawnPoint.right); // Up = X (right) theo quy ước của bạn.

        GameObject go = Object.Instantiate(shuttlecockPrefab, origin, rot);
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            // Đồng bộ với SimpleShuttlecockPhysics mà bạn đang dùng (linearVelocity)
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = dir * launchSpeed;
#else
            // Nếu editor chưa có linearVelocity, fallback về velocity
            rb.velocity = dir * launchSpeed;
#endif
        }
        else
        {
            Debug.LogWarning($"[SimpleOpponentFeeder] Shuttlecock prefab missing Rigidbody on {name}.");
        }

        if (logOnFire)
        {
            Debug.Log($"[SimpleOpponentFeeder] Fired by {name} | dir={dir} | speed={launchSpeed}");
            Debug.DrawLine(origin, origin + dir * 0.6f, gizmoColor, 1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(spawnPoint.position, 0.01f);
        Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 0.3f);
    }
}