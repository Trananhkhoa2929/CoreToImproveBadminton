using UnityEngine;

/// <summary>
/// One-shot launch plan consumed by RacketHitZone on contact.
/// </summary>
public struct PreparedShot
{
    public Vector3 launchVelocity;   // absolute world-space velocity to apply
    public Vector3 upVector;         // for visual orientation (LookRotation(dir, up)), optional
    public float createdTime;        // for debugging/timeout if needed
}

/// <summary>
/// Provider interface RacketHitZone can query on shuttle contact.
/// Implementation should return a plan only once per shot (consume-once).
/// </summary>
public interface IShotPlanProvider
{
    bool HasPlan { get; }
    bool TryConsume(out PreparedShot plan);
}