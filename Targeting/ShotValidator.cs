using UnityEngine;

/// <summary>
/// Basic feasibility checks and clamping for target selection:
/// - Clamp inside opponent court bounds
/// - Ensure minimal net clearance by pushing target away from net plane if needed
/// - Enforce elevation constraints and max speed constraints roughly (with gravity-only plan)
/// Returns an adjusted target and feasibility info for UI.
/// </summary>
public static class ShotValidator
{
    public enum Feasibility
    {
        Valid = 0,
        ClampedToBounds = 1,
        AdjustedForNetClearance = 2,
        NoSolution = 3
    }

    public struct Result
    {
        public Vector3 requestedTarget;
        public Vector3 adjustedTarget;
        public Feasibility status;
        public TrajectorySolver.Solution lowArc;
        public TrajectorySolver.Solution highArc;
        public TrajectorySolver.Solution chosen;  // Picked for ghost
        public bool crossesNet;
        public float netHeightAtCross;
    }

    public static Result ValidateAndPlan(
        Vector3 origin,
        Vector3 requestedTarget,
        CourtSettings court,
        float minElevationDeg = 15f,
        float maxElevationDeg = 75f,
        bool preferLowArc = true)
    {
        Result r = default;
        r.requestedTarget = requestedTarget;
        r.adjustedTarget = requestedTarget;
        r.status = Feasibility.Valid;

        // 1) Clamp target into opponent bounds if requested
        if (court != null && court.clampIntoBounds)
        {
            Vector3 clamp;
            if (court.TryClampIntoOpponentBounds(requestedTarget, out clamp))
            {
                if ((clamp - requestedTarget).sqrMagnitude > 1e-6f)
                {
                    r.status = Feasibility.ClampedToBounds;
                    r.adjustedTarget = clamp;
                }
            }
        }

        // 2) Solve low/high arcs
        TrajectorySolver.SolveBallisticArcs(origin, r.adjustedTarget, Physics.gravity, out r.lowArc, out r.highArc);

        // Filter by elevation constraints
        bool lowOk = r.lowArc.exists && r.lowArc.elevationDeg >= minElevationDeg && r.lowArc.elevationDeg <= maxElevationDeg;
        bool highOk = r.highArc.exists && r.highArc.elevationDeg >= minElevationDeg && r.highArc.elevationDeg <= maxElevationDeg;

        // Choose solution
        TrajectorySolver.Solution chosen = default;
        if (preferLowArc && lowOk) chosen = r.lowArc;
        else if (!preferLowArc && highOk) chosen = r.highArc;
        else if (lowOk) chosen = r.lowArc;
        else if (highOk) chosen = r.highArc;

        if (!chosen.exists)
        {
            r.status = Feasibility.NoSolution;
            r.chosen = default;
            return r;
        }

        // 3) Net clearance check: ensure height over net plane >= netTopY + margin
        r.crossesNet = false;
        r.netHeightAtCross = 0f;

        if (court != null)
        {
            float yAtNet;
            if (TrajectorySolver.TryHeightAtNetPlane(origin, chosen.launchVelocity, Physics.gravity, court.netPlaneZ, chosen.flightTime, out yAtNet))
            {
                r.crossesNet = true;
                r.netHeightAtCross = yAtNet;
                float required = court.netTopY + court.netClearanceMargin;
                if (yAtNet < required)
                {
                    // push target away from net plane along bearing to increase clearance
                    // simple heuristic: move target 0.2m away per 0.05m lack, up to 1.0m
                    float lack = required - yAtNet;
                    float push = Mathf.Clamp(lack * 4.0f, 0.15f, 1.0f);

                    Vector3 bearing = new Vector3(r.adjustedTarget.x - origin.x, 0f, r.adjustedTarget.z - origin.z).normalized;
                    if (bearing.sqrMagnitude > 1e-6f)
                    {
                        r.adjustedTarget += bearing * push;

                        // Re-clamp into bounds
                        if (court.clampIntoBounds)
                        {
                            Vector3 clamp;
                            if (court.TryClampIntoOpponentBounds(r.adjustedTarget, out clamp))
                                r.adjustedTarget = clamp;
                        }

                        // Re-solve with adjusted target
                        TrajectorySolver.SolveBallisticArcs(origin, r.adjustedTarget, Physics.gravity, out r.lowArc, out r.highArc);
                        lowOk = r.lowArc.exists && r.lowArc.elevationDeg >= minElevationDeg && r.lowArc.elevationDeg <= maxElevationDeg;
                        highOk = r.highArc.exists && r.highArc.elevationDeg >= minElevationDeg && r.highArc.elevationDeg <= maxElevationDeg;
                        chosen = preferLowArc && lowOk ? r.lowArc : (highOk ? r.highArc : (lowOk ? r.lowArc : default));

                        if (!chosen.exists)
                        {
                            r.status = Feasibility.NoSolution;
                            r.chosen = default;
                            return r;
                        }

                        // Recheck net height info for UI
                        if (TrajectorySolver.TryHeightAtNetPlane(origin, chosen.launchVelocity, Physics.gravity, court.netPlaneZ, chosen.flightTime, out yAtNet))
                        {
                            r.netHeightAtCross = yAtNet;
                        }

                        // mark status
                        r.status = Feasibility.AdjustedForNetClearance;
                    }
                }
            }
        }

        r.chosen = chosen;
        return r;
    }
}