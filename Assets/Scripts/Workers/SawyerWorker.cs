using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sawmill.UI;

/// <summary>
/// Sawyer - the main character. Handles all physical transport, delivery,
/// tree planting, and stump clearing.
///
/// PRIORITY ORDER (lowest number = highest priority):
///   0. Harvest ready trees             (walk to tree, chop 10s, logs added to stockpile)
///   1. Surfaced boards -> Market       (pickup SurfacingOutput, drop MarketReady)
///   2. Kiln-dried boards -> Surfacing  (pickup KilnOutput, call ProcessBoard, WAIT)
///   3. Milled boards -> Kiln           (pickup MillOutput, call LoadBoard)
///   4. Logs -> Sawmill                 (pickup ForestStockpile, drop MillInput, WAIT)
///   5. Plant empty tree slots          (walk to slot, plant seedling)
///   5.5 Clear stumps                  (walk to stump, clear it — frees the cell for building)
///
/// Note: In Phase 2, as players build out a specialized supply chain (Fellers, Haulers, etc.),
/// Sawyer's generalist logic may be refactored or heavily penalized to encourage automation.
/// </summary>
public class SawyerWorker : WorkerBase
{
    [Header("Sawyer Settings")]
    [SerializeField] private float taskScanInterval       = 1f;
    [SerializeField] private float buildingRescanInterval = 3f;

    [Header("Stump Clearing")]
    [Tooltip("How many seconds Sawyer takes to clear a single stump.")]
    [SerializeField] private float stumpClearDuration = 3f;

    [Header("Planting Priority")]
    [Tooltip("Species with valuePerBoardSurfaced above this threshold get planted before " +
             "any production chain work. Set to 0 to always plant first. Set high to never interrupt.")]
    [SerializeField] private float valuePlantingThreshold = 0f;

    private float _scanTimer;
    private float _rescanTimer;

    private bool _waitingAtSawmill   = false;
    private bool _waitingAtSurfacing = false;
    private bool _harvesting         = false;
    private bool _clearingStump      = false;
    private bool _planting           = false;

    private SawmillBuilding   _sawmill;
    private KilnBuilding      _kiln;
    private SurfacingBuilding _surfacing;
    private MarketBuilding    _market;
    private ForestZone        _forest;

    // Player-requested planting — stored as a queue, consumed when Sawyer next goes idle
    private readonly Queue<(TreeComponent slot, ForestZone zone)> _pendingPlantRequests
        = new Queue<(TreeComponent, ForestZone)>();

    // Player-requested stump clearing
    private readonly Queue<Vector2Int> _pendingClearRequests = new Queue<Vector2Int>();

    public void SetBuildingReferences(SawmillBuilding mill, KilnBuilding kiln,
        SurfacingBuilding surf, MarketBuilding market)
    {
        _sawmill   = mill;
        _kiln      = kiln;
        _surfacing = surf;
        _market    = market;
    }

    /// <summary>
    /// Called by TreeInteractionHandler when the player clicks a stump or empty slot.
    /// Stores the request in a queue — Sawyer works through them one at a time as he goes idle.
    /// Never interrupts mid-task.
    /// </summary>
    public void RequestPlantSlot(TreeComponent slot, ForestZone zone)
    {
        if (slot == null || zone == null) return;
        if (!slot.IsAvailableForPlanting) return;

        _pendingPlantRequests.Enqueue((slot, zone));
        Debug.Log($"[Sawyer] Plant request queued for {zone.AssignedSpecies?.speciesName} at {slot.transform.position} ({_pendingPlantRequests.Count} in queue)");
    }

    protected override void Start()
    {
        base.Start();
        RefreshBuildingReferences();
    }

