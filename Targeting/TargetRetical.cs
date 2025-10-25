using UnityEngine;

/// <summary>
/// Simple world-space reticle presenter:
/// - Move reticle to target point
/// - Set color/material based on validity state
/// - Optionally face camera or align with ground normal
/// </summary>
public class TargetReticle : MonoBehaviour
{
    public enum State { Hidden, Valid, Clamped, Invalid }

    [Header("Visual")]
    public Renderer reticleRenderer;
    public Color validColor = new Color(0f, 1f, 0.6f, 0.9f);
    public Color clampedColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    public bool faceCamera = false;
    public bool alignToHitNormal = true;

    [Header("Scale")]
    public float baseScale = 0.2f;
    public float distanceScale = 0.02f; // scales up slightly with distance from camera

    private Camera cam;

    void Awake()
    {
        if (reticleRenderer == null) reticleRenderer = GetComponentInChildren<Renderer>(true);
        cam = Camera.main;
        SetState(State.Hidden);
    }

    public void SetState(State s)
    {
        if (reticleRenderer == null) return;
        if (s == State.Hidden)
        {
            if (reticleRenderer.gameObject.activeSelf) reticleRenderer.gameObject.SetActive(false);
            return;
        }
        if (!reticleRenderer.gameObject.activeSelf) reticleRenderer.gameObject.SetActive(true);

        Color c = validColor;
        if (s == State.Clamped) c = clampedColor;
        else if (s == State.Invalid) c = invalidColor;

        var mat = reticleRenderer.material;
        if (mat != null)
        {
            if (mat.HasProperty("_Color")) mat.color = c;
        }
    }

    public void UpdatePose(Vector3 position, Vector3 normal)
    {
        transform.position = position;

        if (faceCamera && cam != null)
        {
            transform.rotation = Quaternion.LookRotation((transform.position - cam.transform.position).normalized, Vector3.up);
        }
        else if (alignToHitNormal)
        {
            transform.rotation = Quaternion.LookRotation(-normal, Vector3.forward);
        }

        // Scale by distance to camera for readability
        if (cam != null)
        {
            float d = Vector3.Distance(cam.transform.position, transform.position);
            float s = baseScale + d * distanceScale;
            transform.localScale = new Vector3(s, s, s);
        }
    }
}
