//using UnityEngine;

//// Attach this to the shuttlecock prefab.
//// Requires: Rigidbody, AdvancedShuttlecockPhysics on the same GameObject.
//// Racket side: add RacketController (existing) and RacketMeta (to provide RacketProfile).
//[RequireComponent(typeof(Rigidbody))]
//[RequireComponent(typeof(AdvancedShuttlecockPhysics))]
//public class ShuttlecockHitResponder : MonoBehaviour
//{
//    [Header("Profiles")]
//    public ShuttlecockProfile shuttleProfile; // assign same as in AdvancedShuttlecockPhysics (optional fallback)
//    [Header("Debug")]
//    public bool logOnHit = true;

//    private Rigidbody rb;
//    private AdvancedShuttlecockPhysics advPhys;

//    void Awake()
//    {
//        rb = GetComponent<Rigidbody>();
//        advPhys = GetComponent<AdvancedShuttlecockPhysics>();
//        if (advPhys != null && shuttleProfile == null)
//        {
//            shuttleProfile = advPhys.profile; // fallback to physics' profile
//        }
//    }

//    void OnCollisionEnter(Collision collision)
//    {
//        // Find racket controller and meta
//        var racketCtrl = collision.collider.GetComponentInParent<RacketController>();
//        if (racketCtrl == null) return;

//        var racketMeta = collision.collider.GetComponentInParent<RacketMeta>();
//        RacketProfile racketProfile = racketMeta != null ? racketMeta.profile : null;

//        ContactPoint contact = collision.GetContact(0);
//        Vector3 contactNormal = contact.normal;
//        Vector3 contactPoint = contact.point;

//        // Racket point velocity if racket has Rigidbody; else Vector3.zero
//        Vector3 racketPointVel = Vector3.zero;
//        if (collision.rigidbody != null)
//        {
//            racketPointVel = collision.rigidbody.GetPointVelocity(contactPoint);
//        }

//        // Pull user's swing data from RacketController (already implemented in your repo)
//        Vector3 swingImpulse, swingPoint, swingTorque;
//        bool haveSwing = racketCtrl.TryGetLastSwing(out swingImpulse, out swingPoint, out swingTorque);

//        // Solve collision response
//        CollisionResult res = CollisionManager.CalculateCollisionResponse(
//            rb,
//            shuttleProfile,
//            racketProfile,
//            rb.linearVelocity,
//            racketPointVel,
//            contactNormal,
//            contactPoint,
//            haveSwing,
//            swingImpulse,
//            swingTorque
//        );

//        // Apply result directly (impulse-style instantaneous change)
//        rb.linearVelocity = res.newLinearVelocity;
//        rb.angularVelocity = res.newAngularVelocity;

//        // Apply kspin to aerodynamics and trigger tumbling
//        if (advPhys != null)
//        {
//            advPhys.SetSpinDragModifier(res.spinDragModifier);
//            advPhys.TriggerTumblingState();
//        }

//        if (logOnHit)
//        {
//            Debug.Log($"[ShuttlecockHitResponder] Hit! v-> {res.newLinearVelocity.magnitude:F2} m/s | " +
//                      $"ω-> {res.newAngularVelocity.magnitude:F2} rad/s | kspin={res.spinDragModifier:F2}");
//            Debug.DrawRay(contactPoint, contactNormal * 0.2f, Color.green, 0.25f);
//        }
//    }
//}