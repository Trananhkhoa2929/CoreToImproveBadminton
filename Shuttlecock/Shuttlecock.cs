//using UnityEngine;
//using System.Collections.Generic;

//namespace BadmintonPhysics
//{
//    /// <summary>
//    /// Mô phỏng động lực học cầu lông dựa trên nghiên cứu của Laura Zeying Du
//    /// Hỗ trợ cả linear drag (n=1) và quadratic drag (n=2)
//    /// </summary>
//    [RequireComponent(typeof(Rigidbody))]
//    public class ShuttlecockPhysics : MonoBehaviour
//    {
//        [Header("Launch Parameters")]
//        [SerializeField, Range(5f, 50f)] private float launchSpeed = 44.3f; // m/s
//        [SerializeField, Range(0f, 90f)] private float launchAngle = 45f; // degrees
//        [SerializeField] private Transform launchPoint;

//        [Header("Physical Properties")]
//        [SerializeField] private float mass = 0.005f; // 5 grams typical shuttlecock weight
//        [SerializeField] private float dragCoefficient = 0.5f; // Tunable C_D
//        [SerializeField] private float crossSectionalArea = 0.00196f; // m² (D = 2.5cm)
//        [SerializeField] private float airDensity = 1.225f; // kg/m³ at sea level

//        [Header("Drag Model")]
//        [SerializeField] private DragModel dragModel = DragModel.Quadratic;
//        [SerializeField] private float linearDragCoefficient = 0.1f; // b coefficient for linear drag

//        [Header("Simulation Settings")]
//        [SerializeField] private bool useUnityPhysics = false; // Toggle between Unity physics and custom integration
//        [SerializeField] private float integrationTimeStep = 0.001f; // For Euler method
//        [SerializeField] private bool simulateOnStart = true;
//        [SerializeField] private bool drawTrajectory = true;
//        [SerializeField] private int trajectoryPoints = 100;

//        [Header("Court Dimensions")]
//        [SerializeField] private float courtLength = 13.42f; // meters
//        [SerializeField] private float netHeight = 1.55f; // meters at posts
//        [SerializeField] private float netDistance = 6.71f; // meters from baseline

//        [Header("Debug Visualization")]
//        [SerializeField] private bool showForces = false;
//        [SerializeField] private Color trajectoryColor = Color.yellow;
//        [SerializeField] private float trajectoryLineWidth = 0.05f;

//        public enum DragModel
//        {
//            Linear,    // F_d = bv (n=1)
//            Quadratic  // F_d = 0.5 * ρ * S * C_D * v² (n=2)
//        }

//        // Runtime variables
//        private Rigidbody rb;
//        private Vector3 velocity;
//        private Vector3 initialPosition;
//        private List<Vector3> trajectoryPositions;
//        private LineRenderer trajectoryLine;
//        private float flightTime;
//        private bool isSimulating = false;

//        // Calculated drag coefficient
//        private float dragConstant; // 'b' value

//        // Properties for external access
//        public float Range { get; private set; }
//        public float MaxHeight { get; private set; }
//        public float TimeOfFlight { get; private set; }
//        public Vector3 LandingPosition { get; private set; }

//        void Awake()
//        {
//            rb = GetComponent<Rigidbody>();
//            trajectoryPositions = new List<Vector3>();

//            // Setup LineRenderer for trajectory visualization
//            if (drawTrajectory)
//            {
//                GameObject lineObj = new GameObject("TrajectoryLine");
//                lineObj.transform.SetParent(transform);
//                trajectoryLine = lineObj.AddComponent<LineRenderer>();
//                trajectoryLine.startWidth = trajectoryLineWidth;
//                trajectoryLine.endWidth = trajectoryLineWidth;
//                trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
//                trajectoryLine.startColor = trajectoryColor;
//                trajectoryLine.endColor = trajectoryColor;
//            }

//            // Calculate drag constant based on model
//            CalculateDragConstant();
//        }

//        void Start()
//        {
//            if (simulateOnStart)
//            {
//                Launch();
//            }
//        }

//        void FixedUpdate()
//        {
//            if (isSimulating && !useUnityPhysics)
//            {
//                // Custom physics integration using Euler method
//                UpdatePhysicsCustom(Time.fixedDeltaTime);
//            }
//        }

//        /// <summary>
//        /// Launch shuttlecock with specified parameters
//        /// </summary>
//        public void Launch()
//        {
//            Launch(launchSpeed, launchAngle);
//        }