    protected override void Update()
    {
        base.Update();

        _rescanTimer += Time.deltaTime;
        if (_rescanTimer >= buildingRescanInterval)
        {
            _rescanTimer = 0f;
            RefreshBuildingReferences();
        }

        if (_waitingAtSawmill || _waitingAtSurfacing || _harvesting || _clearingStump || _planting) return;

        if (CurrentState == WorkerState.Idle && _taskQueue.Count == 0)
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= taskScanInterval)
            {
                _scanTimer = 0f;
                AssignDefaultTasks();
            }
        }
    }

    private void RefreshBuildingReferences()
    {
        if (_sawmill   == null) _sawmill   = FindFirstObjectByType<SawmillBuilding>();
        if (_kiln      == null) _kiln      = FindFirstObjectByType<KilnBuilding>();
        if (_surfacing == null) _surfacing = FindFirstObjectByType<SurfacingBuilding>();
        if (_market    == null) _market    = FindFirstObjectByType<MarketBuilding>();
        if (_forest    == null) _forest    = FindFirstObjectByType<ForestZone>();
    }

    /// <summary>
    /// Finds the ForestZone that owns a particular tree or stump, used for
    /// tasks that need to call back into a zone. Falls back to the first
    /// ForestZone found if none matches directly.
    /// </summary>
    private ForestZone FindForestZoneFor(Vector3 worldPos)
    {
        // Search all zones and return the one whose region contains this position
        var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
        if (WorldGrid.Instance != null)
        {
            var cell = WorldGrid.Instance.WorldToGrid(worldPos);
            foreach (var z in allZones)
            {
                if (cell.x >= z.RegionStartX && cell.x < z.RegionStartX + z.RegionWidth &&
                    cell.y >= z.RegionStartY && cell.y < z.RegionStartY + z.RegionHeight)
                    return z;
            }
        }
        return _forest; // fallback
    }

    /// <summary>
    /// Returns the highest-value item in the given inventory zone without removing it.
    /// </summary>
    private LumberItem PeekHighestValue(InventoryManager inv, InventoryManager.InventoryZone zone)
    {
        var all = inv.GetItems(zone);
        if (all == null || all.Count == 0) return inv.Peek(zone);

        LumberItem best      = null;
        float      bestValue = -1f;
        foreach (var item in all)
        {
            float value = item.species != null ? item.species.valuePerBoardSurfaced : 0f;
            if (value > bestValue) { bestValue = value; best = item; }
        }
        return best ?? inv.Peek(zone);
    }

    public override void AssignDefaultTasks()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // ── Pending player request: plant a specific slot ──────────────
        // Stored by RequestPlantSlot() and consumed here when Sawyer is idle.
        if (_pendingPlantRequests.Count > 0)
        {
            var (slot, zone) = _pendingPlantRequests.Dequeue();

            if (slot != null && slot.IsAvailableForPlanting && zone.AssignedSpecies != null)
            {
                Vector3 slotPos = slot.transform.position;
                var task = new WorkerTask(TaskType.Planting, transform.position, slotPos, 0f)
                {
                    completionAction = (_) => 
                    { 
                        if (slot.IsStump)
                            StartCoroutine(RemoveStumpSlot(slot, zone));
                        else
                        {
                            zone.PlantInSlot(slot); 
                            _planting = false; 
                        }
                    },
                    description      = slot.IsStump 
                        ? $"[Player] Clear stump at {slotPos}" 
                        : $"[Player] Plant {zone.AssignedSpecies.speciesName}"
                };
                _planting = true;
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Executing player request: {task.description} ({_pendingPlantRequests.Count} remaining)");
                return;
            }
        }

        // ── Helper: highest value in stockpile ────────────────────────
        float highestStockpileValue = 0f;
        foreach (var item in inv.GetItems(InventoryManager.InventoryZone.ForestStockpile))
            if (item.species != null && item.species.valuePerBoardSurfaced > highestStockpileValue)
                highestStockpileValue = item.species.valuePerBoardSurfaced;

        // ── Priority 1: Plant high-value empty slots ───────────────────
        // Always do this first — no point having oak trees not planted.
        {
            var allZones   = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
            var candidates = new System.Collections.Generic.List<(ForestZone zone, TreeComponent slot, float value)>();
            foreach (var zone in allZones)
            {
                float value = zone.AssignedSpecies != null ? zone.AssignedSpecies.valuePerBoardSurfaced : 0f;
                // Plant high-value species immediately
                if (value <= valuePlantingThreshold) continue;
                
                TreeComponent emptySlot = zone.GetEmptySlot();
                if (emptySlot != null) candidates.Add((zone, emptySlot, value));
            }
            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => b.value.CompareTo(a.value));
                var (forestRef, slot, val) = candidates[0];
                Vector3 slotPos = slot.transform.position;
                var task = new WorkerTask(TaskType.Planting, transform.position, slotPos, 0f)
                {
                    completionAction = (_) => { forestRef.PlantInSlot(slot); _planting = false; },
                    description      = $"Plant {forestRef.AssignedSpecies?.speciesName} at {slotPos} (${val}/board)"
                };
                _planting = true;
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 2: Surfaced boards → Market (highest value first) ─
        if (_market != null && inv.HasItems(InventoryManager.InventoryZone.SurfacingOutput))
        {
            var board = PeekHighestValue(inv, InventoryManager.InventoryZone.SurfacingOutput);
            if (board != null)
            {
                var b = board; var mkt = _market; var srf = _surfacing;
                var task = new WorkerTask(TaskType.Transport, srf?.OutputPosition ?? transform.position, mkt.InputPosition, 0f)
                {
                    targetItem       = b,
                    pickupAction     = () => inv.RemoveItem(b, InventoryManager.InventoryZone.SurfacingOutput),
                    completionAction = (_) => inv.AddItem(b, InventoryManager.InventoryZone.MarketReady),
                    description      = $"Deliver {b.DisplayName} to market"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 1: Kiln-dried boards → Surfacing (highest value first)
        if (_surfacing != null && !_surfacing.IsFull && inv.HasItems(InventoryManager.InventoryZone.KilnOutput))
        {
            var board = PeekHighestValue(inv, InventoryManager.InventoryZone.KilnOutput);
            if (board != null)
            {
                var b = board; var kln = _kiln; var srf = _surfacing;
                var task = new WorkerTask(TaskType.Transport, GetBuildingBottomCenter(kln, transform.position), GetBuildingBottomCenter(srf, transform.position), 0f)
                {
                    targetItem       = b,
                    hideOnPickup     = true,
                    hideOnCompletion = true,
                    pickupAction     = () => inv.RemoveItem(b, InventoryManager.InventoryZone.KilnOutput),
                    completionAction = (_) => { srf.ProcessBoard(b); StartCoroutine(WaitAtSurfacing(srf)); },
                    description      = $"Carry dried {b.species?.speciesName} board to surfacing"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 2: Milled boards → Kiln (highest value first) ─────
        if (_kiln != null && !_kiln.IsFull && inv.HasItems(InventoryManager.InventoryZone.MillOutput))
        {
            var board = PeekHighestValue(inv, InventoryManager.InventoryZone.MillOutput);
            if (board != null)
            {
                var b = board; var mil = _sawmill; var kln = _kiln;
                var task = new WorkerTask(TaskType.Transport, GetBuildingBottomCenter(mil, transform.position), GetBuildingBottomCenter(kln, transform.position), 0f)
                {
                    targetItem       = b,
                    hideOnPickup     = true,
                    hideOnCompletion = true,
                    pickupAction     = () => inv.RemoveItem(b, InventoryManager.InventoryZone.MillOutput),
                    completionAction = (_) => kln.LoadBoard(b),
                    description      = $"Load {b.species?.speciesName} board into kiln"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 3: Logs → Sawmill (highest value first) ────────────
        // Skip if a higher-value tree is ready to harvest — cut that first,
        // then come back to mill everything in value order.
        if (_sawmill != null && !_sawmill.IsProcessing && inv.HasItems(InventoryManager.InventoryZone.ForestStockpile))
        {
            var log = PeekHighestValue(inv, InventoryManager.InventoryZone.ForestStockpile);
            if (log != null)
            {
                // Check if any ready tree is more valuable than the best log we have
                float bestLogValue = log.species != null ? log.species.valuePerBoardSurfaced : 0f;
                float bestReadyTreeValue = 0f;
                foreach (var zone in FindObjectsByType<ForestZone>(FindObjectsSortMode.None))
                {
                    var ready = zone.GetReadyTree();
                    if (ready != null && zone.AssignedSpecies != null)
                        bestReadyTreeValue = Mathf.Max(bestReadyTreeValue, zone.AssignedSpecies.valuePerBoardSurfaced);
                }

                // Only mill now if no more-valuable tree is waiting to be cut
                if (bestReadyTreeValue <= bestLogValue)
                {
                    var l = log; var mil = _sawmill;
                    // Find the zone that owns this log's species for visual update
                    var logZone = FindZoneForSpecies(l.species);
                    var task = new WorkerTask(TaskType.Transport, GetStockpilePositionForLog(log), GetBuildingBottomCenter(mil, transform.position), 0f)
                    {
                        targetItem       = l,
                        hideOnCompletion = true,
                        pickupAction     = () =>
                        {
                            inv.RemoveItem(l, InventoryManager.InventoryZone.ForestStockpile);
                            logZone?.UpdateLogPileVisual();
                        },
                        completionAction = (_) => { inv.AddItem(l, InventoryManager.InventoryZone.MillInput); StartCoroutine(WaitAtSawmill(mil)); },
                        description      = $"Carry {l.species?.speciesName} log to sawmill"
                    };
                    EnqueueTask(task);
                    Debug.Log($"[Sawyer] Task: {task.description}");
                    return;
                }
            }
        }

        // ── Priority 4: Harvest ready trees ─────────────────────────────
        // Only harvest a tree if no logs of equal or higher value are already
        // waiting to be milled. This prevents Sawyer from piling up oak logs
        // while unprocessed oak logs already sit in the stockpile.
        {
            var allZones   = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
            var candidates = new System.Collections.Generic.List<(ForestZone zone, TreeComponent tree, float value)>();
            foreach (var zone in allZones)
            {
                float value = zone.AssignedSpecies != null ? zone.AssignedSpecies.valuePerBoardSurfaced : 0f;
                TreeComponent readyTree = zone.GetReadyTree();
                if (readyTree != null) candidates.Add((zone, readyTree, value));
            }

            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => b.value.CompareTo(a.value));
                var (forestZone, tree, val) = candidates[0];

                // Don't harvest if logs of this value (or higher) already need milling
                bool pipelineHasEqualOrHigher = highestStockpileValue >= val;
                if (!pipelineHasEqualOrHigher)
                {
                    var task = new WorkerTask(TaskType.Harvest, transform.position, tree.transform.position, 0f)
                    {
                        asyncCompletionAction = (_) => ChopTree(tree, forestZone),
                        description      = $"Harvest {tree.Species?.speciesName} at {tree.transform.position}"
                    };
                    EnqueueTask(task);
                    Debug.Log($"[Sawyer] Task: {task.description}");
                    return;
                }
            }
        }

        // ── Priority 7: Fallback Planting (Low Value) ───────────────────
        // If Sawyer has entirely run out of milling, transporting, and high-value planting work,
        // he will finally go plant the low-value species rather than standing idle.
        {
            var allZones   = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
            var candidates = new System.Collections.Generic.List<(ForestZone zone, TreeComponent slot, float value)>();
            foreach (var zone in allZones)
            {
                // We don't filter by threshold here because this is the fallback
                float value = zone.AssignedSpecies != null ? zone.AssignedSpecies.valuePerBoardSurfaced : 0f;
                TreeComponent emptySlot = zone.GetEmptySlot();
                if (emptySlot != null) candidates.Add((zone, emptySlot, value));
            }
            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => b.value.CompareTo(a.value));
                var (forestRef, slot, val) = candidates[0];
                Vector3 slotPos = slot.transform.position;
                var task = new WorkerTask(TaskType.Planting, transform.position, slotPos, 0f)
                {
                    completionAction = (_) => { forestRef.PlantInSlot(slot); _planting = false; },
                    description      = $"Plant {forestRef.AssignedSpecies?.speciesName} at {slotPos} (${val}/board)"
                };
                _planting = true;
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description} (Fallback)");
                return;
            }
        }

        // ── Priority 8 (Auto-clearing stumps) is intentionally removed ───
        // The player must now manually supervise stump clearing by clicking them.
    }

    // ── Coroutines ────────────────────────────────────────────────────

    // Safety: resets all blocking flags — call if the task queue is force-cleared
    private void ResetBlockingFlags()
    {
        _waitingAtSawmill   = false;
        _waitingAtSurfacing = false;
        _harvesting         = false;
        _clearingStump      = false;
        _planting           = false;
    }

    private IEnumerator WaitAtSawmill(SawmillBuilding sawmill)
    {
        _waitingAtSawmill = true;
        IsStationary      = true;
        if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        
        Debug.Log("[Sawyer] Running the sawmill...");
        
        float textTimer = 0f;

        while (sawmill != null && sawmill.IsProcessing)
        {
            if (WorldTextManager.Instance != null && WorldTextManager.Instance.showSawingText && textTimer <= 0f)
            {
                WorldTextManager.Instance.SpawnFollowingText(transform, WorldTextManager.Instance.sawingText, WorldTextManager.Instance.sawingColor);
                textTimer = 2.5f; // Pop text every 2.5 seconds roughly
            }
            
            if (textTimer > 0f) textTimer -= 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        IsStationary      = false;
        _waitingAtSawmill = false;
        _scanTimer        = taskScanInterval;

        Debug.Log("[Sawyer] Milling done — resuming tasks.");
    }

    private IEnumerator WaitAtSurfacing(SurfacingBuilding surfacing)
    {
        _waitingAtSurfacing = true;
        IsStationary        = true;
        if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        
        Debug.Log("[Sawyer] Working at surfacing...");
        
        float textTimer = 0f;

        while (surfacing != null && surfacing.IsProcessing)
        {
            if (WorldTextManager.Instance != null && WorldTextManager.Instance.showSurfacingText && textTimer <= 0f)
            {
                WorldTextManager.Instance.SpawnFollowingText(transform, WorldTextManager.Instance.surfacingText, WorldTextManager.Instance.surfacingColor);
                textTimer = 2.5f;
            }
            
            if (textTimer > 0f) textTimer -= 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        IsStationary        = false;
        _waitingAtSurfacing = false;
        _scanTimer          = taskScanInterval;

        Debug.Log("[Sawyer] Surfacing done — resuming tasks.");
    }

    private IEnumerator ChopTree(TreeComponent tree, ForestZone forest)
    {
        _harvesting  = true;
        IsStationary = true;
        Debug.Log("[Sawyer] Chopping tree...");
        if (WorldTextManager.Instance != null && WorldTextManager.Instance.showChoppingText)
            WorldTextManager.Instance.SpawnFollowingText(transform, WorldTextManager.Instance.choppingText, WorldTextManager.Instance.choppingColor);

        if (_spriteRenderer != null)
            _spriteRenderer.flipX = tree.transform.position.x < transform.position.x;

        yield return new WaitForSeconds(10f);

        forest.HarvestTree(tree);

        IsStationary = false;
        _harvesting  = false;
        _scanTimer   = taskScanInterval;

        Debug.Log("[Sawyer] Tree chopped — resuming tasks.");
    }

    private IEnumerator RemoveStumpSlot(TreeComponent slot, ForestZone zone)
    {
        _clearingStump = true;
        IsStationary   = true;
        Debug.Log($"[Sawyer] Clearing stump at {slot.transform.position}...");
        
        if (WorldTextManager.Instance != null && WorldTextManager.Instance.showClearingStumpText)
            WorldTextManager.Instance.SpawnFollowingText(transform, WorldTextManager.Instance.clearingStumpText, WorldTextManager.Instance.clearingStumpColor);

        // Turn to face the stump
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = slot.transform.position.x < transform.position.x;

        // Brief work animation to physically clear the stump
        yield return new WaitForSeconds(stumpClearDuration);

        // Remove the stump slot entirely so it's ready for building
        if (slot != null && zone != null)
            zone.RemoveTreeSlot(slot);

        IsStationary   = false;
        _clearingStump = false;
        _planting      = false;
        _scanTimer     = taskScanInterval;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private ForestZone FindZoneForSpecies(WoodSpeciesData species)
    {
        if (species == null) return null;
        foreach (var z in FindObjectsByType<ForestZone>(FindObjectsSortMode.None))
            if (z.AssignedSpecies == species) return z;
        return null;
    }

    private Vector3 GetForestStockpilePosition()
    {
        return _forest != null
            ? _forest.ForestCenter
            : new Vector3(-300f, transform.position.y, 0f);
    }

    private Vector3 GetStockpilePositionForLog(LumberItem log)
    {
        if (log?.species == null) return GetForestStockpilePosition();

        // Find the zone whose assigned species matches this log's species
        var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
        foreach (var z in allZones)
            if (z.AssignedSpecies == log.species) return z.ForestCenter;

        // Fallback: nearest zone
        ForestZone nearest = null;
        float nearestDist  = float.MaxValue;
        foreach (var z in allZones)
        {
            float d = Vector3.Distance(transform.position, z.ForestCenter);
            if (d < nearestDist) { nearestDist = d; nearest = z; }
        }
        return nearest != null ? nearest.ForestCenter : GetForestStockpilePosition();
    }
}
