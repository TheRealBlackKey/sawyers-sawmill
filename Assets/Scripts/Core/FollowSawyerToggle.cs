using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to the Follow Sawyer toggle UI element.
///
/// Setup in Inspector:
///   1. Create a UI Toggle in HUD_Canvas/HUD_Root
///   2. Attach this script to the Toggle GameObject
///   3. Assign the CameraScroll reference (or leave null to auto-find)
///   4. The Toggle's OnValueChanged can call SetFollow(bool) directly,
///      OR this script handles it automatically via Start() subscription.
///
/// The toggle also shows a small camera icon label that changes to indicate
/// whether follow is active.
/// </summary>
public class FollowSawyerToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraScroll cameraScroll;
    [SerializeField] private Toggle       toggle;
    [SerializeField] private TextMeshProUGUI labelText;   // Optional label next to checkbox

    [Header("Labels")]
    [SerializeField] private string labelFollow = "Follow Sawyer";
    [SerializeField] private string labelFree   = "Follow Sawyer";

    private void Start()
    {
        // Auto-find if not assigned
        if (cameraScroll == null)
            cameraScroll = Camera.main?.GetComponent<CameraScroll>();

        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (toggle != null)
        {
            // Set initial visual state
            toggle.isOn = cameraScroll != null && cameraScroll.IsFollowing;
            toggle.onValueChanged.AddListener(SetFollow);
        }

        UpdateLabel();
    }

    private void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(SetFollow);
    }

    /// <summary>Called by the Toggle's OnValueChanged event.</summary>
    public void SetFollow(bool value)
    {
        if (cameraScroll != null)
            cameraScroll.FollowMode = value;

        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (labelText == null) return;
        bool isFollowing = cameraScroll != null && cameraScroll.IsFollowing;
        labelText.text = isFollowing ? labelFollow : labelFree;
    }

    private void Update()
    {
        // Sync toggle visual if follow mode was changed externally
        // (e.g. PanTo() disengages follow — we want the checkbox to uncheck)
        if (toggle != null && cameraScroll != null)
        {
            if (toggle.isOn != cameraScroll.IsFollowing)
            {
                // Temporarily remove listener to avoid feedback loop
                toggle.onValueChanged.RemoveListener(SetFollow);
                toggle.isOn = cameraScroll.IsFollowing;
                toggle.onValueChanged.AddListener(SetFollow);
                UpdateLabel();
            }
        }
    }
}
