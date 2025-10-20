//using UnityEngine;

//// RacketController đơn giản:
//// - Chỉ xoay quanh Y (theo chuột X) và Z (theo chuột Y), giữ nguyên vị trí pivot.
//// - Không dùng Input System, chỉ dùng Input.GetAxis để đơn giản.
//// - Clamp góc Y/Z giống RacketMouseFollow bạn gửi.
//// - Expose ShuttleSpawn để launcher khác dùng.

//public class RacketMouseFollow : MonoBehaviour
//{
//    [Header("Mouse Look (giống RacketMouseFollow)")]
//    public float sensitivity = 2f;          // tốc độ xoay
//    public float minY = -45f, maxY = 45f;   // clamp trục Y (yaw)
//    public float minZ = 45f, maxZ = 135f;  // clamp trục Z (tilt); Z=90 là thẳng

//    [Tooltip("Khóa vị trí pivot để không bị dịch chuyển bởi script khác.")]
//    public bool lockPivotPosition = true;

//    [Header("Shuttle Spawn (điểm bắn)")]
//    public Transform shuttleSpawn;          // điểm bắn, nên là con của RacketPivot (root scale 1,1,1)

//    [Header("Khởi tạo")]
//    [Tooltip("Ép vợt đứng thẳng theo up=Vector3.up khi Play (không bắt buộc).")]
//    public bool forceUprightOnPlay = true;

//    // trạng thái góc
//    private float rotationY;
//    private float rotationZ;

//    // ghim vị trí pivot
//    private Vector3 pinnedPosition;

//    void Start()
//    {
//        // Lưu vị trí hiện tại để ghim (nếu cần)
//        pinnedPosition = transform.position;

//        // Lấy góc ban đầu
//        Vector3 angles = transform.localEulerAngles;
//        rotationY = angles.y;
//        rotationZ = angles.z;

//        if (forceUprightOnPlay)
//        {
//            // Giữ dáng tổng thể, nhưng định hướng lại theo trục đứng nếu cần
//            // Bạn có thể tắt nếu muốn giữ nguyên pose trong Editor
//            transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
//            var e = transform.localEulerAngles;
//            rotationY = e.y;
//            rotationZ = e.z;
//        }
//    }

//    void Update()
//    {
//        // Đọc input chuột (legacy)
//        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
//        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

//        // Map: chuột X -> quay quanh trục Y; chuột Y -> quay quanh Z (dấu âm để "úp" khi đẩy chuột lên)
//        rotationY += mouseX;
//        rotationZ -= mouseY;

//        // Clamp giới hạn
//        rotationY = Mathf.Clamp(rotationY, minY, maxY);
//        rotationZ = Mathf.Clamp(rotationZ, minZ, maxZ);

//        // Giữ X cố định = 0; chỉ đổi Y/Z
//        transform.localRotation = Quaternion.Euler(0f, rotationY, rotationZ);

//        // Ghim vị trí pivot nếu cần
//        if (lockPivotPosition && transform.position != pinnedPosition)
//            transform.position = pinnedPosition;
//    }

//    // Hướng ngắm (normal mặt vợt) cho launcher: ưu tiên spawn.forward, fallback transform.forward
//    public Vector3 GetAimDirection()
//    {
//        if (shuttleSpawn != null) return shuttleSpawn.forward;
//        return transform.forward;
//    }

//    // Vị trí bắn
//    public Vector3 GetSpawnPosition(float offset = 0f)
//    {
//        if (shuttleSpawn != null)
//            return shuttleSpawn.position + GetAimDirection() * offset;
//        return transform.position + GetAimDirection() * offset;
//    }
//}

using UnityEngine;

// RacketController đơn giản:
// - Chỉ xoay quanh Y (theo chuột X) và Z (theo chuột Y), giữ nguyên vị trí pivot.
// - Có tuỳ chọn đảo hướng chuột (invert X/Y).
// - Clamp góc Y/Z giống RacketMouseFollow.
// - Expose ShuttleSpawn để launcher khác dùng.

public class RacketMouseFollow : MonoBehaviour
{
    [Header("Mouse Look (giống RacketMouseFollow)")]
    public float sensitivity = 2f;          // tốc độ xoay
    public float minY = -45f, maxY = 45f;   // clamp trục Y (yaw)
    public float minZ = 45f, maxZ = 135f;   // clamp trục Z (tilt); Z=90 là thẳng

    [Header("Invert Controls")]
    public bool invertX = false;             // đảo hướng chuột ngang
    public bool invertY = false;             // đảo hướng chuột dọc

    [Tooltip("Khóa vị trí pivot để không bị dịch chuyển bởi script khác.")]
    public bool lockPivotPosition = true;

    [Header("Shuttle Spawn (điểm bắn)")]
    public Transform shuttleSpawn;          // điểm bắn, nên là con của RacketPivot (root scale 1,1,1)

    [Header("Khởi tạo")]
    [Tooltip("Ép vợt đứng thẳng theo up=Vector3.up khi Play (không bắt buộc).")]
    public bool forceUprightOnPlay = true;

    // trạng thái góc
    private float rotationY;
    private float rotationZ;

    // ghim vị trí pivot
    private Vector3 pinnedPosition;

    void Start()
    {
        // Lưu vị trí hiện tại để ghim (nếu cần)
        pinnedPosition = transform.position;

        // Lấy góc ban đầu
        Vector3 angles = transform.localEulerAngles;
        rotationY = angles.y;
        rotationZ = angles.z;

        if (forceUprightOnPlay)
        {
            transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            var e = transform.localEulerAngles;
            rotationY = e.y;
            rotationZ = e.z;
        }
    }

    void Update()
    {
        // Đọc input chuột (legacy)
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * (invertX ? -1f : 1f);
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * (invertY ? -1f : 1f);

        // Map: chuột X -> quay quanh trục Y; chuột Y -> quay quanh Z
        rotationY += mouseX;
        rotationZ -= mouseY;

        // Clamp giới hạn
        rotationY = Mathf.Clamp(rotationY, minY, maxY);
        rotationZ = Mathf.Clamp(rotationZ, minZ, maxZ);

        // Giữ X cố định = 0; chỉ đổi Y/Z
        transform.localRotation = Quaternion.Euler(0f, rotationY, rotationZ);

        // Ghim vị trí pivot nếu cần
        if (lockPivotPosition && transform.position != pinnedPosition)
            transform.position = pinnedPosition;
    }

    // Hướng ngắm (normal mặt vợt) cho launcher: ưu tiên spawn.forward, fallback transform.forward
    public Vector3 GetAimDirection()
    {
        if (shuttleSpawn != null) return shuttleSpawn.forward;
        return transform.forward;
    }

    // Vị trí bắn
    public Vector3 GetSpawnPosition(float offset = 0f)
    {
        if (shuttleSpawn != null)
            return shuttleSpawn.position + GetAimDirection() * offset;
        return transform.position + GetAimDirection() * offset;
    }
}
