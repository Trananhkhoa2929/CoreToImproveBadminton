////using UnityEngine;

/////// <summary>
/////// ShuttlecockPhysics: custom physics for shuttlecock (Reynolds-aware mixed drag, alignment torque, spin decay, optional Magnus, optional buoyancy).
/////// Attach to shuttlecock GameObject with Rigidbody (use interpolation, continuous collision if needed).
/////// Notes on calibration (from flight_path_analysis):
/////// - For high-speed shots (~44 m/s), tuned Cd around ~0.5 produced realistic ranges (Table D.2).
/////// - Reynolds effects: effective drag exponent varies between ~1 and 2; use mixed drag (linear at low v, quadratic at high v) to extend hang time on descent.
/////// - Buoyancy is small but can add ~1–3% of weight upward to better match time-of-flight.
/////// </summary>
////[RequireComponent(typeof(Rigidbody))]
////public class ShuttlecockPhysics : MonoBehaviour
////{
////    [Header("Physical")]
////    [Tooltip("Mass in kg (typical shuttlecock ~4.74 g).")]
////    public float mass = 0.005f; // kg
////    [Tooltip("Effective projected area in m^2 (tune with Cd to match range/time).")]
////    public float area = 0.003f; // m^2
////    [Tooltip("Air density in kg/m^3.")]
////    public float airDensity = 1.225f; // kg/m3

////    [Header("Drag (baseline)")]
////    [Tooltip("Baseline drag coefficient multiplier (will be modulated by CdVsSpeed).")]
////    public float Cd = 0.6f; // tuned baseline; high-speed effective Cd ~0.5 common in report
////    [Tooltip("Use a speed-dependent Cd curve.")]
////    public bool useVelocityDependentCd = true;
////    [Tooltip("Multiplier over Cd vs speed (m/s). At 44 m/s target ~0.8–1.0 to yield effective Cd~0.5–0.6 when Cd=0.6-0.65.")]
////    public AnimationCurve CdVsSpeed = new AnimationCurve(
////        new Keyframe(0f, 1.05f),
////        new Keyframe(10f, 0.9f),
////        new Keyframe(30f, 0.7f),
////        new Keyframe(50f, 0.6f)
////    );

////    [Header("Reynolds-aware Mixed Drag")]
////    [Tooltip("Blend linear (n≈1) drag at low speed to quadratic (n=2) drag at high speed.")]
////    public bool useMixedDrag = true;
////    [Tooltip("Below this speed (m/s) drag behaves ~linear; above vTransitionHigh behaves ~quadratic. Smooth blend in between.")]
////    public float vTransitionLow = 5f;
////    [Tooltip("Above this speed (m/s) drag behaves ~quadratic.")]
////    public float vTransitionHigh = 12f;

////    [Header("Alignment & Rotation")]
////    [Tooltip("How strongly body forward (+Z) aligns to velocity direction.")]
////    public float alignStiffness = 0.1f; // k_align
////    [Tooltip("Rotational damping coefficient.")]
////    public float rotationalDamping = 0.02f; // c_rot
////    [Tooltip("Extra spin decay coefficient.")]
////    public float spinDecay = 0.5f; // extra

////    [Header("Magnus (optional)")]
////    public bool useMagnus = false;
////    public float magnusCoeff = 0.0005f; // small

////    [Header("Gravity / Buoyancy")]
////    [Tooltip("If true, use Unity's gravity (Physics.gravity). If false, use custom gravity vector below.")]
////    public bool useUnityGravity = true;
////    public Vector3 gravity = new Vector3(0, -9.81f, 0);
////    [Tooltip("Apply a small upward buoyancy force as a fraction of weight to extend time-of-flight slightly.")]
////    public bool useBuoyancy = true;
////    [Range(0f, 0.1f)]
////    [Tooltip("Buoyancy as a fraction of weight (0.0–0.05 typical). 0.02 ≈ 2% of weight.")]
////    public float buoyancyFraction = 0.02f;

////    [Header("Stability")]
////    [Tooltip("Clamp maximum angular velocity (rad/s) to avoid instability.")]
////    public float maxAngVel = 200f;

////    private Rigidbody rb;

////    void Awake()
////    {
////        rb = GetComponent<Rigidbody>();
////        rb.mass = mass;
////        rb.useGravity = useUnityGravity;
////        // Optional known inertia tensor:
////        // rb.inertiaTensor = new Vector3(ix, iy, iz);
////        // rb.inertiaTensorRotation = Quaternion.identity;
////    }