//        /// <summary>
//        /// Launch shuttlecock with custom parameters
//        /// </summary>
//        public void Launch(float speed, float angle)
//        {
//            launchSpeed = speed;
//            launchAngle = angle;

//            // Reset position
//            initialPosition = launchPoint ? launchPoint.position : transform.position;
//            transform.position = initialPosition;

//            // Calculate initial velocity components
//            float angleRad = angle * Mathf.Deg2Rad;
//            velocity = new Vector3(
//                speed * Mathf.Cos(angleRad),
//                speed * Mathf.Sin(angleRad),
//                0f
//            );

//            // Reset tracking variables
//            flightTime = 0f;
//            MaxHeight = initialPosition.y;
//            trajectoryPositions.Clear();

//            if (useUnityPhysics)
//            {
//                // Use Unity's physics system
//                rb.useGravity = true;
//                rb.linearDamping = 0f; // We'll apply custom drag
//                rb.linearVelocity = velocity;
//                rb.mass = mass;
//            }
//            else
//            {
//                // Use custom physics
//                rb.useGravity = false;
//                rb.isKinematic = true;
//            }

//            isSimulating = true;

//            // Pre-calculate trajectory for visualization
//            if (drawTrajectory)
//            {
//                CalculateTrajectory();
//                DrawTrajectory();
//            }
//        }

//        /// <summary>
//        /// Custom physics update using Euler integration
//        /// </summary>
//        private void UpdatePhysicsCustom(float deltaTime)
//        {
//            Vector3 dragForce = CalculateDragForce(velocity);
//            Vector3 gravityForce = new Vector3(0, -Physics.gravity.magnitude * mass, 0);

//            // F = ma => a = F/m
//            Vector3 acceleration = (dragForce + gravityForce) / mass;

//            // Euler integration
//            velocity += acceleration * deltaTime;
//            transform.position += velocity * deltaTime;

//            // Track statistics
//            flightTime += deltaTime;
//            if (transform.position.y > MaxHeight)
//            {
//                MaxHeight = transform.position.y;
//            }

//            // Check if landed
//            if (transform.position.y <= 0f && flightTime > 0.1f)
//            {
//                OnLanded();
//            }

//            // Add to trajectory
//            if (trajectoryPositions.Count < 1000) // Limit points
//            {
//                trajectoryPositions.Add(transform.position);
//            }
//        }

//        /// <summary>
//        /// Calculate drag force based on selected model
//        /// </summary>
//        private Vector3 CalculateDragForce(Vector3 vel)
//        {
//            float speed = vel.magnitude;

//            if (speed < 0.001f) return Vector3.zero;

//            Vector3 dragDirection = -vel.normalized;
//            float dragMagnitude = 0f;

//            switch (dragModel)
//            {
//                case DragModel.Linear:
//                    // F_d = b * v
//                    dragMagnitude = linearDragCoefficient * speed;
//                    break;

//                case DragModel.Quadratic:
//                    // F_d = 0.5 * ρ * S * C_D * v²
//                    dragMagnitude = 0.5f * airDensity * crossSectionalArea * dragCoefficient * speed * speed;
//                    break;
//            }

//            return dragDirection * dragMagnitude;
//        }

//        /// <summary>
//        /// Calculate full trajectory using numerical integration
//        /// </summary>
//        private void CalculateTrajectory()
//        {
//            trajectoryPositions.Clear();

//            Vector3 pos = initialPosition;
//            Vector3 vel = velocity;
//            float t = 0f;
//            float dt = integrationTimeStep;
//            int maxIterations = Mathf.Min(trajectoryPoints, 10000);

//            for (int i = 0; i < maxIterations; i++)
//            {
//                trajectoryPositions.Add(pos);

//                // Calculate forces
//                Vector3 dragForce = CalculateDragForce(vel);
//                Vector3 gravityForce = new Vector3(0, -Physics.gravity.magnitude * mass, 0);

//                // Update velocity and position
//                Vector3 acceleration = (dragForce + gravityForce) / mass;
//                vel += acceleration * dt;
//                pos += vel * dt;

//                t += dt;

//                // Stop if hit ground
//                if (pos.y <= 0f && t > 0.1f)
//                {
//                    LandingPosition = pos;
//                    TimeOfFlight = t;
//                    Range = Vector3.Distance(new Vector3(initialPosition.x, 0, initialPosition.z),
//                                           new Vector3(pos.x, 0, pos.z));
//                    break;
//                }
//            }
//        }

