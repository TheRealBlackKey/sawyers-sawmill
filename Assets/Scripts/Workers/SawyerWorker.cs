using UnityEngine;

/// <summary>
/// Sawyer - the main character. Handles all physical transport and delivery.
///
/// KEY DESIGN: Sawyer physically carries items between buildings.
/// - pickupAction fires when he ARRIVES at sourcePosition (he picks up the item)
/// - completionAction fires when he ARRIVES at destinationPosition (he drops it off)
///
/// For Kiln:      completionAction calls kiln.LoadBoard()        — boards enter drying slots
/// For Surfacing: completionAction calls surfacing.ProcessBoard() — grain reveal starts
/// For Market:    completionAction adds board to MarketReady      — market auto-sells
/// For Sawmill:   completionAction adds log to MillInput          — milling begins
/// </summary>
public class SawyerWorker : WorkerBase
{
    [Header("Sawyer Settings")]
    [SerializeField] private float taskScanInterval       = 1f;
    [SerializeField] private float buildingRescanInterval = 3f;
    private float _scanTimer;
    private float _rescanTimer;

    private SawmillBuilding   _sawmill;
    private KilnBuilding      _kiln;
    private SurfacingBuilding _surfacing;
    private MarketBuilding    _market;

    public void SetBuildingReferences(SawmillBuilding mill, KilnBuilding kiln,
        SurfacingBuilding surf, MarketBuilding market)
    {
        _sawmill   = mill;
        _kiln      = kiln;
        _surfacing = surf;
        _market    = market;
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
    }

    /// <summary>
    /// Priority order (downstream-first prevents pipeline jams):
    ///   1. Surfaced boards  -> Market       (pickup from SurfacingOutput, drop to MarketReady)
    ///   2. Kiln-dried boards -> Surfacing   (pickup from KilnOutput, calls ProcessBoard on arrival)
    ///   3. Milled boards    -> Kiln         (pickup from MillOutput, calls LoadBoard on arrival)
    ///   4. Logs             -> Sawmill      (pickup from ForestStockpile, drop to MillInput)
    /// </summary>
    public override void AssignDefaultTasks()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // ── Priority 1: Surfaced boards -> Market ──────────────────────
        if (_market != null && inv.HasItems(InventoryManager.InventoryZone.SurfacingOutput))
        {
            var board = inv.Peek(InventoryManager.InventoryZone.SurfacingOutput);
            if (board != null)
            {
                var b   = board;
                var mkt = _market;
                var srf = _surfacing;

                var task = new WorkerTask(
                    TaskType.Transport,
                    srf?.OutputPosition ?? transform.position,
                    mkt.InputPosition,
                    0f)
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

        // ── Priority 2: Kiln-dried boards -> Surfacing ─────────────────
        if (_surfacing != null && !_surfacing.IsFull &&
            inv.HasItems(InventoryManager.InventoryZone.KilnOutput))
        {
            var board = inv.Peek(InventoryManager.InventoryZone.KilnOutput);
            if (board != null)
            {
                var b   = board;
                var kln = _kiln;
                var srf = _surfacing;

                var task = new WorkerTask(
                    TaskType.Transport,
                    kln?.OutputPosition ?? transform.position,
                    srf.InputPosition,
                    0f)
                {
                    targetItem       = b,
                    pickupAction     = () => inv.RemoveItem(b, InventoryManager.InventoryZone.KilnOutput),
                    completionAction = (_) => srf.ProcessBoard(b),
                    description      = "Carry dried board to surfacing"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 3: Milled boards -> Kiln ─────────────────────────
        // IsFull checks _dryingSlots.Count — only false when kiln has physical room
        if (_kiln != null && !_kiln.IsFull &&
            inv.HasItems(InventoryManager.InventoryZone.MillOutput))
        {
            var board = inv.Peek(InventoryManager.InventoryZone.MillOutput);
            if (board != null)
            {
                var b   = board;
                var mil = _sawmill;
                var kln = _kiln;

                var task = new WorkerTask(
                    TaskType.Transport,
                    mil?.OutputPosition ?? transform.position,
                    kln.InputPosition,
                    0f)
                {
                    targetItem       = b,
                    pickupAction     = () => inv.RemoveItem(b, InventoryManager.InventoryZone.MillOutput),
                    completionAction = (_) => kln.LoadBoard(b),
                    description      = "Load board into kiln"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }

        // ── Priority 4: Logs -> Sawmill ────────────────────────────────
        if (_sawmill != null && !_sawmill.IsProcessing &&
            inv.HasItems(InventoryManager.InventoryZone.ForestStockpile))
        {
            var log = inv.Peek(InventoryManager.InventoryZone.ForestStockpile);
            if (log != null)
            {
                var l   = log;
                var mil = _sawmill;

                var task = new WorkerTask(
                    TaskType.Transport,
                    GetForestStockpilePosition(),
                    mil.InputPosition,
                    0f)
                {
                    targetItem       = l,
                    pickupAction     = () => inv.RemoveItem(l, InventoryManager.InventoryZone.ForestStockpile),
                    completionAction = (_) => inv.AddItem(l, InventoryManager.InventoryZone.MillInput),
                    description      = $"Carry {l.species?.speciesName} log to sawmill"
                };
                EnqueueTask(task);
                Debug.Log($"[Sawyer] Task: {task.description}");
                return;
            }
        }
    }

    private Vector3 GetForestStockpilePosition()
    {
        var forest = FindFirstObjectByType<ForestZone>();
        return forest != null ? forest.transform.position : new Vector3(-300f, transform.position.y, 0f);
    }
}
