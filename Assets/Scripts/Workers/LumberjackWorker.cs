using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sawmill.Core;

namespace Sawmill.Production
{
    public enum WorkerRole
    {
        Feller,
        Hauler,
        Forester
    }

    /// <summary>
    /// AI Logic for the Lumberjack helper.
    /// Operates out of the Lumberjack Quarters.
    /// Can be specialized as a Feller (chops only), Hauler (moves logs only), or Forester (plants only).
    /// </summary>
    public class LumberjackWorker : WorkerBase
    {
        public LumberjackBuilding HomeBuilding;

        [Header("Specialization Role")]
        public WorkerRole CurrentRole = WorkerRole.Feller;

        [Header("Lumberjack Stats")]
        [SerializeField] private float taskScanInterval = 1f;

        private SawmillBuilding _sawmill;
        private float _scanTimer;

        // State trackers
        private bool _harvesting = false;

        protected override void Start()
        {
            base.Start();
            _sawmill = FindFirstObjectByType<SawmillBuilding>();

            // Temporarily assign a color based on role for testing/clarity
            SetRoleColor();
        }

        private void SetRoleColor()
        {
            if (_spriteRenderer != null)
            {
                switch (CurrentRole)
                {
                    case WorkerRole.Feller:
                        _spriteRenderer.color = new Color(1f, 0.7f, 0.7f); // Light Red
                        break;
                    case WorkerRole.Hauler:
                        _spriteRenderer.color = new Color(0.7f, 0.7f, 1f); // Light Blue
                        break;
                    case WorkerRole.Forester:
                        _spriteRenderer.color = new Color(0.7f, 1f, 0.7f); // Light Green
                        break;
                }
            }
        }

        public void SetRole(WorkerRole newRole)
        {
            CurrentRole = newRole;
            SetRoleColor();
            _taskQueue.Clear(); // Cancel current pending tasks
            CurrentTask = null; // Re-evaluate immediately
            AssignDefaultTasks();
        }

        protected override void Update()
        {
            base.Update();

            if (_harvesting) return;

            if (CurrentState == WorkerState.Idle && _taskQueue.Count == 0 && CurrentTask == null)
            {
                _scanTimer += Time.deltaTime;
                if (_scanTimer >= taskScanInterval)
                {
                    _scanTimer = 0f;
                    AssignDefaultTasks();
                }
            }
        }

        public override void AssignDefaultTasks()
        {
            if (HomeBuilding == null)
            {
                Debug.LogWarning("[Lumberjack] No home building assigned. Idle.");
                return;
            }

            // ── Energy & Refueling Check ────────────────────────────────────
            if (CurrentEnergy < maxEnergy * 0.3f)
            {
                var gm = GameManager.Instance;
                var messHall = FindFirstObjectByType<MessHallBuilding>();
                
                // Only queue the refuel task if we actually have enough sawdust for at least one tick
                if (messHall != null && gm != null && gm.Sawdust >= 1f)
                {
                    var task = new WorkerTask(TaskType.Move, transform.position, GetBuildingBottomCenter(messHall, transform.position), 0f)
                    {
                        asyncCompletionAction = (_) => messHall.HandleRefuel(this),
                        description = $"[Lumberjack-{CurrentRole}] Traveling to Mess Hall to refuel."
                    };
                    EnqueueTask(task);
                    Debug.Log(task.description);
                    return; // Stop processing normal tasks until refueled
                }
                
                // If we have no sawdust (or no mess hall), and we are completely exhausted, 
                // don't bother trying to assign work tasks. Just idle and wait.
                if (CurrentEnergy <= 0f)
                {
                    return;
                }
                
                // Otherwise (energy is between 0 and 30%), we keep working until we drop!
            }

            var inv = InventoryManager.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[Lumberjack] No inventory manager found. Idle.");
                return;
            }

            Vector3 homeWorldPos = HomeBuilding.transform.position;
            Vector2Int homeGridPos = WorldGrid.Instance.WorldToGrid(homeWorldPos);
            int radius = HomeBuilding.EffectRadius;
            var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);

