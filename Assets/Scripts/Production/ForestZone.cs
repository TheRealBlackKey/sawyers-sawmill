using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// ForestZone — manages a rectangular region of the WorldGrid as a planted forest.
///
/// LIFECYCLE:
///   1. Created at runtime by ForestZonePainter when the player paints a region.
///   2. Initialized via Initialize() with a grid origin, size, and species.
///   3. Trees grow, Sawyer harvests them. Each harvest increments that tree's cycle count.
///   4. When a tree's cycle count reaches species.maxHarvestCycles, it becomes a stump.
///   5. When ALL cells are stumps, the zone is depleted — OnZoneDepletedGlobal fires.
///   6. Sawyer clears stumps at low priority, freeing cells for building or repainting.
///
/// FIRST ZONE:
///   ForestZonePainter handles the free/capped first placement — ForestZone itself
///   doesn't need to know whether it's the first or not. It always uses Initialize().
/// </summary>
public class ForestZone : MonoBehaviour
{
    // ── Static Events ─────────────────────────────────────────────────
    public static event Action<WoodSpeciesData>  OnSpeciesUnlocked;
    public static event Action<ForestZone>       OnZoneDepletedGlobal;

    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Tree Settings")]
    [SerializeField] private GameObject treeSlotPrefab;
    [SerializeField] private Transform  treeContainer;

    [Header("Species Unlock Registry")]
    [Tooltip("All species the game knows about — used for progressive unlock checks. " +
             "Assign on the prefab so every zone can check unlocks.")]
    [SerializeField] private WoodSpeciesData[] allSpecies;

    [Header("Passive Seeding")]
    [Tooltip("Multiplier on species growth time for passive regrowth. Higher = slower.")]
    [SerializeField] private float passiveGrowthMultiplier = 5f;

    [Header("Log Pile Visual")]
    [SerializeField] private SpriteRenderer logPileRenderer;
    [SerializeField] private Sprite         logPileSmall;    // 1–3 logs
    [SerializeField] private Sprite         logPileMedium;   // 4–7 logs
    [SerializeField] private Sprite         logPileLarge;    // 8+ logs

    // ── Exposed for TreeComponent ──────────────────────────────────────
    public WoodSpeciesData StarterSpecies          => _assignedSpecies;
    public float           PassiveGrowthMultiplier => passiveGrowthMultiplier;

    // ── Runtime State ─────────────────────────────────────────────────
    private List<TreeComponent>   _treeSlots       = new List<TreeComponent>();
    private List<WoodSpeciesData> _unlockedSpecies = new List<WoodSpeciesData>();
    private WoodSpeciesData       _assignedSpecies;

    // Per-tree harvest cycle tracking
    private Dictionary<TreeComponent, int> _harvestCycles = new Dictionary<TreeComponent, int>();

    // Cached forest region bounds
    private int _regionStartX;
    private int _regionStartY;
    private int _regionWidth;
    private int _regionHeight;

    private bool _isInitialized  = false;
    private bool _isDepletedZone = false;

    // ── Public Accessors ──────────────────────────────────────────────
    public bool            IsDepletedZone  => _isDepletedZone;
    public WoodSpeciesData AssignedSpecies => _assignedSpecies;

    public int RegionStartX => _regionStartX;
    public int RegionStartY => _regionStartY;
    public int RegionWidth  => _regionWidth;
    public int RegionHeight => _regionHeight;

    // ── Initialization ────────────────────────────────────────────────

    /// <summary>
    /// Runtime initialization — called by ForestZonePainter immediately after instantiation.
    /// </summary>
    public void Initialize(Vector2Int gridOrigin, int width, int height, WoodSpeciesData species)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[ForestZone] Initialize called twice — ignoring.");
            return;
        }

        _regionStartX    = gridOrigin.x;
        _regionStartY    = gridOrigin.y;
        _regionWidth     = width;
        _regionHeight    = height;
        _assignedSpecies = species;

        if (species != null && !_unlockedSpecies.Contains(species))
            _unlockedSpecies.Add(species);

        _isInitialized = true;

        if (treeSlotPrefab != null)
            InitializeSlots();
        else
            Debug.LogWarning("[ForestZone] treeSlotPrefab not assigned.");

        TreeComponent.OnTreeReady     += OnTreeReady;
        TreeComponent.OnTreeHarvested += OnTreeHarvested;
        GameManager.OnItemProduced    += CheckSpeciesUnlocks;
    }

    private void OnDestroy()
    {
        TreeComponent.OnTreeReady     -= OnTreeReady;
        TreeComponent.OnTreeHarvested -= OnTreeHarvested;
        GameManager.OnItemProduced    -= CheckSpeciesUnlocks;
    }

