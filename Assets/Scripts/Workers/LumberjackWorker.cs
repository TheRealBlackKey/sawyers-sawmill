using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sawmill.Core;

namespace Sawmill.Production
{
    /// <summary>
    /// AI Logic for the Lumberjack helper.
    /// Operates out of the Lumberjack Quarters.
    /// Priority 1 (Stockpile -> Sawmill): If logs exist in the Stockpile, carry them to the Sawmill.
    /// Priority 2 (Harvest): If no logs exist, find the nearest mature tree within the Quarters' radius and chop it.
    /// </summary>
    public class LumberjackWorker : WorkerBase
    {
        public LumberjackBuilding HomeBuilding;

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
        }

        protected override void Update()
        {
            base.Update();

            if (_harvesting) return;

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

        public override void AssignDefaultTasks()
        {
            if (HomeBuilding == null)
            {
                Debug.LogWarning("[Lumberjack] No home building assigned. Idle.");
                return;
            }

            var inv = InventoryManager.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[Lumberjack] No inventory manager found. Idle.");
                return;
            }

            // ── Priority 1: Logs → Sawmill ────────────────────────────
            if (_sawmill == null) _sawmill = FindFirstObjectByType<SawmillBuilding>();
            
            if (_sawmill != null)
            {
                var allLogs = inv.GetItems(InventoryManager.InventoryZone.ForestStockpile);
                LumberItem targetLog = null;
                ForestZone targetZone = null;

                Vector3 homeWorldPos = HomeBuilding.transform.position;
                Vector2Int homeGridPos = WorldGrid.Instance.WorldToGrid(homeWorldPos);
                int radius = HomeBuilding.EffectRadius;

                var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);

                foreach (var log in allLogs)
                {
                    foreach (var zone in allZones)
                    {
                        if (zone.AssignedSpecies == log.species)
                        {
                            // The zone represents a painted rectangle of trees.
                            // We check if the Lumberjack's operating area overlaps this rectangle AT ALL.
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
                        description      = $"[Lumberjack] Carry log to sawmill"
                    };
                    EnqueueTask(task);
                    Debug.Log($"[Lumberjack] Assigned Task: {task.description}");
                    return;
                }
                else
                {
                    Debug.Log("[Lumberjack] No logs within radius to deliver.");
                }
            }
            else
            {
                Debug.LogWarning("[Lumberjack] No sawmill building found on map.");
            }

            // ── Priority 2: Harvest Ready Trees within Radius ───────────────
            {
                var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
                TreeComponent bestTarget = null;
                float shortestDistance = float.MaxValue;
                ForestZone bestZone = null;

                Vector3 homeWorldPos = HomeBuilding.transform.position;
                Vector2Int homeGridPos = WorldGrid.Instance.WorldToGrid(homeWorldPos);
                int radius = HomeBuilding.EffectRadius;

                foreach (var zone in allZones)
                {
                    // Scan all populated slots in the zone instead of just getting the first ready one
                    // because we must ensure it's within our specific radius.
                    foreach (var tree in zone.GetAllMatureTrees())
                    {
                        if (tree == null || !tree.IsReadyToHarvest) continue;

                        Vector2Int treeGridPos = WorldGrid.Instance.WorldToGrid(tree.transform.position);

                        // Check if within bounds
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
                        description      = $"[Lumberjack] Harvest tree at {tree.transform.position}"
                    };
                    EnqueueTask(task);
                    Debug.Log($"[Lumberjack] Assigned Task: {task.description}");
                    return;
                }
                else
                {
                    Debug.Log($"[Lumberjack] Idling. No mature trees found within {radius} cells of quarters.");
                }
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
