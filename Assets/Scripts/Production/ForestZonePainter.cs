using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Sawmill.UI;

/// <summary>
/// ForestZonePainter — handles the player interaction for painting new forest zones.
///
/// FIRST ZONE (tutorial):
///   The very first zone is free, forced to Pine, and capped at firstZoneMaxCells.
///   A tutorial hint panel explains this before the player starts dragging.
///   After the first zone is placed, normal rules apply (gold cost, species picker).
///
/// NORMAL FLOW:
///   1. BeginPainting() called from BuildMenu.
///   2. Player drag-paints a rectangular region — cells highlight green/red.
///   3. On mouse release, a species picker popup appears (built at runtime in code).
///   4. Player selects species and confirms — gold is deducted.
///   5. ForestZone is instantiated and Initialize() is called on it.
///
/// SETUP:
///   - Attach to a persistent manager GameObject.
///   - Assign forestZonePrefab (GameObject with ForestZone component, useInspectorRegion = false).
///   - Tune goldPerCell and firstZoneMaxCells in Inspector.
///   - The species picker UI is built entirely in code — no prefabs needed.
/// </summary>
public class ForestZonePainter : MonoBehaviour
{
    public static ForestZonePainter Instance { get; private set; }

    [Header("Prefabs & References")]
    [Tooltip("ForestZone prefab — must have ForestZone component with useInspectorRegion false.")]
    [SerializeField] private GameObject forestZonePrefab;
    [SerializeField] private Camera _camera;

    [Header("Zone Costs")]
    [Tooltip("Gold cost per cell for normal zones. First zone is always free.")]
    [SerializeField] private float goldPerCell = 5f;

    [Header("First Zone (Tutorial)")]
    [Tooltip("Max number of cells the player can paint for their first (free) zone.")]
    [SerializeField] private int firstZoneMaxCells = 12;
    [Tooltip("The Pine species asset — forced for the first zone.")]
    [SerializeField] private WoodSpeciesData pineSpecies;

    [Header("Species Registry")]
    [Tooltip("All species in the game — used to populate the picker. " +
             "Assign WSD_Pine, WSD_Oak, WSD_Walnut here.")]
    [SerializeField] private WoodSpeciesData[] allSpecies;
    [SerializeField] private Color validCellColor   = new Color(0.3f, 0.85f, 0.3f, 0.45f);
    [SerializeField] private Color invalidCellColor = new Color(0.9f, 0.2f, 0.2f, 0.45f);
    [SerializeField] private Color cappedCellColor  = new Color(0.9f, 0.7f, 0.1f, 0.45f);  // orange = over size cap

    // ── State ─────────────────────────────────────────────────────────
    public bool IsPainting { get; private set; } = false;

    /// <summary>True while the species picker popup is visible — blocks drag input.</summary>
    public bool IsPickerOpen { get; private set; } = false;

    /// <summary>True once the player has confirmed their first (free) Pine zone.</summary>
    public bool HasPlacedFirstZone => !_isFirstZone;

    /// <summary>Exposed so BuildMenu can show the per-cell cost in the entry label.</summary>
    public float GoldPerCell => goldPerCell;

    private bool       _isFirstZone        = true;
    private bool       _isDragging         = false;
    private bool       _isAwaitingConfirm  = false;  // drag done, green grid shown, waiting for confirm
    private Vector2Int _dragStart          = Vector2Int.one * -1;
    private Vector2Int _dragCurrent        = Vector2Int.one * -1;

    private List<GameObject> _ghostCells = new List<GameObject>();

    // Set when mouse released, before species pick
    private Vector2Int _confirmedOrigin;
    private int        _confirmedWidth;
    private int        _confirmedHeight;