            switch (CurrentRole)
            {
                case WorkerRole.Hauler:
                    AssignHaulerTask(inv, homeGridPos, radius, allZones);
                    break;
                case WorkerRole.Feller:
                    AssignFellerTask(homeGridPos, radius, allZones);
                    break;
                case WorkerRole.Forester:
                    AssignForesterTask(homeGridPos, radius, allZones);
                    break;
            }
        }

        private void AssignHaulerTask(InventoryManager inv, Vector2Int homeGridPos, int radius, ForestZone[] allZones)
        {
            if (_sawmill == null) _sawmill = FindFirstObjectByType<SawmillBuilding>();
            if (_sawmill == null) return;

            var allLogs = inv.GetItems(InventoryManager.InventoryZone.ForestStockpile);
            LumberItem targetLog = null;
            ForestZone targetZone = null;

            foreach (var log in allLogs)
            {
                foreach (var zone in allZones)
                {
                    if (zone.AssignedSpecies == log.species)
                    {
                        RectInt zoneRect = new RectInt(zone.RegionStartX, zone.RegionStartY, zone.RegionWidth, zone.RegionHeight);
                        RectInt lumberjackRect = new RectInt(homeGridPos.x - radius, homeGridPos.y - radius, radius * 2, radius * 2);

                        if (lumberjackRect.Overlaps(zoneRect))
                        {
                            targetLog = log;
                            targetZone = zone;
                            break;
                        }
                    }
                }
                if (targetLog != null) break;
            }

            if (targetLog != null)
            {
                var l = targetLog; var mil = _sawmill;
                var task = new WorkerTask(TaskType.Transport, targetZone.ForestCenter, GetBuildingBottomCenter(mil, transform.position), 0f)
                {
                    targetItem       = l,
                    hideOnCompletion = true,
                    pickupAction     = () => 
                    {
                        inv.RemoveItem(l, InventoryManager.InventoryZone.ForestStockpile);
                        targetZone?.UpdateLogPileVisual();
                    },
                    completionAction = (_) => { inv.AddItem(l, InventoryManager.InventoryZone.MillInput); },
                    description      = $"[Lumberjack-Hauler] Carry log to sawmill"
                };
                EnqueueTask(task);
                Debug.Log(task.description);
            }
        }

        private void AssignFellerTask(Vector2Int homeGridPos, int radius, ForestZone[] allZones)
        {
            TreeComponent bestTarget = null;
            float shortestDistance = float.MaxValue;
            ForestZone bestZone = null;

            foreach (var zone in allZones)
            {
                foreach (var tree in zone.GetAllMatureTrees())
                {
                    if (tree == null || !tree.IsReadyToHarvest) continue;

                    Vector2Int treeGridPos = WorldGrid.Instance.WorldToGrid(tree.transform.position);

                    if (Mathf.Abs(treeGridPos.x - homeGridPos.x) <= radius && 
                        Mathf.Abs(treeGridPos.y - homeGridPos.y) <= radius)
                    {
                        float dist = Vector3.Distance(transform.position, tree.transform.position);
                        if (dist < shortestDistance)
                        {
                            shortestDistance = dist;
                            bestTarget = tree;
                            bestZone = zone;
                        }
                    }
                }
            }

            if (bestTarget != null && bestZone != null)
            {
                var tree = bestTarget;
                var task = new WorkerTask(TaskType.Harvest, transform.position, tree.transform.position, 0f)
                {
                    asyncCompletionAction = (_) => ChopTree(tree, bestZone),
                    description      = $"[Lumberjack-Feller] Harvest tree at {tree.transform.position}"
                };
                EnqueueTask(task);
                Debug.Log(task.description);
            }
        }

        private void AssignForesterTask(Vector2Int homeGridPos, int radius, ForestZone[] allZones)
        {
            TreeComponent bestSlot = null;
            float shortestDistance = float.MaxValue;
            ForestZone bestZone = null;

            foreach (var zone in allZones)
            {
                // We must check if the slot is within radius
                // Unfortunately GetEmptySlot() returns the first one it finds.
                // We'll iterate all children of the zone to find an empty slot within radius.
                TreeComponent[] allSlots = zone.GetComponentsInChildren<TreeComponent>();
                foreach (var slot in allSlots)
                {
                    if (slot.State == TreeState.Empty || slot.State == TreeState.Stump)
                    {
                        Vector2Int slotGridPos = WorldGrid.Instance.WorldToGrid(slot.transform.position);
                        if (Mathf.Abs(slotGridPos.x - homeGridPos.x) <= radius && 
                            Mathf.Abs(slotGridPos.y - homeGridPos.y) <= radius)
                        {
                            float dist = Vector3.Distance(transform.position, slot.transform.position);
                            if (dist < shortestDistance)
                            {
                                shortestDistance = dist;
                                bestSlot = slot;
                                bestZone = zone;
                            }
                        }
                    }
                }
            }

            if (bestSlot != null && bestZone != null)
            {
                var slot = bestSlot;
                var z = bestZone;
                Vector3 slotPos = slot.transform.position;
                var task = new WorkerTask(TaskType.Planting, transform.position, slotPos, 0f)
                {
                    completionAction = (_) => { z.PlantInSlot(slot); },
                    description      = $"[Lumberjack-Forester] Plant sapling at {slotPos}"
                };
                EnqueueTask(task);
                Debug.Log(task.description);
            }
        }

        // WaitAtSawmill removed — the Lumberjack doesn't wait for logs to process.

        private IEnumerator ChopTree(TreeComponent tree, ForestZone forest)
        {
            _harvesting  = true;
            IsStationary = true;

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = tree.transform.position.x < transform.position.x;

            yield return new WaitForSeconds(10f);

            forest.HarvestTree(tree);

            IsStationary = false;
            _harvesting  = false;
            _scanTimer   = taskScanInterval;
        }

        private ForestZone FindZoneForSpecies(WoodSpeciesData species)
        {
            var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
            foreach (var z in allZones)
            {
                if (z.AssignedSpecies == species) return z;
            }
            return allZones.Length > 0 ? allZones[0] : null;
        }

        protected Vector3 GetStockpilePositionForLog(LumberItem log)
        {
            var zone = FindZoneForSpecies(log.species);
            if (zone != null)
            {
                // Return the center of the region
                float cx = zone.RegionStartX + (zone.RegionWidth / 2f);
                float cy = zone.RegionStartY + (zone.RegionHeight / 2f);
                return WorldGrid.Instance.GridToWorld(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), 0f);
            }
            if (HomeBuilding != null) return HomeBuilding.transform.position;
            return transform.position; 
        }

        protected override IEnumerator DoIdleWander()
        {
            if (IsStationary || HomeBuilding == null || WorldGrid.Instance == null)
            {
                yield return base.DoIdleWander();
                yield break;
            }

            Vector3 homePos = HomeBuilding.transform.position;
            Vector2Int homeGrid = WorldGrid.Instance.WorldToGrid(homePos);
            int radius = HomeBuilding.EffectRadius;

            int rx = UnityEngine.Random.Range(homeGrid.x - radius, homeGrid.x + radius + 1);
            int ry = UnityEngine.Random.Range(homeGrid.y - radius, homeGrid.y + radius + 1);

            // Clamp to map boundaries
            rx = Mathf.Clamp(rx, 0, WorldGrid.Instance.Columns - 1);
            ry = Mathf.Clamp(ry, 0, WorldGrid.Instance.Rows - 1);

            Vector3 wanderTarget = WorldGrid.Instance.GridToWorld(rx, ry, transform.position.z);

            List<Vector3> path = Sawmill.Core.Pathfinding.GridPathfinder.FindPath(transform.position, wanderTarget, allowDiagonalMovement);
            if (path == null || path.Count == 0) yield break;

            _isMoving = true;
            UpdateSpriteState(null, WorkerState.Walking);

            float elapsed = 0f;
            int waypointIndex = 0;
            _targetPosition = path[waypointIndex];

            while (elapsed < 1.5f && _taskQueue.Count == 0 && waypointIndex < path.Count)
            {
                if (_spriteRenderer != null && Mathf.Abs(_targetPosition.x - transform.position.x) > 0.5f)
                {
                    _spriteRenderer.flipX = _targetPosition.x < transform.position.x;
                }

                if (Vector3.Distance(transform.position, _targetPosition) <= 0.5f)
                {
                    waypointIndex++;
                    if (waypointIndex < path.Count)
                    {
                        _targetPosition = path[waypointIndex];
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _isMoving = false;
            UpdateSpriteState(null, WorkerState.Idle);
        }
    }
}