//        /// <summary>
//        /// Draw trajectory using LineRenderer
//        /// </summary>
//        private void DrawTrajectory()
//        {
//            if (trajectoryLine != null && trajectoryPositions.Count > 1)
//            {
//                trajectoryLine.positionCount = trajectoryPositions.Count;
//                trajectoryLine.SetPositions(trajectoryPositions.ToArray());
//            }
//        }

//        /// <summary>
//        /// Calculate drag constant based on physical parameters
//        /// </summary>
//        private void CalculateDragConstant()
//        {
//            if (dragModel == DragModel.Quadratic)
//            {
//                dragConstant = 0.5f * airDensity * crossSectionalArea * dragCoefficient;
//            }
//            else
//            {
//                dragConstant = linearDragCoefficient;
//            }
//        }

//        /// <summary>
//        /// Called when shuttlecock lands
//        /// </summary>
//        private void OnLanded()
//        {
//            isSimulating = false;
//            LandingPosition = transform.position;
//            TimeOfFlight = flightTime;
//            Range = Vector3.Distance(new Vector3(initialPosition.x, 0, initialPosition.z),
//                                   new Vector3(transform.position.x, 0, transform.position.z));

//            Debug.Log($"Shuttlecock landed! Range: {Range:F2}m, Max Height: {MaxHeight:F2}m, Time: {TimeOfFlight:F2}s");

//            // Check if it's a valid clear (lands behind baseline)
//            if (Range > netDistance)
//            {
//                Debug.Log($"Valid clear! Distance behind net: {Range - netDistance:F2}m");
//            }
//        }

//        /// <summary>
//        /// Analytical solution for linear drag (n=1)
//        /// </summary>
//        public Vector3 GetPositionLinearDrag(float t)
//        {
//            if (dragModel != DragModel.Linear)
//            {
//                Debug.LogWarning("This method only works for linear drag model");
//                return Vector3.zero;
//            }

//            float b = linearDragCoefficient;
//            float m = mass;
//            float g = Physics.gravity.magnitude;

//            // From the paper's analytical solution
//            float vx0 = velocity.x;
//            float vy0 = velocity.y;

//            float x = initialPosition.x + (vx0 * m / b) * (1f - Mathf.Exp(-b * t / m));

//            float vT = m * g / b; // Terminal velocity
//            float y = initialPosition.y + vT * t + (Mathf.Exp(-b * t / m) - 1f) * (vT * m / b - vy0 * m / b);

//            return new Vector3(x, y, initialPosition.z);
//        }

//        /// <summary>
//        /// Get optimal launch angle for maximum range (considering drag)
//        /// </summary>
//        public float GetOptimalLaunchAngle(float speed)
//        {
//            float bestAngle = 45f;
//            float maxRange = 0f;

//            // Test angles from 30 to 60 degrees (based on paper's findings)
//            for (float angle = 30f; angle <= 60f; angle += 1f)
//            {
//                Launch(speed, angle);
//                CalculateTrajectory();

//                if (Range > maxRange)
//                {
//                    maxRange = Range;
//                    bestAngle = angle;
//                }
//            }

//            return bestAngle;
//        }

//        void OnDrawGizmos()
//        {
//            if (!Application.isPlaying) return;

//            // Draw court boundaries
//            Gizmos.color = Color.green;
//            Vector3 netPos = new Vector3(netDistance, 0, 0);
//            Gizmos.DrawLine(netPos + Vector3.left * 3, netPos + Vector3.right * 3);
//            Gizmos.DrawLine(netPos, netPos + Vector3.up * netHeight);

//            // Draw landing position
//            if (LandingPosition != Vector3.zero)
//            {
//                Gizmos.color = Color.red;
//                Gizmos.DrawWireSphere(LandingPosition, 0.1f);
//            }

//            // Draw forces if enabled
//            if (showForces && isSimulating)
//            {
//                Gizmos.color = Color.blue;
//                Gizmos.DrawRay(transform.position, velocity.normalized * 2f);

//                Gizmos.color = Color.red;
//                Vector3 dragForce = CalculateDragForce(velocity);
//                Gizmos.DrawRay(transform.position, dragForce.normalized * 1f);
//            }
//        }
//    }
//}