#if UNITY_EDITOR
    // ── DEBUG: Press L to instantly add 10 logs ───────────────────────
    // Remove before shipping!
    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (!UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame) return;

        var inv = InventoryManager.Instance;
        if (inv == null || _assignedSpecies == null) return;

        for (int i = 0; i < 10; i++)
        {
            var log = new LumberItem(_assignedSpecies, ProcessingStage.Log, LumberItemType.Log);
            inv.AddItem(log, InventoryManager.InventoryZone.ForestStockpile);
        }

        GameManager.Instance?.RegisterLogHarvested(_assignedSpecies, 10);
        Debug.Log($"[DEBUG] Added 10 {_assignedSpecies.speciesName} logs.");
        CheckSpeciesUnlocksNow();
    }
#endif

    // ── Slot Initialization ───────────────────────────────────────────

    private void InitializeSlots()
    {
        if (WorldGrid.Instance == null) return;
        
        // Spawn one tree slot on every available cell within the painted region
        var emptyCells = WorldGrid.Instance.GetEmptyCellsInRegion(
            _regionStartX, _regionStartY, _regionWidth, _regionHeight);

        foreach (var cell in emptyCells)
        {
            Vector3 pos = WorldGrid.Instance.GridToWorld(cell.x, cell.y, 0f);

            var slotGO = Instantiate(treeSlotPrefab, pos, Quaternion.identity, treeContainer);
            var tree   = slotGO.GetComponent<TreeComponent>();

            WorldGrid.Instance.Occupy(cell.x, cell.y, 1, 1, CellType.Tree, slotGO, walkable: true);

            _treeSlots.Add(tree);
            _harvestCycles[tree] = 0;
        }
        
        Debug.Log($"[ForestZone] Spawned {_treeSlots.Count} tree slots in region.");
    }

    public TreeComponent AddTreeSlot()
    {
        Vector3 pos = GetRandomEmptyForestPosition();

        var slotGO = Instantiate(treeSlotPrefab, pos, Quaternion.identity, treeContainer);
        var tree   = slotGO.GetComponent<TreeComponent>();

        if (WorldGrid.Instance != null)
        {
            var cell = WorldGrid.Instance.WorldToGrid(pos);
            if (WorldGrid.Instance.InBounds(cell.x, cell.y))
                WorldGrid.Instance.Occupy(cell.x, cell.y, 1, 1,
                    CellType.Tree, slotGO, walkable: true);
        }

        _treeSlots.Add(tree);
        _harvestCycles[tree] = 0;
        return tree;
    }

    private Vector3 GetRandomEmptyForestPosition()
    {
        if (WorldGrid.Instance != null)
        {
            var emptyCells = WorldGrid.Instance.GetEmptyCellsInRegion(
                _regionStartX, _regionStartY, _regionWidth, _regionHeight);

            if (emptyCells.Count > 0)
            {
                var cell = emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];
                return WorldGrid.Instance.GridToWorld(cell.x, cell.y, 0f);
            }

            Debug.LogWarning("[ForestZone] No empty cells in forest region — forest is full.");
        }

        return transform.position + Vector3.right * (_treeSlots.Count * 48f);
    }

    public void ExpandSlots(int additionalSlots)
    {
        for (int i = 0; i < additionalSlots; i++)
            AddTreeSlot();
    }

    // ── Planting Interface ────────────────────────────────────────────

    public TreeComponent GetEmptySlot()
    {
        foreach (var slot in _treeSlots)
        {
            // Auto-planting should ONLY target completely empty (freshly painted) slots.
            // Stumps must be manually clicked by the player as per the new requirement.
            if (slot != null && slot.State == TreeState.Empty)
            {
                return slot;
            }
        }
        return null;
    }

    public void PlantInSlot(TreeComponent slot)
    {
        if (slot == null || !slot.IsAvailableForPlanting)
        {
            Debug.Log($"[ForestZone] PlantInSlot rejected — slot null: {slot == null}, available: {slot?.IsAvailableForPlanting}");
            return;
        }
        if (_assignedSpecies == null)
        {
            Debug.Log("[ForestZone] PlantInSlot rejected — _assignedSpecies is null");
            return;
        }

        // If replanting a depleted stump, clear the grid stump marker first
        if (slot.IsStump && WorldGrid.Instance != null)
        {
            var cell = WorldGrid.Instance.WorldToGrid(slot.transform.position);
            if (WorldGrid.Instance.InBounds(cell.x, cell.y) && WorldGrid.Instance.GetCell(cell.x, cell.y).IsStump)
                WorldGrid.Instance.ClearStump(cell.x, cell.y);
        }

        slot.PlantByWorker(_assignedSpecies);
        _harvestCycles[slot] = 0;
        Debug.Log($"[ForestZone] PlantInSlot complete — slot state now: {slot.State}");
    }

    public void RemoveTreeSlot(TreeComponent slot)
    {
        if (slot == null) return;
        _treeSlots.Remove(slot);
        if (_harvestCycles.ContainsKey(slot))
            _harvestCycles.Remove(slot);

        // Free the grid cell
        if (WorldGrid.Instance != null)
        {
            var cell = WorldGrid.Instance.WorldToGrid(slot.transform.position);
            if (WorldGrid.Instance.InBounds(cell.x, cell.y))
            {
                WorldGrid.Instance.Free(slot.gameObject);
                if (WorldGrid.Instance.GetCell(cell.x, cell.y).IsStump)
                    WorldGrid.Instance.ClearStump(cell.x, cell.y);
            }
        }
        
        Destroy(slot.gameObject);
        Debug.Log($"[ForestZone] Permanently removed tree slot at {slot.transform.position}");
    }

    // ── Depletion Tracking ────────────────────────────────────────────

    private bool RecordHarvestCycle(TreeComponent tree)
    {
        if (!_harvestCycles.ContainsKey(tree))
            _harvestCycles[tree] = 0;

        int maxCycles = _assignedSpecies != null ? _assignedSpecies.maxHarvestCycles : 3;
        if (maxCycles <= 0) return false;

        _harvestCycles[tree]++;

        if (_harvestCycles[tree] >= maxCycles)
        {
            ConvertToStump(tree);
            CheckZoneDepletion();
            return true;
        }
        return false;
    }

    private void ConvertToStump(TreeComponent tree)
    {
        if (WorldGrid.Instance != null)
        {
            var cell = WorldGrid.Instance.WorldToGrid(tree.transform.position);
            if (WorldGrid.Instance.InBounds(cell.x, cell.y))
            {
                WorldGrid.Instance.Free(tree.gameObject);
                WorldGrid.Instance.SetStump(cell.x, cell.y);
            }
        }

        tree.BecomeStump();
        Debug.Log($"[ForestZone] Tree at {tree.transform.position} depleted — now a stump.");
    }

    private void CheckZoneDepletion()
    {
        bool allStumps = true;
        foreach (var slot in _treeSlots)
        {
            if (slot == null) continue;
            if (!slot.IsStump) { allStumps = false; break; }
        }

        if (allStumps && !_isDepletedZone)
        {
            _isDepletedZone = true;
            Debug.Log($"[ForestZone] Zone depleted — all {_assignedSpecies?.speciesName} trees are stumps.");
            OnZoneDepletedGlobal?.Invoke(this);
        }
    }

    // ── Species Unlock ────────────────────────────────────────────────

    private void CheckSpeciesUnlocks(LumberItem item)
    {
        if (item.itemType != LumberItemType.Log) return;
        CheckSpeciesUnlocksNow();
    }

    public void CheckSpeciesUnlocksNow()
    {
        if (allSpecies == null) return;

        int   totalHarvested = GetTotalLogsHarvested();
        float currentGold    = GameManager.Instance != null ? GameManager.Instance.Gold : 0f;

        foreach (var species in allSpecies)
        {
            if (_unlockedSpecies.Contains(species)) continue;
            if (species.totalLogsToUnlock == 0) continue;

            bool meetsLogs = totalHarvested >= species.totalLogsToUnlock;
            bool meetsGold = species.unlockCost <= 0 || currentGold >= species.unlockCost;

            float progress = (float)totalHarvested / species.totalLogsToUnlock;
            if (progress >= 0.9f && progress < 1f)
                Debug.Log($"[Forest] Almost there! {totalHarvested}/{species.totalLogsToUnlock} " +
                          $"logs to unlock {species.speciesName}.");
            else if (progress >= 0.5f && progress < 0.55f)
                Debug.Log($"[Forest] Halfway to {species.speciesName} " +
                          $"({totalHarvested}/{species.totalLogsToUnlock} logs).");

            if (meetsLogs && meetsGold)
            {
                if (species.unlockCost <= 0)
                    UnlockSpecies(species);
                else
                    PurchaseSpeciesUnlock(species);
            }
            else if (meetsLogs && !meetsGold)
                Debug.Log($"[Forest] {species.speciesName} log requirement met " +
                          $"— needs ${species.unlockCost} gold. Current: ${currentGold:F0}");
        }
    }

    private int GetTotalLogsHarvested()
    {
        int total = 0;
        if (GameManager.Instance == null) return 0;
        foreach (var kvp in GameManager.Instance.TotalLogsHarvested)
            total += kvp.Value;
        return total;
    }

    public void UnlockSpecies(WoodSpeciesData species)
    {
        if (species == null || _unlockedSpecies.Contains(species)) return;
        _unlockedSpecies.Add(species);
        OnSpeciesUnlocked?.Invoke(species);

        Debug.Log($"[Forest] Unlocked {species.speciesName}! Paint a zone to grow it.");
        HUDManager.Instance?.ShowNotification(
            $"{species.speciesName} Unlocked!",
            $"Paint a new forest zone to grow {species.speciesName}. " +
            $"Worth ${species.valuePerBoardSurfaced} per board.",
            new Color(0.2f, 0.7f, 0.3f));
    }

    public bool PurchaseSpeciesUnlock(WoodSpeciesData species)
    {
        if (_unlockedSpecies.Contains(species)) return false;
        if (species.unlockCost > 0 && !GameManager.Instance.SpendGold(species.unlockCost))
            return false;
        UnlockSpecies(species);
        return true;
    }

    // ── Harvest Interface ─────────────────────────────────────────────

    public TreeComponent GetReadyTree()
    {
        foreach (var slot in _treeSlots)
            if (slot != null && slot.IsReadyToHarvest) return slot;
        return null;
    }

    /// <summary>
    /// Returns all mature trees in this zone. Used by Helpers (like the Lumberjack) 
    /// who need to filter available trees by their specific operational radius.
    /// </summary>
    public List<TreeComponent> GetAllMatureTrees()
    {
        List<TreeComponent> matureTrees = new List<TreeComponent>();
        foreach (var slot in _treeSlots)
        {
            if (slot != null && slot.IsReadyToHarvest)
            {
                matureTrees.Add(slot);
            }
        }
        return matureTrees;
    }

    public void HarvestTree(TreeComponent tree)
    {
        if (tree == null || !tree.IsReadyToHarvest) return;

        int logCount = tree.HarvestAndGetLogs(out WoodSpeciesData species);
        if (species == null || logCount == 0) return;

        var inv = InventoryManager.Instance;
        var gm  = GameManager.Instance;

        for (int i = 0; i < logCount; i++)
        {
            var log = new LumberItem(species, ProcessingStage.Log, LumberItemType.Log);
            inv?.AddItem(log, InventoryManager.InventoryZone.ForestStockpile);
        }

        gm?.RegisterLogHarvested(species, logCount);
        Debug.Log($"[Forest] Sawyer harvested {logCount} {species.speciesName} logs.");

        bool depleted = RecordHarvestCycle(tree);
        if (depleted)
            Debug.Log($"[Forest] Tree fully harvested — became a stump.");

        CheckSpeciesUnlocksNow();
        UpdateLogPileVisual();
    }

    /// <summary>
    /// Refreshes the log pile sprite based on how many logs of this zone's
    /// species are currently sitting in the ForestStockpile.
    /// Call after any harvest or after Sawyer picks up a log.
    /// </summary>
    public void UpdateLogPileVisual()
    {
        if (logPileRenderer == null) return;

        var inv = InventoryManager.Instance;
        int count = inv != null && _assignedSpecies != null
            ? inv.GetBySpecies(InventoryManager.InventoryZone.ForestStockpile, _assignedSpecies).Count
            : 0;

        if (count <= 0)
        {
            logPileRenderer.enabled = false;
            return;
        }

        logPileRenderer.enabled = true;
        logPileRenderer.transform.position = ForestCenter;
        logPileRenderer.sprite  = count >= 8 ? logPileLarge
                                : count >= 4 ? logPileMedium
                                :              logPileSmall;
    }

    // ── Accessors ─────────────────────────────────────────────────────

    public Vector3 ForestCenter
    {
        get
        {
            if (WorldGrid.Instance != null)
            {
                int cx = _regionStartX + _regionWidth  / 2;
                int cy = _regionStartY + _regionHeight / 2;
                return WorldGrid.Instance.GridToWorld(cx, cy, 0f);
            }
            return transform.position;
        }
    }

    private void OnTreeReady(TreeComponent tree)    { }
    private void OnTreeHarvested(TreeComponent tree, int logCount) { }

    public List<WoodSpeciesData> UnlockedSpecies      => new List<WoodSpeciesData>(_unlockedSpecies);
    public int                   TotalSlots           => _treeSlots.Count;
    public int EmptySlotCount    => _treeSlots.FindAll(t => t != null && t.IsAvailableForPlanting).Count;
    public int ReadyToHarvestCount => _treeSlots.FindAll(t => t != null && t.IsReadyToHarvest).Count;
    public int StumpCount        => _treeSlots.FindAll(t => t != null && t.IsStump).Count;
    public int GrowingCount      => _treeSlots.FindAll(
        t => t != null && (t.State == TreeState.Sapling || t.State == TreeState.YoungTree)).Count;

    /// <summary>Returns true if this zone manages the given tree slot.</summary>
    public bool OwnsSlot(TreeComponent slot) => slot != null && _treeSlots.Contains(slot);
}
