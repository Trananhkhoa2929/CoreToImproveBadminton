using UnityEngine;

/// <summary>
/// Gravity-only ballistic solver and sampler (no drag).
/// Provides low/high arc solutions if exist, and sampling utilities for ghost rendering.
/// Coordinate-independent, uses world positions and Physics.gravity (or custom gravity if provided).
/// </summary>
public static class TrajectorySolver
{
    public struct Solution
    {
        public bool exists;
        public Vector3 launchVelocity; // v0 vector
        public float flightTime;       // estimated time to land at target (s)
        public float elevationDeg;     // elevation angle in degrees (relative to horizontal bearing)
        public bool isHighArc;         // true if high arc solution, false if low
    }

    /// <summary>
    /// Solve ballistic arc from origin to target with gravity-only.
    /// Returns low and high arc solutions (if exist).
    /// </summary>
    public static void SolveBallisticArcs(Vector3 origin, Vector3 target, Vector3 gravity, out Solution low, out Solution high)
    {
        low = default; high = default;

        Vector3 g = gravity;
        if (g.sqrMagnitude < 1e-6f) g = Physics.gravity;
        float gMag = g.magnitude;
        Vector3 gDir = g.normalized;

        Vector3 diff = target - origin;
        Vector2 diffXZ = new Vector2(diff.x, diff.z);
        float R = diffXZ.magnitude; // horizontal distance
        float dy = diff.y;          // vertical delta

        if (R < 1e-5f)
        {
            // Target directly above/below origin — treat as no horizontal solution
            low.exists = false; high.exists = false;
            return;
        }

        // Choose a gravity axis: assume gravity points mostly down Y (typical)
        // Use formula for launch speed given angle or for angle given speed.
        // We'll solve for angle first using standard equation:
        // tan? = (v^2 ± sqrt(v^4 - g (g R^2 + 2 dy v^2))) / (g R)
        // But that needs v. Instead, use the closed-form to compute ? directly is not trivial without v.
        // We'll derive using standard ballistic solution:
        // For given g, R, dy, solutions for elevation ? exist if:
        // D = R^2 * g^2 - g*(g*R^2 + 2*dy*g?) — Not ideal.
        // Better approach: Solve for v0 vector directly using textbook method by reorienting coordinates.

        // We'll use the well-known formula for elevation angles:
        //
        // Let g = |gravity|. Then:
        // v^2 = g*R^2 / (2*cos^2? * (R*tan? - dy))
        // For existence we need denominator > 0.
        //
        // We'll find low/high ? by solving quadratic in tan?:
        // g*R^2/(2*v^2) = cos^2? * (R*tan? - dy)
        // To avoid choosing v first, use classical result for ?:
        //
        // tan? = (v^2 ± sqrt(v^4 - g (g R^2 + 2 dy v^2))) / (g R)
        //
        // Because choosing v a priori is annoying, we can use direct closed-form for v0 vector using
        // "Ballistic Trajectory - closed form" technique (see Craig Reynolds note) by rotating frame.
        //
        // Practical approach: choose low/high via solving using parameter k = v^2.
        // We'll instead compute both solutions via classical method:
        // ?_low/high = atan( (v^2 ± sqrt(v^4 - g (g R^2 + 2 dy v^2))) / (g R) )
        //
        // To avoid picking v, we can use optimal-time solution for given apex sign.
        // For simplicity and stability in step 2, we will pick a "low-arc" default elevation
        // and compute v needed; then pick a "high-arc" elevation and compute v. This is robust.

        float[] candidateElevDeg = new float[] { 25f, 55f }; // low, high exemplars for step 2
        Solution[] sols = new Solution[2];

        for (int i = 0; i < candidateElevDeg.Length; i++)
        {
            sols[i] = SolveGivenElevation(origin, target, g, candidateElevDeg[i]);
        }

        // Choose valid ones
        Solution a = sols[0];
        Solution b = sols[1];

        // Mark flags
        a.isHighArc = false; b.isHighArc = true;

        low = a.exists ? a : default;
        high = b.exists ? b : default;
    }

