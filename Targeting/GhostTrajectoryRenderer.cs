using UnityEngine;

/// <summary>
/// Draw a ballistic ghost trajectory with LineRenderer.
/// Safety:
/// - Auto-creates and caches a LineRenderer if missing (lazy in every API).
/// - All public methods call EnsureLR() so they are safe even if called before Awake.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class GhostTrajectoryRenderer : MonoBehaviour
{
    [Header("Rendering")]
    public int segments = 40;
    public Color validColor = new Color(0f, 1f, 0.6f, 0.9f);
    public Color clampedColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    public float width = 0.02f;

    // Note: keep private so it won't require Inspector assignment
    private LineRenderer lr;
    private Vector3[] points;

    void Awake()
    {
        EnsureLR();
        AllocateBuffer();
    }

    void OnEnable()
    {
        // Ensure again in case of domain reload or script recompile
        EnsureLR();
        if (lr != null)
        {
            lr.enabled = false;
            lr.positionCount = 0;
        }
    }

    void EnsureLR()
    {
        if (lr == null)
        {
            lr = GetComponent<LineRenderer>();
            if (lr == null)
            {
                lr = gameObject.AddComponent<LineRenderer>();
            }

            // Initialize material and widths once
            if (lr != null)
            {
                lr.positionCount = 0;
                lr.startWidth = width;
                lr.endWidth = width;
                if (lr.material == null)
                {
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                }
                lr.enabled = false;
            }
        }
    }

    void AllocateBuffer()
    {
        int seg = Mathf.Max(segments, 2);
        if (points == null || points.Length != seg)
        {
            points = new Vector3[seg];
        }
    }

    public void Show(ShotValidator.Result plan, Vector3 origin, bool isClamped)
    {
        EnsureLR();
        AllocateBuffer();
        if (lr == null) return;

        if (!plan.chosen.exists)
        {
            Hide();
            return;
        }

        int n = TrajectorySolver.Sample(origin, plan.chosen.launchVelocity, Physics.gravity, plan.chosen.flightTime, Mathf.Max(segments, 2), points);
        lr.positionCount = n;
        lr.SetPositions(points);

        Color c = isClamped ? clampedColor : validColor;
        lr.startColor = c;
        lr.endColor = c;
        lr.enabled = true;
    }

    public void ShowInvalid(Vector3 origin, Vector3 forward, float length = 0.5f)
    {
        EnsureLR();
        if (lr == null) return;

        lr.positionCount = 2;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + forward.normalized * length);
        lr.startColor = invalidColor;
        lr.endColor = invalidColor;
        lr.enabled = true;
    }

    public void Hide()
    {
        EnsureLR();
        if (lr == null) return;

        lr.enabled = false;
        lr.positionCount = 0;
    }
}