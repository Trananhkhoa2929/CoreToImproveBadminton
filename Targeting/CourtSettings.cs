using UnityEngine;

[CreateAssetMenu(fileName = "CourtSettings", menuName = "SuperRealife/Court Settings", order = 11)]
public class CourtSettings : ScriptableObject
{
    [Header("Opponent Court Bounds (World Space)")]
    [Tooltip("If assigned, use this collider's bounds (XZ) for the opponent side. Otherwise, use manual rectangle below.")]
    public Collider opponentCourtCollider;

    [Tooltip("Manual XZ bounds for opponent side when collider is not assigned.")]
    public Vector2 minXZ = new Vector2(-3.0f, 1.0f);  // x, z
    public Vector2 maxXZ = new Vector2(3.0f, 7.0f);   // x, z

    [Header("Net Settings (World Space)")]
    [Tooltip("World Z of the net plane (assuming net is aligned along X and spans across court).")]
    public float netPlaneZ = 0.0f;
    [Tooltip("Net tape top height (Y).")]
    public float netTopY = 1.55f;
    [Tooltip("Required extra clearance over net (meters).")]
    public float netClearanceMargin = 0.10f;

    [Header("Targeting")]
    [Tooltip("Layer mask used for raycasting target on opponent court surface.")]
    public LayerMask targetableCourtMask;

    [Tooltip("Clamp target into opponent bounds automatically.")]
    public bool clampIntoBounds = true;

    [Header("Debug")]
    public bool drawBoundsGizmo = true;
    public Color boundsColor = new Color(0f, 1f, 0f, 0.12f);

    public bool TryClampIntoOpponentBounds(Vector3 world, out Vector3 clamped)
    {
        clamped = world;
        // Use collider bounds if available
        if (opponentCourtCollider != null)
        {
            var b = opponentCourtCollider.bounds;
            clamped.x = Mathf.Clamp(world.x, b.min.x, b.max.x);
            clamped.z = Mathf.Clamp(world.z, b.min.z, b.max.z);
            return true;
        }
        // Manual bounds
        clamped.x = Mathf.Clamp(world.x, minXZ.x, maxXZ.x);
        clamped.z = Mathf.Clamp(world.z, minXZ.y, maxXZ.y);
        return true;
    }

    public bool IsInsideOpponentBounds(Vector3 world)
    {
        if (opponentCourtCollider != null)
        {
            var b = opponentCourtCollider.bounds;
            return (world.x >= b.min.x && world.x <= b.max.x &&
                    world.z >= b.min.z && world.z <= b.max.z);
        }
        return (world.x >= minXZ.x && world.x <= maxXZ.x &&
                world.z >= minXZ.y && world.z <= maxXZ.y);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawBoundsGizmo) return;

        Gizmos.color = boundsColor;
        if (opponentCourtCollider != null)
        {
            var b = opponentCourtCollider.bounds;
            var center = new Vector3((b.min.x + b.max.x) * 0.5f, Mathf.Max(0.01f, b.center.y), (b.min.z + b.max.z) * 0.5f);
            var size = new Vector3(b.size.x, 0.02f, b.size.z);
            Gizmos.DrawCube(center, size);
        }
        else
        {
            var center = new Vector3((minXZ.x + maxXZ.x) * 0.5f, 0.02f, (minXZ.y + maxXZ.y) * 0.5f);
            var size = new Vector3(Mathf.Abs(maxXZ.x - minXZ.x), 0.02f, Mathf.Abs(maxXZ.y - minXZ.y));
            Gizmos.DrawCube(center, size);
        }
    }
}