////    void OnValidate()
////    {
////        if (rb == null) rb = GetComponent<Rigidbody>();
////        if (rb != null)
////        {
////            rb.mass = mass;
////            rb.useGravity = useUnityGravity;
////        }
////        // Ensure transition speeds are sensible
////        if (vTransitionHigh < vTransitionLow) vTransitionHigh = vTransitionLow + 0.01f;
////    }

////    void FixedUpdate()
////    {
////        Vector3 velocity = rb.linearVelocity;
////        float v = velocity.magnitude;

////        // Gravity (Unity or custom)
////        if (!useUnityGravity)
////        {
////            rb.AddForce(mass * gravity, ForceMode.Force);
////        }

////        // Buoyancy: small upward fraction of weight (helps extend time-of-flight slightly)
////        if (useBuoyancy && (useUnityGravity || gravity.sqrMagnitude > 0f))
////        {
////            Vector3 gVec = useUnityGravity ? Physics.gravity : gravity;
////            Vector3 buoyancyForce = -buoyancyFraction * mass * gVec; // opposite to gravity
////            rb.AddForce(buoyancyForce, ForceMode.Force);
////        }

////        // Drag force
////        if (v > 0f)
////        {
////            Vector3 vHat = velocity / v;

////            // Speed-dependent Cd
////            float cdNow = Cd;
////            if (useVelocityDependentCd && CdVsSpeed != null)
////            {
////                cdNow *= Mathf.Max(0f, CdVsSpeed.Evaluate(v));
////            }

////            // Quadratic coefficient K2 = 0.5 * rho * Cd * A
////            float K2 = 0.5f * airDensity * cdNow * area;

////            Vector3 drag = Vector3.zero;

////            if (useMixedDrag)
////            {
////                // Blend linear (low v) and quadratic (high v) drag smoothly
////                // Ensure continuity at transition by choosing K1 such that K1*vT = K2*vT^2  => K1 = K2 * vT
////                float vT = Mathf.Clamp((vTransitionLow + vTransitionHigh) * 0.5f, 0.01f, 1000f);
////                float K1 = K2 * vT;

////                // Smooth blend factor in [0,1]
////                float t = SmoothStep(vTransitionLow, vTransitionHigh, v);

////                // |F| = (1 - t) * (K1 * v) + t * (K2 * v^2)
////                float dragMag = (1f - t) * (K1 * v) + t * (K2 * v * v);
////                drag = -dragMag * vHat;
////            }
////            else
////            {
////                // Pure quadratic drag
////                float dragMag = K2 * v * v;
////                drag = -dragMag * vHat;
////            }

////            rb.AddForce(drag, ForceMode.Force);
////        }

////        // Magnus lift (optional, small)
////        if (useMagnus && v > 0f)
////        {
////            Vector3 magnus = magnusCoeff * Vector3.Cross(rb.angularVelocity, rb.linearVelocity);
////            rb.AddForce(magnus, ForceMode.Force);
////        }

////        // Alignment torque: align local +Z to velocity direction
////        Vector3 bodyForward = transform.forward; // assume forward points from cork to skirt
////        if (v > 0.05f)
////        {
////            Vector3 vHat = velocity / v;
////            Vector3 axis = Vector3.Cross(bodyForward, vHat);
////            float axisMag = axis.magnitude;
////            float angle = Vector3.Angle(bodyForward, vHat) * Mathf.Deg2Rad;

////            Vector3 alignTorque = Vector3.zero;
////            if (axisMag > 1e-7f && angle > 1e-6f)
////            {
////                alignTorque = (alignStiffness * angle) * (axis / axisMag);
////            }

////            Vector3 dampingTorque = -rotationalDamping * rb.angularVelocity;
////            rb.AddTorque(alignTorque + dampingTorque, ForceMode.Force);
////        }
////        else
////        {
////            // small damping when nearly stationary
////            rb.AddTorque(-rotationalDamping * rb.angularVelocity, ForceMode.Force);
////        }

////        // Extra spin decay
////        rb.AddTorque(-spinDecay * rb.angularVelocity, ForceMode.Force);

////        // Clamp angular velocity
////        float w = rb.angularVelocity.magnitude;
////        if (w > maxAngVel)
////        {
////            rb.angularVelocity = (rb.angularVelocity / w) * maxAngVel;
////        }
////    }

