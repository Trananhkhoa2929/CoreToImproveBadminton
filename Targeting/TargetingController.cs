using UnityEngine;

public class TargetingController : MonoBehaviour
{
    [Header("References")]
    public Transform shotOrigin;
    public CourtSettings court;
    public TargetReticle reticle;
    public GhostTrajectoryRenderer ghost;

    [Header("Input")]
    public KeyCode lockKey = KeyCode.Mouse0;
    public KeyCode unlockKey = KeyCode.Escape;

    [Header("Planning Preferences")]
    public bool preferLowArc = true;
    public float minElevationDeg = 15f;
    public float maxElevationDeg = 75f;

    [Header("Debug")]
    public bool logLock = true;

    private Camera cam;
    private bool hasLock = false;
    private Vector3 lockedPoint;
    private ShotValidator.Result currentPlan;
    private RaycastHit lastHit;

    public bool HasLockedTarget => hasLock;
    public ShotValidator.Result CurrentPlan => currentPlan;
    public Vector3 Origin => (shotOrigin != null ? shotOrigin.position : transform.position);

    void Awake()
    {
        cam = Camera.main;

        // Safe init visuals
        if (ghost != null) ghost.Hide(); // now safe (Ghost ensures LR internally)
        if (reticle != null) reticle.SetState(TargetReticle.State.Hidden);
    }

    void Update()
    {
        if (cam == null || court == null) return;

        Vector3 origin = Origin;

        if (!hasLock)
        {
            if (TryRayToCourt(out lastHit))
            {
                Vector3 desired = lastHit.point;
                var plan = ShotValidator.ValidateAndPlan(origin, desired, court, minElevationDeg, maxElevationDeg, preferLowArc);
                currentPlan = plan;

                if (reticle != null)
                {
                    reticle.UpdatePose(plan.adjustedTarget, lastHit.normal);
                    reticle.SetState(StateFrom(plan));
                }

                if (ghost != null)
                {
                    if (plan.status != ShotValidator.Feasibility.NoSolution && plan.chosen.exists)
                    {
                        bool clamped = plan.status == ShotValidator.Feasibility.ClampedToBounds || plan.status == ShotValidator.Feasibility.AdjustedForNetClearance;
                        ghost.Show(plan, origin, clamped);
                    }
                    else
                    {
                        ghost.ShowInvalid(origin, (plan.adjustedTarget - origin), 0.4f);
                    }
                }

                if (Input.GetKeyDown(lockKey) && plan.chosen.exists && plan.status != ShotValidator.Feasibility.NoSolution)
                {
                    hasLock = true;
                    lockedPoint = plan.adjustedTarget;
                    if (logLock) Debug.Log($"[Targeting] Locked target at {lockedPoint}, status={plan.status}, elev={plan.chosen.elevationDeg:F1}°");
                }
            }
            else
            {
                if (reticle != null) reticle.SetState(TargetReticle.State.Hidden);
                if (ghost != null) ghost.Hide();
            }
        }
        else
        {
            var plan = ShotValidator.ValidateAndPlan(origin, lockedPoint, court, minElevationDeg, maxElevationDeg, preferLowArc);
            currentPlan = plan;

            if (reticle != null)
            {
                Vector3 n = lastHit.collider ? lastHit.normal : Vector3.up;
                reticle.UpdatePose(plan.adjustedTarget, n);
                reticle.SetState(StateFrom(plan));
            }
            if (ghost != null)
            {
                if (plan.status != ShotValidator.Feasibility.NoSolution && plan.chosen.exists)
                {
                    bool clamped = plan.status == ShotValidator.Feasibility.ClampedToBounds || plan.status == ShotValidator.Feasibility.AdjustedForNetClearance;
                    ghost.Show(plan, origin, clamped);
                }
                else
                {
                    ghost.ShowInvalid(origin, (plan.adjustedTarget - origin), 0.4f);
                }
            }

            if (Input.GetKeyDown(unlockKey))
            {
                hasLock = false;
                if (reticle != null) reticle.SetState(TargetReticle.State.Hidden);
                if (ghost != null) ghost.Hide();
                if (logLock) Debug.Log("[Targeting] Unlock target.");
            }
        }
    }

    bool TryRayToCourt(out RaycastHit hit)
    {
        hit = default;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, 1000f, court != null ? court.targetableCourtMask : ~0);
    }

    TargetReticle.State StateFrom(ShotValidator.Result plan)
    {
        if (plan.status == ShotValidator.Feasibility.NoSolution || !plan.chosen.exists)
            return TargetReticle.State.Invalid;
        if (plan.status == ShotValidator.Feasibility.ClampedToBounds || plan.status == ShotValidator.Feasibility.AdjustedForNetClearance)
            return TargetReticle.State.Clamped;
        return TargetReticle.State.Valid;
    }
}