using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Handles player click interactions with individual tree slots.
///
/// INTERACTIONS:
///   Click a stump  → Sawyer walks over and clears/replants it immediately
///   Click an empty slot → Sawyer walks over and plants it immediately
///
/// Player-directed planting jumps ahead of Sawyer's auto-task queue.
/// Costs gold per sapling (set on WoodSpeciesData.saplingCost).
/// Blocked while ForestZonePainter is painting, confirming, or picker is open.
///
/// SETUP:
///   Add this component to any persistent GameObject (e.g. GameManager or a
///   dedicated InputHandlers object). No other wiring needed — it finds
///   SawyerWorker and ForestZones at runtime.
/// </summary>
public class TreeInteractionHandler : MonoBehaviour
{
    [Header("Visual Feedback")]
    [Tooltip("Cursor or highlight shown when hovering a plantable slot.")]
    [SerializeField] private GameObject plantCursorPrefab;

    [Header("Cost Label")]
    [Tooltip("Font for the cost label that appears above hovered slots.")]
    [SerializeField] private TMP_FontAsset costLabelFont = null;
    [SerializeField] private float costLabelOffsetY = 40f;   // pixels above the slot in screen space

    [Header("Click Detection")]
    [Tooltip("Layers that tree slot colliders live on.")]
    [SerializeField] private LayerMask treeSlotLayerMask = ~0;

    // ── Runtime refs ──────────────────────────────────────────────────
    private SawyerWorker      _sawyer;
    private ForestZonePainter _painter;
    private Camera            _cam;
    private GameObject        _cursorInstance;
    private GameObject        _costLabelGO;
    private TextMeshProUGUI   _costLabelText;
    private Canvas            _overlayCanvas;

    private TreeComponent _hoveredSlot;

    private void Start()
    {
        _sawyer  = FindFirstObjectByType<SawyerWorker>();
        _painter = FindFirstObjectByType<ForestZonePainter>();
        _cam     = Camera.main;

        // Find or create a screen-space canvas for the cost label
        _overlayCanvas = FindFirstObjectByType<Canvas>();

        if (_sawyer == null)
            Debug.LogWarning("[TreeInteractionHandler] No SawyerWorker found in scene.");
    }

    private void Update()
    {
        if (PainterIsActive())
        {
            ClearHover();
            return;
        }

        HandleHover();
        HandleClick();
    }

    // ── Input ─────────────────────────────────────────────────────────

    private void HandleHover()
    {
        TreeComponent slot = SlotUnderMouse();

        if (slot == _hoveredSlot) return;

        ClearHover();

        if (slot != null && slot.IsAvailableForPlanting)
        {
            _hoveredSlot = slot;
            ForestZone zone = FindZoneForSlot(slot);
            ShowHoverFeedback(slot, zone);
        }
    }

    private void HandleClick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        TreeComponent slot = SlotUnderMouse();
        if (slot == null) return;
        if (!slot.IsAvailableForPlanting) return;

        ForestZone zone = FindZoneForSlot(slot);
        if (zone == null)
        {
            Debug.LogWarning("[TreeInteractionHandler] Clicked slot has no owning ForestZone.");
            return;
        }

        if (zone.AssignedSpecies == null)
        {
            Debug.LogWarning("[TreeInteractionHandler] Zone has no assigned species — cannot plant.");
            return;
        }

        float cost = zone.AssignedSpecies.saplingCost;
        var gm = GameManager.Instance;

        if (gm != null && cost > 0f)
        {
            if (!gm.SpendGold(cost))
            {
                Debug.Log($"[TreeInteractionHandler] Not enough gold to plant {zone.AssignedSpecies.speciesName} (costs {cost}g).");
                FlashCostLabelRed();
                return;
            }
        }

