using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Handles the building placement flow using the WorldGrid system.
///
/// PLACEMENT FLOW:
///   1. Player selects a building from BuildMenu
///   2. Ghost preview snaps to grid cells and follows mouse
///   3. Ghost turns red if the footprint is not buildable
///   4. Left-click to confirm, right-click / Escape to cancel
///
/// GRID INTEGRATION:
///   Buildings have a gridFootprint (width × height in cells) defined in BuildingData.
///   PlacementManager queries WorldGrid.IsBuildable() for validation and calls
///   WorldGrid.Occupy() after confirmation. WorldGrid is the single source of truth
///   for what is placed where — ObstacleRegistry is no longer used.
///
/// INITIALIZE PATTERN:
///   After attaching a script via AddComponent, PlacementManager calls
///   Initialize(Vector3) on the component if the method exists, before Start() runs.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Ghost Preview")]
    [SerializeField] private Color validColor   = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("References")]
    [SerializeField] private Camera _camera;

    // ── State ─────────────────────────────────────────────────────────
    public bool IsPlacing { get; private set; } = false;

    private BuildingData   _selectedBuilding;
    private GameObject     _ghostObject;
    private SpriteRenderer _ghostRenderer;
    private bool           _isValidPosition;
    private Vector2Int     _ghostGridPos;   // top-left grid cell of the ghost footprint

    private List<GameObject> _radiusGhostCells = new List<GameObject>();

    private List<PlacedBuilding> _placedBuildings = new List<PlacedBuilding>();

    public static event System.Action<PlacedBuilding> OnBuildingPlaced;
    public static event System.Action                 OnPlacementCancelled;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (!IsPlacing) return;
        UpdateGhostPosition();
        HandlePlacementInput();
    }

    // ── Public API ────────────────────────────────────────────────────

    public void BeginPlacement(BuildingData buildingData)
    {
        if (buildingData == null) return;

        if (GameManager.Instance != null && GameManager.Instance.Gold < buildingData.goldCost)
        {
            HUDManager.Instance?.ShowNotification(
                "Can't Afford",
                $"Need ${buildingData.goldCost} to build {buildingData.buildingName}",
                new Color(0.8f, 0.2f, 0.2f));
            return;
        }

        if (!MeetsUnlockRequirements(buildingData))
        {
            HUDManager.Instance?.ShowNotification(
                "Locked",
                $"{buildingData.buildingName} is not yet unlocked.",
                new Color(0.4f, 0.4f, 0.4f));
            return;
        }

        _selectedBuilding = buildingData;
        IsPlacing         = true;
        CreateGhost(buildingData);
        Debug.Log($"[Placement] Placing {buildingData.buildingName} " +
                  $"({buildingData.gridWidth}×{buildingData.gridHeight} cells) — " +
                  $"click to confirm, right-click to cancel.");
    }

    public void CancelPlacement()
    {
        IsPlacing         = false;
        _selectedBuilding = null;
        DestroyGhost();
        OnPlacementCancelled?.Invoke();
    }

    // ── Ghost Preview ─────────────────────────────────────────────────

    private void CreateGhost(BuildingData data)
    {
        DestroyGhost();

        _ghostObject                        = new GameObject("PlacementGhost");
        _ghostRenderer                      = _ghostObject.AddComponent<SpriteRenderer>();
        _ghostRenderer.sprite               = data.previewSprite != null ? data.previewSprite : data.builtSprite;
        _ghostRenderer.sortingOrder         = 10;
        _ghostRenderer.color                = validColor;

        ScaleGhostToFootprint(data);
    }

    private void ScaleGhostToFootprint(BuildingData data)
    {
        if (_ghostObject == null || WorldGrid.Instance == null) return;

        float targetW = data.gridWidth  * WorldGrid.Instance.CellSize;
        float targetH = data.gridHeight * WorldGrid.Instance.CellSize;

        if (_ghostRenderer.sprite != null)
        {
            float spriteW = _ghostRenderer.sprite.rect.width  / _ghostRenderer.sprite.pixelsPerUnit;
            float spriteH = _ghostRenderer.sprite.rect.height / _ghostRenderer.sprite.pixelsPerUnit;
            _ghostObject.transform.localScale = new Vector3(targetW / spriteW, targetH / spriteH, 1f);
        }
        else
        {
            _ghostObject.transform.localScale = new Vector3(targetW * 0.01f, targetH * 0.01f, 1f);
        }
    }

    private void UpdateGhostPosition()
    {
        if (_ghostObject == null || _camera == null || WorldGrid.Instance == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Convert mouse → world → nearest grid cell
        Vector3 mouseWorld = _camera.ScreenToWorldPoint(
            new Vector3(mouse.position.value.x, mouse.position.value.y, 0f));

        var grid = WorldGrid.Instance.WorldToGrid(mouseWorld);
        if (!WorldGrid.Instance.InBounds(grid.x, grid.y)) return;

        _ghostGridPos = grid;

        // World position = center of the footprint
        float cellSize = WorldGrid.Instance.CellSize;
        float cx = WorldGrid.Instance.GridToWorld(grid.x, grid.y).x
                   + (_selectedBuilding.gridWidth  - 1) * cellSize * 0.5f;
        float cy = WorldGrid.Instance.GridToWorld(grid.x, grid.y).y
                   - (_selectedBuilding.gridHeight - 1) * cellSize * 0.5f;

        _ghostObject.transform.position = new Vector3(cx, cy, -0.5f);

        _isValidPosition    = WorldGrid.Instance.IsBuildable(
            grid.x, grid.y, _selectedBuilding.gridWidth, _selectedBuilding.gridHeight);
        _ghostRenderer.color = _isValidPosition ? validColor : invalidColor;

        RefreshRadiusGhostCells();
    }

    private void HandlePlacementInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && _isValidPosition)
            ConfirmPlacement();

        if (mouse.rightButton.wasPressedThisFrame)
            CancelPlacement();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            CancelPlacement();
    }

    private void DestroyGhost()
    {
        if (_ghostObject != null) Destroy(_ghostObject);
        _ghostObject   = null;
        _ghostRenderer = null;

        ClearRadiusGhostCells();
    }

    private void RefreshRadiusGhostCells()
    {
        ClearRadiusGhostCells();

        if (WorldGrid.Instance == null || _selectedBuilding == null || _selectedBuilding.effectRadius <= 0)
            return;

        int r = _selectedBuilding.effectRadius;
        int gx = _ghostGridPos.x;
        int gy = _ghostGridPos.y;
        int gw = _selectedBuilding.gridWidth;
        int gh = _selectedBuilding.gridHeight;

        // The exact center point of the footprint in grid terms
        float centerGridX = gx + (gw - 1) / 2f;
        float centerGridY = gy + (gh - 1) / 2f;

        // Radius boundaries
        int minX = Mathf.FloorToInt(centerGridX - r);
        int maxX = Mathf.CeilToInt(centerGridX + r);
        int minY = Mathf.FloorToInt(centerGridY - r);
        int maxY = Mathf.CeilToInt(centerGridY + r);

        float cs = WorldGrid.Instance.CellSize;
        Color radiusColor = new Color(0.3f, 0.85f, 0.3f, 0.25f); // Transparent green

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!WorldGrid.Instance.InBounds(x, y)) continue;

                // Don't draw the radius effect directly *under* the building itself
                if (x >= gx && x < gx + gw && y >= gy && y < gy + gh) continue;

                Vector3 worldPos = WorldGrid.Instance.GridToWorld(x, y, -0.1f);

                var ghost = new GameObject($"RadiusCell_{x}_{y}");
                ghost.transform.SetParent(this.transform);
                ghost.transform.position = worldPos;

                var sr = ghost.AddComponent<SpriteRenderer>();
                sr.color = radiusColor;
                sr.sortingOrder = 9; // Below the building ghost (10)
                sr.sprite = GetWhiteSquareSprite();
                ghost.transform.localScale = new Vector3(cs * 0.92f, cs * 0.92f, 1f);

                _radiusGhostCells.Add(ghost);
            }
        }
    }

    private void ClearRadiusGhostCells()
    {
        foreach (var g in _radiusGhostCells)
            if (g != null) Destroy(g);
        _radiusGhostCells.Clear();
    }

    private static Sprite _whiteSquareSprite;
    private static Sprite GetWhiteSquareSprite()
    {
        if (_whiteSquareSprite != null) return _whiteSquareSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSquareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSquareSprite;
    }

    // ── Confirm Placement ─────────────────────────────────────────────

    private void ConfirmPlacement()
    {
        if (_selectedBuilding == null || _ghostObject == null) return;
        if (WorldGrid.Instance == null) return;

        int gx = _ghostGridPos.x;
        int gy = _ghostGridPos.y;
        int gw = _selectedBuilding.gridWidth;
        int gh = _selectedBuilding.gridHeight;

        // Double-check validity (mouse may have moved between UpdateGhost and click)
        if (!WorldGrid.Instance.IsBuildable(gx, gy, gw, gh))
        {
            Debug.LogWarning("[Placement] Placement invalid at confirm time — ignoring.");
            return;
        }

        // World position = center of the footprint, at z=0
        Vector3 worldCenter = new Vector3(
            _ghostObject.transform.position.x,
            _ghostObject.transform.position.y,
            0f);

        // Deduct gold
        GameManager.Instance?.SpendGold((float)_selectedBuilding.goldCost);

        // ── Create the building GameObject ────────────────────────────
        var buildingGO = new GameObject(_selectedBuilding.buildingName);
        buildingGO.transform.position = worldCenter;

        var sr             = buildingGO.AddComponent<SpriteRenderer>();
        sr.sprite          = _selectedBuilding.builtSprite;
        sr.sortingOrder    = 2;

        // Scale sprite to fill its grid footprint exactly
        if (sr.sprite != null)
        {
            float targetW = gw * WorldGrid.Instance.CellSize;
            float targetH = gh * WorldGrid.Instance.CellSize;
            float spriteW = sr.sprite.rect.width  / sr.sprite.pixelsPerUnit;
            float spriteH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
            buildingGO.transform.localScale = new Vector3(targetW / spriteW, targetH / spriteH, 1f);
        }

        // Register in WorldGrid — this is now the single source of truth
        WorldGrid.Instance.Occupy(gx, gy, gw, gh, CellType.Building, buildingGO, walkable: false);

        // ── Attach building script ────────────────────────────────────
        if (!string.IsNullOrEmpty(_selectedBuilding.scriptTypeName))
        {
            System.Type scriptType = System.Type.GetType(_selectedBuilding.scriptTypeName);
            if (scriptType != null)
            {
                var component  = buildingGO.AddComponent(scriptType);
                var initMethod = scriptType.GetMethod(
                    "Initialize",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new System.Type[] { typeof(Vector3) },
                    null);

                if (initMethod != null)
                {
                    initMethod.Invoke(component, new object[] { worldCenter });
                    Debug.Log($"[Placement] Called Initialize({worldCenter}) on {_selectedBuilding.scriptTypeName}");
                }
            }
            else
            {
                Debug.LogWarning($"[Placement] Script type not found: {_selectedBuilding.scriptTypeName}");
            }
        }

        // Track placement
        var placed = new PlacedBuilding
        {
            data        = _selectedBuilding,
            worldObject = buildingGO,
            gridOrigin  = new Vector2Int(gx, gy),
            center      = new Vector2(worldCenter.x, worldCenter.y)
        };
        _placedBuildings.Add(placed);
        OnBuildingPlaced?.Invoke(placed);

        Debug.Log($"[Placement] Placed {_selectedBuilding.buildingName} at grid ({gx},{gy}), world {worldCenter}");

        IsPlacing         = false;
        _selectedBuilding = null;
        DestroyGhost();
    }

    // ── Save / Load ───────────────────────────────────────────────────

    public void LoadFromSaveData(List<BuildingSaveData> savedBuildings, List<BuildingData> allBuildings)
    {
        if (savedBuildings == null || allBuildings == null || allBuildings.Count == 0) return;

        int loadedCount = 0;
        foreach (var savedData in savedBuildings)
        {
            // Find the matching ScriptableObject
            BuildingData match = null;
            foreach (var b in allBuildings)
            {
                if (b.name == savedData.buildingDataName)
                {
                    match = b;
                    break;
                }
            }

            if (match == null)
            {
                Debug.LogWarning($"[Placement] Could not find BuildingData '{savedData.buildingDataName}'. Skipping.");
                continue;
            }

            // Fake the ghost setup to reuse ConfirmPlacement logic
            _selectedBuilding = match;
            _ghostGridPos     = new Vector2Int(savedData.gridX, savedData.gridY);
            
            // Re-instantiate a dummy ghost just so ConfirmPlacement can read its position
            _ghostObject = new GameObject("DummyGhost");
            float cx = WorldGrid.Instance.GridToWorld(savedData.gridX, savedData.gridY).x
                       + (match.gridWidth  - 1) * WorldGrid.Instance.CellSize * 0.5f;
            float cy = WorldGrid.Instance.GridToWorld(savedData.gridX, savedData.gridY).y
                       - (match.gridHeight - 1) * WorldGrid.Instance.CellSize * 0.5f;
            _ghostObject.transform.position = new Vector3(cx, cy, 0f);

            // Temporarily suppress gold so loading doesn't bankrupt the player
            int originalCost = match.goldCost;
            match.goldCost = 0;
            
            ConfirmPlacement();
            
            // Restore actual cost
            match.goldCost = originalCost;
            loadedCount++;
        }

        Debug.Log($"[Placement] Rebuilt {loadedCount} buildings from save data.");
    }

    // ── Unlock Requirements ───────────────────────────────────────────

    private bool MeetsUnlockRequirements(BuildingData data)
    {
        if (GameManager.Instance == null) return true;
        if (data.reputationRequired > 0 &&
            GameManager.Instance.Reputation < data.reputationRequired) return false;
        foreach (var req in data.requiredBuildings)
            if (!_placedBuildings.Exists(p => p.data == req)) return false;
        return true;
    }

    public List<PlacedBuilding> PlacedBuildings => new List<PlacedBuilding>(_placedBuildings);
}

// ── Supporting Types ──────────────────────────────────────────────────

public class PlacedBuilding
{
    public BuildingData data;
    public GameObject   worldObject;
    public Vector2Int   gridOrigin;   // top-left grid cell
    public Vector2      center;       // world-space center
}