    // Runtime picker UI
    private GameObject      _pickerRoot;
    private WoodSpeciesData _pickedSpecies;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (!IsPainting) return;
        if (IsPickerOpen) return;
        if (_isAwaitingConfirm) return;  // grid shown, waiting for confirm/redraw button
        UpdateDrag();
    }

    // ── Public API ────────────────────────────────────────────────────

    public void BeginPainting()
    {
        if (IsPainting) return;
        IsPainting          = true;
        _isDragging         = false;
        _isAwaitingConfirm  = false;

        if (_isFirstZone)
        {
            _pickedSpecies = pineSpecies;
            Debug.Log($"[ForestPainter] First zone — free Pine zone, max {firstZoneMaxCells} cells. " +
                      "Drag to mark your starting forest.");
            HUDManager.Instance?.ShowNotification(
                "Plant Your First Forest",
                $"Drag to mark an area for Pine trees. Free! Max {firstZoneMaxCells} cells.",
                new Color(0.2f, 0.65f, 0.25f));
        }
        else
        {
            Debug.Log("[ForestPainter] Drag to plant a new forest zone.");
            HUDManager.Instance?.ShowNotification(
                "Plant Forest Zone",
                "Drag across the grassy tiles to select a planting area.",
                new Color(0.2f, 0.65f, 0.25f));
        }
    }

    public void CancelPainting()
    {
        IsPainting         = false;
        _isDragging        = false;
        _isAwaitingConfirm = false;
        ClearGhostCells();
        DestroyPicker();
    }

    // ── Drag Input ────────────────────────────────────────────────────

    private void UpdateDrag()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelPainting();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int cell = MouseToGrid();
            if (WorldGrid.Instance != null && WorldGrid.Instance.InBounds(cell.x, cell.y))
            {
                _dragStart   = cell;
                _dragCurrent = cell;
                _isDragging  = true;
                RefreshGhostCells();
            }
        }

        if (_isDragging && mouse.leftButton.isPressed)
        {
            Vector2Int cell = MouseToGrid();
            if (WorldGrid.Instance != null && WorldGrid.Instance.InBounds(cell.x, cell.y))
            {
                if (cell != _dragCurrent)
                {
                    _dragCurrent = cell;
                    RefreshGhostCells();
                }
            }
        }

        if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            FinalizePaintedRegion();
        }
    }

    private Vector2Int MouseToGrid()
    {
        if (_camera == null || WorldGrid.Instance == null) return Vector2Int.one * -1;
        var mouse = Mouse.current;
        if (mouse == null) return Vector2Int.one * -1;
        Vector3 worldPos = _camera.ScreenToWorldPoint(
            new Vector3(mouse.position.value.x, mouse.position.value.y, 0f));
        return WorldGrid.Instance.WorldToGrid(worldPos);
    }

    // ── Ghost Cell Visualization ──────────────────────────────────────

    private void RefreshGhostCells()
    {
        ClearGhostCells();
        if (WorldGrid.Instance == null) return;

        GetRegionFromDrag(out int ox, out int oy, out int w, out int h);
        int totalCells = w * h;

        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = ox + dx, cy = oy + dy;

            bool cellPaintable = WorldGrid.Instance.InBounds(cx, cy) &&
                                 (WorldGrid.Instance.GetCell(cx, cy).IsEmpty ||
                                  WorldGrid.Instance.GetCell(cx, cy).IsStump);

            // For the first zone: cells beyond the size cap shown in orange
            bool overCap = _isFirstZone && totalCells > firstZoneMaxCells;

            Vector3 worldPos = WorldGrid.Instance.GridToWorld(cx, cy, -0.1f);
            float cs = WorldGrid.Instance.CellSize;

            var ghost = new GameObject($"GhostCell_{cx}_{cy}");
            ghost.transform.position = worldPos;

            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.color        = !cellPaintable ? invalidCellColor
                            : overCap        ? cappedCellColor
                            :                  validCellColor;
            sr.sortingOrder = 20;
            sr.sprite       = GetWhiteSquareSprite();
            ghost.transform.localScale = new Vector3(cs * 0.92f, cs * 0.92f, 1f);

            _ghostCells.Add(ghost);
        }
    }

    private void ClearGhostCells()
    {
        foreach (var g in _ghostCells)
            if (g != null) Destroy(g);
        _ghostCells.Clear();
    }

    // ── Region Calculation ────────────────────────────────────────────

    private void GetRegionFromDrag(out int ox, out int oy, out int w, out int h)
    {
        ox = Mathf.Min(_dragStart.x, _dragCurrent.x);
        oy = Mathf.Min(_dragStart.y, _dragCurrent.y);
        int ex = Mathf.Max(_dragStart.x, _dragCurrent.x);
        int ey = Mathf.Max(_dragStart.y, _dragCurrent.y);
        w  = ex - ox + 1;
        h  = ey - oy + 1;
    }

    private void FinalizePaintedRegion()
    {
        if (WorldGrid.Instance == null) return;

        GetRegionFromDrag(out int ox, out int oy, out int w, out int h);
        int totalCells = w * h;

        // First zone size cap
        if (_isFirstZone && totalCells > firstZoneMaxCells)
        {
            HUDManager.Instance?.ShowNotification(
                "Zone Too Large",
                $"Your first forest zone can be at most {firstZoneMaxCells} cells. " +
                $"You painted {totalCells} — try a smaller area.",
                new Color(0.85f, 0.5f, 0.1f));
            // Keep green cells so player sees what they painted, but allow them to just drag again
            return;
        }

        _confirmedOrigin = new Vector2Int(ox, oy);
        _confirmedWidth  = w;
        _confirmedHeight = h;

        // Count only the VALID (paintable) cells for the cost calculation.
        // Even if they dragged over a river, they only pay for the grassy spots.
        int paintableCells = 0;
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (WorldGrid.Instance.IsZonePaintable(ox + i, oy + j, 1, 1))
                {
                    paintableCells++;
                }
            }
        }

        // Open the picker acting as our confirmation screen.
        ShowSpeciesPicker(paintableCells);
    }

    // ── Species Picker (built in code) ────────────────────────────────

    private void ShowSpeciesPicker(int totalCells)
    {
        DestroyPicker();
        IsPickerOpen = true;

        var unlocked = new List<WoodSpeciesData>();
        if (_isFirstZone && pineSpecies != null)
        {
            unlocked.Add(pineSpecies);
        }
        else
        {
            unlocked = GetAllUnlockedSpecies();
            if (unlocked.Count == 0)
            {
                Debug.LogWarning("[ForestPainter] No unlocked species — defaulting to Pine.");
                if (pineSpecies != null) unlocked.Add(pineSpecies);
                else { CancelPainting(); return; }
            }
        }

        if (SawyerSpeciesPickerController.Instance != null)
        {
            // Subscribe to completion events
            SawyerSpeciesPickerController.Instance.OnSpeciesConfirmed += HandleSpeciesConfirmed;
            SawyerSpeciesPickerController.Instance.OnPickerCancelled += HandlePickerCancelled;
            
            // Show the new UI Toolkit picker, now with totalCells
            SawyerSpeciesPickerController.Instance.Show(unlocked, _isFirstZone, goldPerCell, totalCells);
        }
        else
        {
            Debug.LogError("[ForestPainter] SawyerSpeciesPickerController.Instance is missing! Did you forget to add the Component to the scene?");
            IsPainting = false;
        }
    }

    private void HandleSpeciesConfirmed(WoodSpeciesData species)
    {
        // Unsubscribe
        if (SawyerSpeciesPickerController.Instance != null)
        {
            SawyerSpeciesPickerController.Instance.OnSpeciesConfirmed -= HandleSpeciesConfirmed;
            SawyerSpeciesPickerController.Instance.OnPickerCancelled -= HandlePickerCancelled;
        }

        IsPickerOpen = false;
        _pickedSpecies = species;
        
        int paintableCells = 0;
        for (int i = 0; i < _confirmedWidth; i++)
        {
            for (int j = 0; j < _confirmedHeight; j++)
            {
                if (WorldGrid.Instance != null && WorldGrid.Instance.IsZonePaintable(_confirmedOrigin.x + i, _confirmedOrigin.y + j, 1, 1))
                {
                    paintableCells++;
                }
            }
        }
        
        if (_isFirstZone)
        {
            SpawnForestZone(_confirmedOrigin, _confirmedWidth, _confirmedHeight, species, free: true, paintableCells);
            _isFirstZone = false;
        }
        else
        {
            float totalCost = paintableCells * goldPerCell;

            if (!GameManager.Instance.SpendGold(totalCost))
            {
                HUDManager.Instance?.ShowNotification(
                    "Insufficient Gold", $"Requires ${totalCost:F0} to plant.",
                    new Color(0.8f, 0.2f, 0.2f));
                ClearGhostCells();
                return;
            }

            SpawnForestZone(_confirmedOrigin, _confirmedWidth, _confirmedHeight, species, free: false, paintableCells);
        }

        ClearGhostCells();
        IsPainting = false;
    }

    private void HandlePickerCancelled()
    {
        // Unsubscribe
        if (SawyerSpeciesPickerController.Instance != null)
        {
            SawyerSpeciesPickerController.Instance.OnSpeciesConfirmed -= HandleSpeciesConfirmed;
            SawyerSpeciesPickerController.Instance.OnPickerCancelled -= HandlePickerCancelled;
        }

        IsPickerOpen = false;
        ClearGhostCells(); // Clear visuals on cancel so it doesn't look weird
        // We remain in IsPainting = true; mode so they can instantly drag again.
    }
    
    private void DestroyPicker()
    {
        _pickedSpecies = null;
        IsPickerOpen   = false;
        
        // Hide UI Toolkit if available
        if (SawyerSpeciesPickerController.Instance != null)
        {
            SawyerSpeciesPickerController.Instance.Hide();
        }
    }



    // ── Zone Spawning ─────────────────────────────────────────────────

    private void SpawnForestZone(Vector2Int origin, int width, int height,
                                  WoodSpeciesData species, bool free, int actualCells)
    {
        if (forestZonePrefab == null)
        {
            Debug.LogError("[ForestPainter] forestZonePrefab not assigned!");
            return;
        }

        Vector3 worldCenter = WorldGrid.Instance != null
            ? WorldGrid.Instance.GridToWorld(origin.x + width / 2, origin.y + height / 2, 0f)
            : Vector3.zero;

        var zoneGO = Instantiate(forestZonePrefab, worldCenter, Quaternion.identity);
        zoneGO.name = $"ForestZone_{species?.speciesName}_{origin.x}_{origin.y}";

        var zone = zoneGO.GetComponent<ForestZone>();
        if (zone == null)
        {
            Debug.LogError("[ForestPainter] forestZonePrefab has no ForestZone component!");
            Destroy(zoneGO);
            return;
        }

        zone.Initialize(origin, width, height, species);

        string costMsg = free ? "Free starter zone!" : $"Cost: ${actualCells * goldPerCell:F0}";
        Debug.Log($"[ForestPainter] Planted {species?.speciesName} zone at ({origin.x},{origin.y}) " +
                  $"{actualCells} cells. {costMsg}");

        HUDManager.Instance?.ShowNotification(
            free ? "First Forest Planted!" : "Forest Zone Planted!",
            $"Sawyer will grow {species?.speciesName} here. {costMsg}",
            new Color(0.2f, 0.7f, 0.3f));
    }

    // ── Save / Load ───────────────────────────────────────────────────

    public void LoadFromSaveData(List<ForestZoneSaveData> savedZones)
    {
        if (savedZones == null || savedZones.Count == 0 || allSpecies == null) return;

        // Skip the tutorial "free Pine" if they have already saved zones
        _isFirstZone = false; 

        int loadedCount = 0;
        foreach (var data in savedZones)
        {
            WoodSpeciesData match = null;
            foreach (var s in allSpecies)
            {
                if (s != null && s.name == data.speciesDataName)
                {
                    match = s;
                    break;
                }
            }

            if (match == null)
            {
                Debug.LogWarning($"[ForestPainter] Unknown species '{data.speciesDataName}' in save data. Skipping zone.");
                continue;
            }

            // Spawn the zone freely (assuming all loaded cells are valid)
            SpawnForestZone(new Vector2Int(data.gridX, data.gridY), data.width, data.height, match, free: true, data.width * data.height);
            loadedCount++;
        }

        Debug.Log($"[ForestPainter] Rebuilt {loadedCount} forest zones from save data.");
    }

    // ── Species Lookup ────────────────────────────────────────────────

    private List<WoodSpeciesData> GetAllUnlockedSpecies()
    {
        var result = new List<WoodSpeciesData>();

        // First: check which species are unlocked across all live zones
        var unlockedSet = new System.Collections.Generic.HashSet<WoodSpeciesData>();
        var allZones    = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
        foreach (var z in allZones)
            foreach (var s in z.UnlockedSpecies)
                unlockedSet.Add(s);

        // Then: return species from the registry in order, filtered to unlocked ones
        // Pine is always available. Others must be unlocked.
        if (allSpecies != null)
        {
            foreach (var s in allSpecies)
            {
                if (s == null) continue;
                // Pine (zero unlock threshold) always available
                // Others: must appear in an existing zone's unlocked list
                bool isStarter  = s.totalLogsToUnlock == 0;
                bool isUnlocked = unlockedSet.Contains(s);
                if (isStarter || isUnlocked)
                    result.Add(s);
            }
        }

        // Fallback: if registry empty, use zone-collected list directly
        if (result.Count == 0)
            foreach (var s in unlockedSet)
                result.Add(s);

        return result;
    }



    // ── Helpers ───────────────────────────────────────────────────────

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
}