        _sawyer.RequestPlantSlot(slot, zone);
        ClearHover();
    }

    // ── Raycasting ────────────────────────────────────────────────────

    private TreeComponent SlotUnderMouse()
    {
        if (_cam == null) return null;

        var mouse = Mouse.current;
        if (mouse == null) return null;

        Vector2 screenPos = mouse.position.ReadValue();
        Vector3 worldPos  = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -_cam.transform.position.z));
        RaycastHit2D hit  = Physics2D.Raycast(worldPos, Vector2.zero, 0f, treeSlotLayerMask);

        if (hit.collider == null) return null;
        return hit.collider.GetComponentInParent<TreeComponent>();
    }

    private ForestZone FindZoneForSlot(TreeComponent slot)
    {
        if (slot == null) return null;
        foreach (var zone in FindObjectsByType<ForestZone>(FindObjectsSortMode.None))
            if (zone.OwnsSlot(slot)) return zone;
        return null;
    }

    // ── Hover Feedback ────────────────────────────────────────────────

    private void ShowHoverFeedback(TreeComponent slot, ForestZone zone)
    {
        if (plantCursorPrefab != null && _cursorInstance == null)
            _cursorInstance = Instantiate(plantCursorPrefab, slot.transform.position, Quaternion.identity);

        // Green tint on the slot sprite
        var sr = slot.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.6f, 1f, 0.6f, 1f);

        // Cost label
        if (zone?.AssignedSpecies != null)
            ShowCostLabel(slot, zone.AssignedSpecies.saplingCost);
    }

    private void ShowCostLabel(TreeComponent slot, float cost)
    {
        if (_overlayCanvas == null) return;

        // Create label GO under the canvas
        _costLabelGO = new GameObject("CostLabel");
        _costLabelGO.transform.SetParent(_overlayCanvas.transform, false);

        _costLabelText = _costLabelGO.AddComponent<TextMeshProUGUI>();
        _costLabelText.text      = $"{cost:0}g";
        _costLabelText.fontSize  = 18f;
        _costLabelText.alignment = TextAlignmentOptions.Center;
        _costLabelText.color     = new Color(0.95f, 0.85f, 0.3f); // warm gold colour

        if (costLabelFont != null)
            _costLabelText.font = costLabelFont;

        // Check if player can afford — show red if not
        var gm = GameManager.Instance;
        if (gm != null && gm.Gold < cost)
            _costLabelText.color = new Color(1f, 0.3f, 0.3f);

        // Position above slot in screen space
        var rect = _costLabelGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 30f);

        UpdateCostLabelPosition(slot);
    }

    private void UpdateCostLabelPosition(TreeComponent slot)
    {
        if (_costLabelGO == null || _cam == null) return;

        Vector3 screenPos = _cam.WorldToScreenPoint(slot.transform.position);
        screenPos.y += costLabelOffsetY;

        // Convert screen pos to canvas local pos
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayCanvas.GetComponent<RectTransform>(),
            screenPos,
            _overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam,
            out Vector2 localPos);

        _costLabelGO.GetComponent<RectTransform>().localPosition = localPos;
    }

    private void LateUpdate()
    {
        // Keep cost label positioned above the hovered slot as camera moves
        if (_hoveredSlot != null && _costLabelGO != null)
            UpdateCostLabelPosition(_hoveredSlot);
    }

    private void FlashCostLabelRed()
    {
        if (_costLabelText != null)
            _costLabelText.color = new Color(1f, 0.2f, 0.2f);
    }

    private void ClearHover()
    {
        if (_hoveredSlot != null)
        {
            var sr = _hoveredSlot.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
            _hoveredSlot = null;
        }

        if (_cursorInstance != null) { Destroy(_cursorInstance); _cursorInstance = null; }
        if (_costLabelGO   != null) { Destroy(_costLabelGO);    _costLabelGO    = null; }
        _costLabelText = null;
    }

    // ── Painter Guard ─────────────────────────────────────────────────

    private bool PainterIsActive()
    {
        if (_painter == null) return false;
        return _painter.IsPainting || _painter.IsPickerOpen;
    }
}
