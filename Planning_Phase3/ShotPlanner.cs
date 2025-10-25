using UnityEngine;

/// <summary>
/// Phase 3 planner utilities:
/// - Map a "perfect" solution (from Phase 2) to a sweet-spot power fraction.
/// - Build a PreparedShot from power01, maxLaunchSpeed, and small manual overrides.
/// Notes:
/// - Phase 3 v?n dùng gravity-only ?? seed "perfect speed". Phase 5 s? ??ng b? v?i drag-aware solver.
/// </summary>
public static class ShotPlanner
{
    public struct SweetSpotInfo
    {
        public float perfectPower01; // v? trí sweet spot trên [0..1]
        public float sweetWidth01;   // b? r?ng d?i sweet spot (ví d? 0.06)
        public float perfectSpeed;   // m/s
    }

    /// <summary>
    /// Compute sweet spot location from perfect speed and a maxLaunchSpeed budget.
    /// </summary>
    public static SweetSpotInfo ComputeSweetSpot(float perfectSpeed, float maxLaunchSpeed, float sweetWidth01 = 0.06f)
    {
        float p = Mathf.Clamp01(maxLaunchSpeed > 1e-3f ? perfectSpeed / maxLaunchSpeed : 0f);
        return new SweetSpotInfo
        {
            perfectPower01 = p,
            sweetWidth01 = Mathf.Clamp01(sweetWidth01),
            perfectSpeed = perfectSpeed
        };
    }

    /// <summary>
    /// Build final launch velocity based on:
    /// - baseDirection: normalized planned direction (from Phase 2 chosen.launchVelocity)
    /// - power01: user's power on release (0..1)
    /// - maxLaunchSpeed: cap speed
    /// - manual yaw/pitch (deg) small override in local frame defined by (baseDirection, upBasis)
    /// Returns PreparedShot (velocity + upVector for look).
    /// </summary>
    public static PreparedShot BuildPreparedShot(
        Vector3 origin,
        Vector3 baseDirection,
        float power01,
        float maxLaunchSpeed,
        float yawOffsetDeg,
        float pitchOffsetDeg,
        Vector3 upBasis)
    {
        Vector3 dir = baseDirection.sqrMagnitude > 1e-8f ? baseDirection.normalized : Vector3.forward;

        // Build a stable local frame from baseDirection:
        Vector3 upRef = upBasis.sqrMagnitude > 1e-8f ? upBasis.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(upRef, dir).normalized;
        if (right.sqrMagnitude < 1e-8f)
        {
            // Fallback if upRef almost colinear with dir
            upRef = Vector3.up;
            right = Vector3.Cross(upRef, dir).normalized;
        }
        Vector3 up = Vector3.Cross(dir, right).normalized;

        // Apply small manual offsets (yaw around up, then pitch around right)
        Quaternion qYaw = Quaternion.AngleAxis(yawOffsetDeg, up);
        Quaternion qPitch = Quaternion.AngleAxis(pitchOffsetDeg, right);
        dir = (qYaw * qPitch) * dir;
        dir.Normalize();

        float speed = Mathf.Clamp01(power01) * Mathf.Max(0f, maxLaunchSpeed);
        Vector3 v0 = dir * speed;

        return new PreparedShot
        {
            launchVelocity = v0,
            upVector = right,     // per your convention, Racket uses LookRotation(dir, spawn.right). Here we give a stable up hint (right).
            createdTime = Time.time
        };
    }
}