////    // SmoothStep blend on speed band
////    private static float SmoothStep(float edge0, float edge1, float x)
////    {
////        float t = Mathf.InverseLerp(edge0, edge1, x);
////        t = Mathf.Clamp01(t);
////        return t * t * (3f - 2f * t);
////    }
////}




////////////////////////////////////////////////////////////////////////////////////
//using UnityEngine;

///// <summary>
///// ShuttlecockPhysics: custom physics for shuttlecock (drag ~ v^2, alignment torque, spin decay, optional Magnus).
///// Attach to shuttlecock GameObject with Rigidbody (use interpolation, continuous collision if needed).
///// </summary>
//[RequireComponent(typeof(Rigidbody))]
//public class ShuttlecockPhysics : MonoBehaviour
//{
//    [Header("Physical")]
//    public float mass = 0.005f; // kg, typical shuttlecock ~4.74g -> ~0.005 kg (tune to real)
//    public float area = 0.003f; // m^2 effective projected area (tune)
//    public float airDensity = 1.225f; // kg/m3

//    [Header("Drag")]
//    public float Cd = 0.8f; // baseline drag coefficient (tune from data, typically large)
//    public bool useVelocityDependentCd = true;
//    public AnimationCurve CdVsSpeed = AnimationCurve.Linear(0, 1f, 50, 0.6f); // example

//    [Header("Alignment & Rotation")]
//    public float alignStiffness = 0.1f; // k_align: how strongly body z aligns to velocity dir
//    public float rotationalDamping = 0.02f; // c_rot: damps angular velocity
//    public float spinDecay = 0.5f; // extra spin decay coefficient

//    [Header("Magnus (optional)")]
//    public bool useMagnus = false;
//    public float magnusCoeff = 0.0005f; // small value to tune

//    [Header("Misc")]
//    public bool useUnityGravity = true;
//    public Vector3 gravity = new Vector3(0, -9.81f, 0);

//    Rigidbody rb;

//    void Awake()
//    {
//        rb = GetComponent<Rigidbody>();
//        rb.mass = mass;
//        // Optionally set inertia tensor here if you know it:
//        // rb.inertiaTensor = new Vector3(ix, iy, iz);
//        // rb.inertiaTensorRotation = Quaternion.identity;
//    }

//    void FixedUpdate()
//    {
//        Vector3 velocity = rb.linearVelocity;
//        float v = velocity.magnitude;
//        if (v < 1e-6f) v = 0f;

//        // Gravity (use Unity gravity by default, or custom)
//        if (!useUnityGravity)
//        {
//            rb.AddForce(mass * gravity, ForceMode.Force);
//        }

//        // Drag: Fd = -0.5 * rho * Cd * A * v^2 * vHat
//        float cdNow = Cd;
//        if (useVelocityDependentCd && CdVsSpeed != null)
//            cdNow = CdVsSpeed.Evaluate(v);

//        Vector3 drag = Vector3.zero;
//        if (v > 0f)
//        {
//            Vector3 vHat = velocity / v;
//            float dragMag = 0.5f * airDensity * cdNow * area * v * v; // v^2
//            drag = -dragMag * vHat;
//            rb.AddForce(drag, ForceMode.Force);
//        }

//        // Magnus lift (optional, small)
//        if (useMagnus && v > 0f)
//        {
//            Vector3 magnus = magnusCoeff * Vector3.Cross(rb.angularVelocity, velocity);
//            rb.AddForce(magnus, ForceMode.Force);
//        }

//        // Alignment torque: try to align the shuttlecock's body-forward (local +Z) to velocity direction
//        Vector3 bodyForward = transform.forward; // assume forward points from cork to skirt
//        if (v > 0.05f)
//        {
//            Vector3 vHat = velocity.normalized;
//            // compute axis/angle to rotate bodyForward -> vHat
//            Vector3 axis = Vector3.Cross(bodyForward, vHat);
//            float angle = Vector3.Angle(bodyForward, vHat) * Mathf.Deg2Rad;
//            // A simple proportional torque:
//            Vector3 alignTorque = alignStiffness * angle * axis.normalized;
//            // Damping to remove spin
//            Vector3 dampingTorque = -rotationalDamping * rb.angularVelocity;
//            rb.AddTorque(alignTorque + dampingTorque, ForceMode.Force);
//        }
//        else
//        {
//            // small damping when nearly stationary
//            rb.AddTorque(-rotationalDamping * rb.angularVelocity, ForceMode.Force);
//        }

//        // Spin decay extra
//        rb.AddTorque(-spinDecay * rb.angularVelocity, ForceMode.Force);

