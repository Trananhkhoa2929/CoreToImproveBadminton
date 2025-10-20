using System.Collections.Generic;
using UnityEngine;

public class RacketMover : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 1.4f;
    public float acceleration = 30f;
    public float deceleration = 40f;

    [Header("Input")]
    public bool useCameraRelative = true;       // WASD theo hướng camera
    public Transform reference;                 // Mặc định tự lấy Camera.main nếu null
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Plane & Height")]
    public bool keepYAtStart = true;            // Giữ độ cao Y cố định
    public float fixedY = 0f;                   // Sẽ ghi đè bằng vị trí Y lúc Start nếu keepYAtStart = true

    [Header("Court Bounds (optional)")]
    public bool clampToCourt = false;
    public Collider courtBoundaryCollider;      // Nếu set, dùng collider.bounds để clamp
    public Vector2 minXZ = new Vector2(-5, -5); // Nếu không có collider, dùng manual
    public Vector2 maxXZ = new Vector2(5, 5);

    [Header("Auto Intercept (optional)")]
    public bool autoIntercept = false;          // Bật để tự đón cầu gần nhất
    public string shuttleTag = "Shuttlecock";
    public float predictionTime = 0.25f;        // Dự đoán vị trí cầu sau t giây
    public float interceptRefresh = 0.1f;       // Tần suất cập nhật mục tiêu
    public float arriveStopDistance = 0.05f;    // Dừng khi gần mục tiêu
    public bool drawInterceptGizmos = true;

    private Rigidbody rb;
    private Vector3 velocityXZ = Vector3.zero;
    private float nextInterceptUpdateTime = 0f;
    private Vector3 interceptTarget;
    private float startY;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (reference == null && Camera.main != null) reference = Camera.main.transform;
    }

    void Start()
    {
        startY = transform.position.y;
        if (keepYAtStart) fixedY = startY;
        // Khuyến nghị dùng Rigidbody isKinematic = true nếu có Rigidbody
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Không ép isKinematic ở đây, để bạn tự quyết. Nhưng khuyên bật isKinematic = true.
        }
    }

    void Update()
    {
        // 1) Lấy hướng mong muốn (từ input hoặc auto intercept)
        Vector3 desiredDir = Vector3.zero;

        if (autoIntercept)
        {
            desiredDir = GetAutoInterceptDirection();
        }
        else
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S
            desiredDir = BuildMoveDirection(h, v);
        }

        // 2) Tính desired velocity theo tốc độ/sprint
        float sprint = (Input.GetKey(sprintKey) ? sprintMultiplier : 1f);
        Vector3 desiredVel = desiredDir.normalized * moveSpeed * sprint;

        // 3) Tăng/giảm tốc (mượt)
        float accel = desiredVel.sqrMagnitude > 0.0001f
            ? acceleration
            : deceleration;

        velocityXZ = Vector3.MoveTowards(velocityXZ, desiredVel, accel * Time.deltaTime);

        // 4) Tính vị trí mới (giữ trên mặt phẳng XZ)
        Vector3 delta = velocityXZ * Time.deltaTime;
        Vector3 newPos = transform.position + delta;

        if (keepYAtStart) newPos.y = fixedY;

        if (clampToCourt)
        {
            newPos = ClampToBounds(newPos);
        }

        // 5) Áp dụng dịch chuyển
        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(newPos);
        }
        else
        {
            transform.position = newPos;
        }
    }

    Vector3 BuildMoveDirection(float h, float v)
    {
        Vector3 fwd, right;
        if (useCameraRelative && reference != null)
        {
            fwd = reference.forward; fwd.y = 0f; fwd.Normalize();
            right = reference.right; right.y = 0f; right.Normalize();
        }
        else
        {
            fwd = Vector3.forward; right = Vector3.right;
        }

        Vector3 dir = fwd * v + right * h;
        dir.y = 0f;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    Vector3 ClampToBounds(Vector3 worldPos)
    {
        if (courtBoundaryCollider != null)
        {
            var b = courtBoundaryCollider.bounds;
            worldPos.x = Mathf.Clamp(worldPos.x, b.min.x, b.max.x);
            worldPos.z = Mathf.Clamp(worldPos.z, b.min.z, b.max.z);
        }
        else
        {
            worldPos.x = Mathf.Clamp(worldPos.x, minXZ.x, maxXZ.x);
            worldPos.z = Mathf.Clamp(worldPos.z, minXZ.y, maxXZ.y);
        }
        return worldPos;
    }

    Vector3 GetAutoInterceptDirection()
    {
        // Cập nhật mục tiêu theo tần suất
        if (Time.time >= nextInterceptUpdateTime)
        {
            nextInterceptUpdateTime = Time.time + interceptRefresh;
            interceptTarget = ComputeInterceptTarget();
        }

        Vector3 toTarget = interceptTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude <= arriveStopDistance) return Vector3.zero;
        return toTarget.normalized;
    }

    Vector3 ComputeInterceptTarget()
    {
        var shuttles = GameObject.FindGameObjectsWithTag(shuttleTag);
        if (shuttles == null || shuttles.Length == 0) return transform.position;

        // Tìm shuttle gần nhất theo khoảng cách ngang (XZ)
        GameObject best = null;
        float bestDist = float.PositiveInfinity;

        Vector3 p = transform.position;
        foreach (var s in shuttles)
        {
            if (s == null) continue;
            Vector3 sp = s.transform.position;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(sp.x, sp.z));
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }

        if (best == null) return transform.position;

        // Dự đoán vị trí sau predictionTime (nếu có Rigidbody)
        Vector3 pos = best.transform.position;
        Vector3 vel = Vector3.zero;

        if (best.TryGetComponent<Rigidbody>(out var shuttleRb))
        {
#if UNITY_6000_0_OR_NEWER
            vel = shuttleRb.linearVelocity;
#else
            vel = shuttleRb.velocity;
#endif
        }

        Vector3 predicted = pos + vel * predictionTime;
        if (keepYAtStart) predicted.y = fixedY;

        if (clampToCourt) predicted = ClampToBounds(predicted);

        return predicted;
    }

    void OnDrawGizmosSelected()
    {
        if (drawInterceptGizmos && autoIntercept)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(interceptTarget, 0.025f);
        }

        if (clampToCourt)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            if (courtBoundaryCollider != null)
            {
                var b = courtBoundaryCollider.bounds;
                Vector3 c = new Vector3((b.min.x + b.max.x) * 0.5f, transform.position.y, (b.min.z + b.max.z) * 0.5f);
                Vector3 size = new Vector3(b.size.x, 0.01f, b.size.z);
                Gizmos.DrawCube(c, size);
            }
            else
            {
                Vector3 c = new Vector3((minXZ.x + maxXZ.x) * 0.5f, transform.position.y, (minXZ.y + maxXZ.y) * 0.5f);
                Vector3 size = new Vector3(Mathf.Abs(maxXZ.x - minXZ.x), 0.01f, Mathf.Abs(maxXZ.y - minXZ.y));
                Gizmos.DrawCube(c, size);
            }
        }
    }
}
