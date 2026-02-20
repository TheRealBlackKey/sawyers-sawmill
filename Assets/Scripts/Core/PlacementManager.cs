using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles the building placement flow.
/// Player selects a building from BuildMenu → ghost preview follows mouse
/// → turns red on invalid position → clicks to place and deduct gold.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Ghost Preview")]
    [SerializeField] private Color validColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("Placement Settings")]
    [SerializeField] private float groundY = -60f;      // Y position buildings snap to
    [SerializeField] private float snapIncrement = 5f;  // Snap to grid in world units

    [Header("References")]
    [SerializeField] private Camera _camera;

    // ── State ─────────────────────────────────────────────────────────
    public bool IsPlacing { get; private set; } = false;
    private BuildingData _selectedBuilding;
    private GameObject _ghostObject;
    private SpriteRenderer _ghostRenderer;
    private bool _isValidPosition = false;

    // Track all placed buildings
    private List<PlacedBuilding> _placedBuildings = new List<PlacedBuilding>();

    public static event System.Action<PlacedBuilding> OnBuildingPlaced;
    public static event System.Action OnPlacementCancelled;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (!IsPlacing) return;

        UpdateGhostPosition();
        HandlePlacementInput();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Enter placement mode for a specific building type.</summary>
    public void BeginPlacement(BuildingData buildingData)
    {
        if (buildingData == null) return;

        // Check if player can afford it
        if (GameManager.Instance != null && GameManager.Instance.Gold < buildingData.goldCost)
        {
            Debug.Log($"[Placement] Cannot afford {buildingData.buildingName} (costs {buildingData.goldCost} gold)");
            HUDManager.Instance?.ShowNotification(
                "Can't Afford",
                $"Need ${buildingData.goldCost} to build {buildingData.buildingName}",
                new Color(0.8f, 0.2f, 0.2f));
            return;
        }

        // Check unlock requirements
        if (!MeetsUnlockRequirements(buildingData))
        {
            HUDManager.Instance?.ShowNotification(
                "Locked",
                $"{buildingData.buildingName} is not yet unlocked.",
                new Color(0.4f, 0.4f, 0.4f));
            return;
        }

        _selectedBuilding = buildingData;
        IsPlacing = true;
        CreateGhost(buildingData);

        Debug.Log($"[Placement] Placing {buildingData.buildingName} — click to confirm, right-click to cancel");
    }

    /// <summary>Cancel the current placement.</summary>
    public void CancelPlacement()
    {
        IsPlacing = false;
        _selectedBuilding = null;
        DestroyGhost();
        OnPlacementCancelled?.Invoke();
    }

    // ── Ghost Preview ─────────────────────────────────────────────────

    private void CreateGhost(BuildingData data)
    {
        DestroyGhost();

        _ghostObject = new GameObject("PlacementGhost");
        _ghostRenderer = _ghostObject.AddComponent<SpriteRenderer>();
        _ghostRenderer.sprite = data.previewSprite != null ? data.previewSprite : data.builtSprite;
        _ghostRenderer.sortingOrder = 10;
        _ghostRenderer.color = validColor;

        // Scale ghost to match footprint
        if (_ghostRenderer.sprite != null)
        {
            float scaleX = data.footprintSize.x / (_ghostRenderer.sprite.rect.width / _ghostRenderer.sprite.pixelsPerUnit);
            float scaleY = data.footprintSize.y / (_ghostRenderer.sprite.rect.height / _ghostRenderer.sprite.pixelsPerUnit);
            _ghostObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            // No sprite — use a colored quad placeholder
            _ghostObject.transform.localScale = new Vector3(
                data.footprintSize.x * 0.01f,
                data.footprintSize.y * 0.01f, 1f);
        }
    }

    private void UpdateGhostPosition()
    {
        if (_ghostObject == null || _camera == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Convert mouse screen position to world position
        Vector3 mouseScreen = new Vector3(mouse.position.value.x, mouse.position.value.y, 0f);
        Vector3 worldPos = _camera.ScreenToWorldPoint(mouseScreen);

        // Snap X to grid, lock Y to ground level
        float snappedX = Mathf.Round(worldPos.x / snapIncrement) * snapIncrement;
        Vector3 ghostPos = new Vector3(snappedX, groundY, -0.5f);
        _ghostObject.transform.position = ghostPos;

        // Check validity
        _isValidPosition = CheckPlacementValid(new Vector2(snappedX, groundY), _selectedBuilding);
        _ghostRenderer.color = _isValidPosition ? validColor : invalidColor;
    }

    private void HandlePlacementInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Left click — confirm placement
        if (mouse.leftButton.wasPressedThisFrame && _isValidPosition)
        {
            ConfirmPlacement();
        }

        // Right click — cancel
        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
        }

        // Escape — cancel
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    private void DestroyGhost()
    {
        if (_ghostObject != null)
            Destroy(_ghostObject);
        _ghostObject = null;
        _ghostRenderer = null;
    }

    // ── Placement Validation ──────────────────────────────────────────

    private bool CheckPlacementValid(Vector2 center, BuildingData data)
    {
        if (ObstacleRegistry.Instance == null) return true;

        // Add a small padding around footprint for breathing room
        Vector2 paddedSize = data.footprintSize + new Vector2(10f, 5f);

        if (ObstacleRegistry.Instance.IsBlocked(center, paddedSize))
            return false;

        // Check minimum distance from sawmill if required
        if (data.minDistanceFromSawmill > 0f)
        {
            var sawmill = FindFirstObjectByType<SawmillBuilding>();
            if (sawmill != null)
            {
                float dist = Mathf.Abs(center.x - sawmill.transform.position.x);
                if (dist < data.minDistanceFromSawmill)
                    return false;
            }
        }

        return true;
    }

    // ── Confirm Placement ─────────────────────────────────────────────

    private void ConfirmPlacement()
    {
        if (_selectedBuilding == null || _ghostObject == null) return;

        Vector2 placedCenter = new Vector2(_ghostObject.transform.position.x, groundY);

        // Deduct gold
        if (GameManager.Instance != null)
            GameManager.Instance.SpendGold((float)_selectedBuilding.goldCost);

        // Create the actual building GameObject
        var buildingGO = new GameObject(_selectedBuilding.buildingName);
        buildingGO.transform.position = new Vector3(placedCenter.x, placedCenter.y, 0f);

        // Add sprite renderer with built sprite
        var sr = buildingGO.AddComponent<SpriteRenderer>();
        sr.sprite = _selectedBuilding.builtSprite;
        sr.sortingOrder = 1;

        // Scale to footprint
        if (sr.sprite != null)
        {
            float scaleX = _selectedBuilding.footprintSize.x / (sr.sprite.rect.width / sr.sprite.pixelsPerUnit);
            float scaleY = _selectedBuilding.footprintSize.y / (sr.sprite.rect.height / sr.sprite.pixelsPerUnit);
            buildingGO.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        // Register with ObstacleRegistry so future buildings can't overlap it
        ObstacleRegistry.Instance?.RegisterBounds(
            placedCenter,
            _selectedBuilding.footprintSize,
            ObstacleType.Building,
            buildingGO
        );

        // Attach the building's script component if specified
        if (!string.IsNullOrEmpty(_selectedBuilding.scriptTypeName))
        {
            System.Type scriptType = System.Type.GetType(_selectedBuilding.scriptTypeName);
            if (scriptType != null)
            {
                buildingGO.AddComponent(scriptType);
                Debug.Log($"[Placement] Attached {_selectedBuilding.scriptTypeName} to {_selectedBuilding.buildingName}");
            }
            else
            {
                Debug.LogWarning($"[Placement] Could not find script type: {_selectedBuilding.scriptTypeName}");
            }
        }

        // Track it
        var placed = new PlacedBuilding
        {
            data = _selectedBuilding,
            worldObject = buildingGO,
            center = placedCenter
        };
        _placedBuildings.Add(placed);
        OnBuildingPlaced?.Invoke(placed);

        Debug.Log($"[Placement] Placed {_selectedBuilding.buildingName} at {placedCenter}");

        // Clean up
        IsPlacing = false;
        DestroyGhost();
        _selectedBuilding = null;
    }

    // ── Unlock Requirements ───────────────────────────────────────────

    private bool MeetsUnlockRequirements(BuildingData data)
    {
        if (GameManager.Instance == null) return true;

        // Reputation check
        if (data.reputationRequired > 0 && GameManager.Instance.Reputation < data.reputationRequired)
            return false;

        // Required buildings check
        foreach (var req in data.requiredBuildings)
        {
            bool found = _placedBuildings.Exists(p => p.data == req);
            if (!found) return false;
        }

        return true;
    }

    public List<PlacedBuilding> PlacedBuildings => new List<PlacedBuilding>(_placedBuildings);
}

// ── Supporting Types ──────────────────────────────────────────────────

public class PlacedBuilding
{
    public BuildingData data;
    public GameObject worldObject;
    public Vector2 center;
}
