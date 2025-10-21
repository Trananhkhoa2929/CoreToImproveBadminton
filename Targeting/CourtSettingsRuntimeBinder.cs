using UnityEngine;

/// <summary>
/// Tự động sao chép Bounds của Collider trong Scene vào Asset CourtSettings khi game chạy.
/// Đặt script này lên một đối tượng bất kỳ trong Scene.
/// </summary>
public class CourtSettingsRuntimeBinder : MonoBehaviour
{
    [Header("Thứ cần kết nối")]
    [Tooltip("Asset CourtSettings cần được cập nhật")]
    public CourtSettings courtSettingsAsset;

    [Tooltip("Collider của sân đối thủ (trong Scene)")]
    public Collider opponentCourtColliderInScene;

    void Awake()
    {
        ApplyBounds();
    }

    [ContextMenu("Apply Bounds Now")]
    public void ApplyBounds()
    {
        if (courtSettingsAsset == null || opponentCourtColliderInScene == null)
        {
            Debug.LogWarning("CourtSettingsRuntimeBinder: Thiếu Asset hoặc Collider!", this);
            return;
        }

        // Đây là thao tác mấu chốt:
        // Lấy bounds từ Scene Collider và chép nó vào các trường thủ công của Asset
        var b = opponentCourtColliderInScene.bounds;
        courtSettingsAsset.minXZ = new Vector2(b.min.x, b.min.z);
        courtSettingsAsset.maxXZ = new Vector2(b.max.x, b.max.z);

        Debug.Log($"[CourtSettingsRuntimeBinder] Đã tự động cập nhật bounds cho {courtSettingsAsset.name}: " +
                  $"Min({b.min.x}, {b.min.z}) - Max({b.max.x}, {b.max.z})");
    }
}