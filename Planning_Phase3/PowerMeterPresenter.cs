using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple power meter presenter:
/// - Shows current power 0..1 from PowerController
/// - Draws a sweet-spot region around ShotPlanner.ComputeSweetSpot(perfectSpeed)
/// - Auto show/hide based on PowerController charging state
/// Usage:
/// - Assign PowerController and RacketShotCoordinator.
/// - Provide Slider for the bar and an Image for sweet-spot overlay (fill or positioned rect).
/// </summary>
public class PowerMeterPresenter : MonoBehaviour
{
    [Header("Refs")]
    public PowerController power;
    public RacketShotCoordinator coordinator;

    [Header("UI")]
    public Slider bar;                  // main power slider (0..1)
    public Image sweetSpotFill;         // optional overlay; use Image type Filled (Horizontal) or a simple colored rect

    [Header("Sweet Spot Visual")]
    public Color sweetSpotColor = new Color(0f, 1f, 0.6f, 0.6f); // teal/green with alpha
    public bool showSweetSpot = true;

    [Header("Auto Show/Hide")]
    public bool autoShowHide = true;    // hide bar when not charging

    void OnEnable()
    {
        if (power != null)
        {
            power.onChargeStart.AddListener(OnChargeStart);
            power.onCharging.AddListener(OnCharging);
            power.onReleased.AddListener(OnReleased);
        }

        // Initialize hidden
        if (autoShowHide && bar != null) bar.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (power != null)
        {
            power.onChargeStart.RemoveListener(OnChargeStart);
            power.onCharging.RemoveListener(OnCharging);
            power.onReleased.RemoveListener(OnReleased);
        }
    }

    void OnChargeStart()
    {
        if (autoShowHide && bar != null)
        {
            bar.gameObject.SetActive(true);
        }

        // Setup sweet spot visual when charging starts
        if (showSweetSpot && sweetSpotFill != null && coordinator != null)
        {
            sweetSpotFill.gameObject.SetActive(true);
            sweetSpotFill.color = sweetSpotColor;
        }
    }

    void OnCharging(float current01)
    {
        if (bar != null)
        {
            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.value = current01;
        }
    }

    void OnReleased(float final01)
    {
        if (autoShowHide && bar != null)
        {
            bar.gameObject.SetActive(false);
        }

        if (sweetSpotFill != null)
        {
            sweetSpotFill.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Update sweet spot position/size based on coordinator's perfect power
        if (showSweetSpot && sweetSpotFill != null && coordinator != null && power != null && power.IsCharging)
        {
            float perfectP = coordinator.GetSweetSpot01();
            float width = coordinator.GetSweetWidth01();

            // Simple approach: set fillAmount to show sweet spot region
            // (For a more sophisticated UI, use a child Image with anchors positioned at perfectP-width/2 .. perfectP+width/2)
            // Here we just use fillAmount as a visual marker at the perfect power level
            sweetSpotFill.fillAmount = Mathf.Clamp01(perfectP);

            // Optionally: if you want a "band" effect, create a separate RectTransform child
            // and position it based on perfectP and width. For simplicity, we'll keep this basic.
        }
    }
}