//        // Optionally clamp max angular velocity to avoid instability
//        float maxAngVel = 200f;
//        if (rb.angularVelocity.magnitude > maxAngVel)
//            rb.angularVelocity = rb.angularVelocity.normalized * maxAngVel;
//    }
//}

/////////////////////////////////////////////////////////////////////////////////////

using UnityEngine;

/// <summary>
/// ShuttlecockPhysics: custom physics for shuttlecock (Reynolds-aware mixed drag, alignment torque, spin decay, optional Magnus, optional buoyancy).
/// Stops completely on landing when colliding with a ground-tagged collider (CourtFloor by default).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShuttlecockPhysics : MonoBehaviour
{
    [Header("Physical")]
    [Tooltip("Mass in kg (typical shuttlecock ~4.74 g).")]
    public float mass = 0.005f; // kg
    [Tooltip("Effective projected area in m^2 (tune with Cd to match range/time).")]
    public float area = 0.003f; // m^2
    [Tooltip("Air density in kg/m^3.")]
    public float airDensity = 1.225f; // kg/m3

    [Header("Drag (baseline)")]
    [Tooltip("Baseline drag coefficient multiplier (will be modulated by CdVsSpeed).")]
    public float Cd = 0.6f; // tuned baseline; high-speed effective Cd ~0.5 common in report
    [Tooltip("Use a speed-dependent Cd curve.")]
    public bool useVelocityDependentCd = true;
    [Tooltip("Multiplier over Cd vs speed (m/s). At 44 m/s target ~0.8–1.0 to yield effective Cd~0.5–0.6 when Cd=0.6-0.65.")]
    public AnimationCurve CdVsSpeed = new AnimationCurve(
        new Keyframe(0f, 1.05f),
        new Keyframe(10f, 0.9f),
        new Keyframe(30f, 0.7f),
        new Keyframe(50f, 0.6f)
    );

    [Header("Reynolds-aware Mixed Drag")]
    [Tooltip("Blend linear (n≈1) drag at low speed to quadratic (n=2) drag at high speed.")]
    public bool useMixedDrag = true;
    [Tooltip("Below this speed (m/s) drag behaves ~linear; above vTransitionHigh behaves ~quadratic. Smooth blend in between.")]
    public float vTransitionLow = 5f;
    [Tooltip("Above this speed (m/s) drag behaves ~quadratic.")]
    public float vTransitionHigh = 12f;

    [Header("Alignment & Rotation")]
    [Tooltip("How strongly body forward (+Z) aligns to velocity direction.")]
    public float alignStiffness = 0.1f; // k_align
    [Tooltip("Rotational damping coefficient.")]
    public float rotationalDamping = 0.02f; // c_rot
    [Tooltip("Extra spin decay coefficient.")]
    public float spinDecay = 0.5f; // extra

    [Header("Magnus (optional)")]
    public bool useMagnus = false;
    public float magnusCoeff = 0.0005f; // small

    [Header("Gravity / Buoyancy")]
    [Tooltip("If true, use Unity's gravity (Physics.gravity). If false, use custom gravity vector below.")]
    public bool useUnityGravity = true;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    [Tooltip("Apply a small upward buoyancy force as a fraction of weight to extend time-of-flight slightly.")]
    public bool useBuoyancy = true;
    [Range(0f, 0.1f)]
    [Tooltip("Buoyancy as a fraction of weight (0.0–0.05 typical). 0.02 ≈ 2% of weight.")]
    public float buoyancyFraction = 0.02f;

    [Header("Landing")]
    [Tooltip("Stop physics when hitting ground colliders with this tag.")]
    public string groundTag = "CourtFloor";
    [Tooltip("If true, set Rigidbody.isKinematic on landing to stop motion instantly.")]
    public bool makeKinematicOnLand = true;
    [Tooltip("Destroy the GameObject after this many seconds on landing. Set <= 0 to keep it.")]
    public float destroyAfterSeconds = 5f;

    [Header("Stability")]
    [Tooltip("Clamp maximum angular velocity (rad/s) to avoid instability.")]
    public float maxAngVel = 200f;

    private Rigidbody rb;
    private bool landed = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = useUnityGravity;

        // Recommended settings for fast/light objects
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Also use built-in angular velocity clamp
        rb.maxAngularVelocity = Mathf.Max(0.1f, maxAngVel);
    }

    void OnValidate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = mass;
            rb.useGravity = useUnityGravity;
            rb.maxAngularVelocity = Mathf.Max(0.1f, maxAngVel);
        }
        if (vTransitionHigh < vTransitionLow) vTransitionHigh = vTransitionLow + 0.01f;
        if (string.IsNullOrWhiteSpace(groundTag)) groundTag = "CourtFloor";
    }

    void FixedUpdate()
    {
        if (landed) return; // Stop all custom forces after landing

        Vector3 velocity = rb.linearVelocity;
        float v = velocity.magnitude;

        // Gravity (Unity or custom)
        if (!useUnityGravity)
        {
            rb.AddForce(mass * gravity, ForceMode.Force);
        }

        // Buoyancy
        if (useBuoyancy && (useUnityGravity || gravity.sqrMagnitude > 0f))
        {
            Vector3 gVec = useUnityGravity ? Physics.gravity : gravity;
            Vector3 buoyancyForce = -buoyancyFraction * mass * gVec; // opposite to gravity
            rb.AddForce(buoyancyForce, ForceMode.Force);
        }

        // Drag force
        if (v > 0f)
        {
            Vector3 vHat = velocity / v;

            // Speed-dependent Cd
            float cdNow = Cd;
            if (useVelocityDependentCd && CdVsSpeed != null)
            {
                cdNow *= Mathf.Max(0f, CdVsSpeed.Evaluate(v));
            }

            // Quadratic coefficient K2 = 0.5 * rho * Cd * A
            float K2 = 0.5f * airDensity * cdNow * area;

            Vector3 drag;

            if (useMixedDrag)
            {
                // Blend linear (low v) and quadratic (high v) drag smoothly
                float vT = Mathf.Clamp((vTransitionLow + vTransitionHigh) * 0.5f, 0.01f, 1000f);
                float K1 = K2 * vT; // ensure continuity at vT

                float t = SmoothStep(vTransitionLow, vTransitionHigh, v);

                float dragMag = (1f - t) * (K1 * v) + t * (K2 * v * v);
                drag = -dragMag * vHat;
            }
            else
            {
                float dragMag = K2 * v * v;
                drag = -dragMag * vHat;
            }

            rb.AddForce(drag, ForceMode.Force);
        }

        // Magnus lift (optional, small)
        if (useMagnus && v > 0f)
        {
            Vector3 magnus = magnusCoeff * Vector3.Cross(rb.angularVelocity, rb.linearVelocity);
            rb.AddForce(magnus, ForceMode.Force);
        }

        // Alignment torque: align local +Z to velocity direction
        Vector3 bodyForward = transform.forward; // assume forward points from cork to skirt
        if (v > 0.05f)
        {
            Vector3 vHat = velocity / v;
            Vector3 axis = Vector3.Cross(bodyForward, vHat);
            float axisMag = axis.magnitude;
            float angle = Vector3.Angle(bodyForward, vHat) * Mathf.Deg2Rad;

            Vector3 alignTorque = Vector3.zero;
            if (axisMag > 1e-7f && angle > 1e-6f)
            {
                alignTorque = (alignStiffness * angle) * (axis / axisMag);
            }

            Vector3 dampingTorque = -rotationalDamping * rb.angularVelocity;
            rb.AddTorque(alignTorque + dampingTorque, ForceMode.Force);
        }
        else
        {
            // small damping when nearly stationary
            rb.AddTorque(-rotationalDamping * rb.angularVelocity, ForceMode.Force);
        }

        // Extra spin decay
        rb.AddTorque(-spinDecay * rb.angularVelocity, ForceMode.Force);

        // Clamp angular velocity
        float w = rb.angularVelocity.magnitude;
        if (w > maxAngVel)
        {
            rb.angularVelocity = (rb.angularVelocity / w) * maxAngVel;
        }
    }

    // Landing handlers
    void OnCollisionEnter(Collision collision)
    {
        if (landed) return;
        if (collision != null && collision.collider != null && collision.collider.CompareTag(groundTag))
        {
            Land();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (landed) return;
        if (other != null && other.CompareTag(groundTag))
        {
            Land();
        }
    }

    private void Land()
    {
        if (landed) return;
        landed = true;

        // Stop motion completely
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (makeKinematicOnLand)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            // Optional: reduce collision work after landing
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        else
        {
            // Fallback: freeze all if you keep it dynamic
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        // Optionally destroy after a delay
        if (destroyAfterSeconds > 0f)
        {
            Destroy(gameObject, destroyAfterSeconds);
        }
    }

    // SmoothStep blend on speed band
    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.InverseLerp(edge0, edge1, x);
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}