    /// <summary>
    /// Compute launch velocity that hits target with a specified elevation angle (deg).
    /// Returns exists=false if denominator <= 0 or numerically unstable.
    /// </summary>
    public static Solution SolveGivenElevation(Vector3 origin, Vector3 target, Vector3 gravity, float elevationDeg)
    {
        Solution s = default; s.exists = false;

        Vector3 g = gravity.sqrMagnitude > 1e-6f ? gravity : Physics.gravity;
        float gMag = g.magnitude;

        Vector3 diff = target - origin;
        Vector2 diffXZ = new Vector2(diff.x, diff.z);
        float R = diffXZ.magnitude;
        float dy = diff.y;
        if (R < 1e-6f) return s;

        float elevRad = elevationDeg * Mathf.Deg2Rad;

        // Bearing in XZ plane
        Vector3 bearing = new Vector3(diff.x, 0, diff.z).normalized;
        if (bearing.sqrMagnitude < 1e-6f) return s;

        // Using v^2 = g R^2 / (2 cos^2? (R tan? - dy))
        float cos = Mathf.Cos(elevRad);
        float sin = Mathf.Sin(elevRad);
        float denom = 2f * cos * cos * (R * Mathf.Tan(elevRad) - dy);
        if (denom <= 1e-6f) return s;

        float v2 = gMag * R * R / denom;
        if (v2 <= 0f || float.IsNaN(v2) || float.IsInfinity(v2)) return s;

        float v = Mathf.Sqrt(v2);

        // Build launch velocity vector: horizontal = v*cos, vertical = v*sin
        Vector3 v0 = bearing * (v * cos) + Vector3.up * (v * sin);

        // Flight time till target along horizontal: t = R / (v cos)
        float t = R / (v * cos);

        s.exists = true;
        s.launchVelocity = v0;
        s.flightTime = t;
        s.elevationDeg = elevationDeg;
        return s;
    }

    /// <summary>
    /// Sample positions along the ballistic curve p(t) = p0 + v0 t + 0.5 g t^2
    /// </summary>
    public static int Sample(Vector3 origin, Vector3 v0, Vector3 gravity, float totalTime, int segments, Vector3[] buffer)
    {
        if (segments < 2) segments = 2;
        float dt = totalTime / (segments - 1);
        for (int i = 0; i < segments; i++)
        {
            float t = dt * i;
            buffer[i] = origin + v0 * t + 0.5f * gravity * t * t;
        }
        return segments;
    }

    /// <summary>
    /// Approximate height at net plane crossing (given netPlaneZ) to validate clearance.
    /// Assumes trajectory crosses the plane within [0, totalTime].
    /// </summary>
    public static bool TryHeightAtNetPlane(Vector3 origin, Vector3 v0, Vector3 gravity, float netPlaneZ, float totalTime, out float yOnNetPlane)
    {
        yOnNetPlane = 0f;

        // Solve for time when z(t) = netPlaneZ: origin.z + v0.z t + 0.5 gz t^2 = netPlaneZ
        // We assume gravity mostly on Y so gz ~ 0; but keep general form:
        float a = 0.5f * gravity.z;
        float b = v0.z;
        float c = origin.z - netPlaneZ;

        float t;
        if (Mathf.Abs(a) < 1e-6f)
        {
            // Linear
            if (Mathf.Abs(b) < 1e-6f) return false;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float sqrt = Mathf.Sqrt(disc);
            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);
            // Choose valid positive time within flight
            t = (t1 >= 0f && t1 <= totalTime) ? t1 :
                (t2 >= 0f && t2 <= totalTime) ? t2 : -1f;
            if (t < 0f) return false;
        }

        Vector3 p = origin + v0 * t + 0.5f * gravity * t * t;
        yOnNetPlane = p.y;
        return true;
    }
}