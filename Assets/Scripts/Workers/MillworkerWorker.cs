using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sawmill.Core;
using Sawmill.UI;

namespace Sawmill.Production
{
    /// <summary>
    /// AI Logic for the dedicated Millworker.
    /// Operates out of the Millworker Quarters.
    /// Exclusively responsible for moving logs from the forest to the sawmill.
    /// </summary>
    public class MillworkerWorker : WorkerBase
    {
        public MillworkerBuilding HomeBuilding;

        [Header("Millworker Stats")]
        [SerializeField] private float taskScanInterval = 1f;

        private SawmillBuilding _sawmill;
        private float _scanTimer;

        protected override void Start()
        {
            base.Start();
            _sawmill = FindFirstObjectByType<SawmillBuilding>();

            // Distinctive color for the Millworker
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(1f, 0.9f, 0.6f); // Light Yellow/Orange
            }
        }

        protected override void Update()
        {
            base.Update();

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
                Debug.LogWarning("[Millworker] No home building assigned. Idle.");
                return;
            }

            // ── Energy & Refueling Check ────────────────────────────────────
            if (CurrentEnergy < maxEnergy * 0.3f)
            {
                var gm = GameManager.Instance;
                var messHall = FindFirstObjectByType<MessHallBuilding>();
                
                if (messHall != null && gm != null && gm.Sawdust >= 1f)
                {
                    Debug.Log("[Millworker] Energy is low. Going to Mess Hall to refuel.");
                    var refuelTask = new WorkerTask(TaskType.Move, transform.position, GetBuildingBottomCenter(messHall, transform.position), 0f)
                    {
                        asyncCompletionAction = (_) => messHall.HandleRefuel(this),
                        description = $"[Millworker] Traveling to Mess Hall to refuel."
                    };
                    EnqueueTask(refuelTask);
                    return; 
                }
                
                if (CurrentEnergy <= 0f)
                {
                    Debug.Log("[Millworker] Energy is ZERO and cannot refuel. Completely Exhausted.");
                    return; // Exhausted and cannot refuel
                }
            }

            var inv = InventoryManager.Instance;
            if (inv == null) return;

            // 1. Prioritize taking processed boards from the Sawmill to the Kiln
            if (AssignKilnTask(inv))
            {
                return; // Task assigned, stop looking
            }

            // 2. Fall back to milling logs already sitting at the Sawmill Input
            AssignMillingTask(inv);
        }

        private bool AssignKilnTask(InventoryManager inv)
        {
            if (_sawmill == null) _sawmill = FindFirstObjectByType<SawmillBuilding>();
            if (_sawmill == null) return false;

            var kiln = FindFirstObjectByType<KilnBuilding>();
            if (kiln == null || kiln.IsFull)
            {
                Debug.Log("[Millworker] Kiln is missing or full. Skipping Kiln task.");
                return false;
            }

            // Check if there are rough-sawn boards waiting at the sawmill output
            var boards = inv.GetItems(InventoryManager.InventoryZone.MillOutput);
            LumberItem targetBoard = null;

            foreach (var b in boards)
            {
                if (!b.IsClaimed && b.stage == ProcessingStage.RoughSawn)
                {
                    targetBoard = b;
                    break;
                }
            }

            if (targetBoard != null)
            {
                targetBoard.IsClaimed = true;
                Debug.Log($"[Millworker] Found a board ({targetBoard.DisplayName}) at the Sawmill. Taking it to the Kiln.");

                var task = new WorkerTask(TaskType.Transport, GetBuildingBottomCenter(_sawmill, transform.position), GetBuildingBottomCenter(kiln, transform.position), 0f)
                {
                    targetItem       = targetBoard,
                    hideOnCompletion = true,
                    pickupAction     = () => 
                    {
                        Debug.Log($"[Millworker] Picked up board from Sawmill Output.");
                        inv.RemoveItem(targetBoard, InventoryManager.InventoryZone.MillOutput);
                    },
                    completionAction = (_) => 
                    { 
                        Debug.Log($"[Millworker] Delivered board to Kiln.");
                        targetBoard.IsClaimed = false; // Free the claim so the kiln can take true ownership
                        kiln.LoadBoard(targetBoard); 
                    },
                    abortAction      = (_) => 
                    { 
                        Debug.LogWarning($"[Millworker] Board transport aborted.");
                        targetBoard.IsClaimed = false; 
                    },
                    description      = $"[Millworker] Carry {targetBoard.DisplayName} to kiln"
                };
                EnqueueTask(task);
                return true;
            }

            return false;
        }

        private void AssignMillingTask(InventoryManager inv)
        {
            if (_sawmill == null) _sawmill = FindFirstObjectByType<SawmillBuilding>();
            if (_sawmill == null)
            {
                Debug.Log("[Millworker] No sawmill found. Cannot assign milling task.");
                return;
            }
            
            if (_sawmill.IsProcessing)
            {
                Debug.Log("[Millworker] Sawmill is currently busy processing. Waiting.");
                return;
            }

            // Only look for logs that are ALREADY at the Sawmill Input
            var allMillLogs = inv.GetItems(InventoryManager.InventoryZone.MillInput);
            LumberItem targetLog = null;

            foreach (var log in allMillLogs)
            {
                if (!log.IsClaimed)
                {
                    targetLog = log;
                    break;
                }
            }

            if (targetLog != null)
            {
                targetLog.IsClaimed = true; 
                var mil = _sawmill;
                Debug.Log($"[Millworker] Found {targetLog.species.speciesName} log at Sawmill Input. Moving to process it.");
                
                // Move from wherever we are, to the sawmill, and start milling
                var task = new WorkerTask(TaskType.Move, transform.position, GetBuildingBottomCenter(mil, transform.position), 0f)
                {
                    completionAction = (_) => 
                    { 
                        Debug.Log($"[Millworker] Arrived at sawmill. Starting milling sequence.");
                        targetLog.IsClaimed = false; 
                        
                        // We are about to process it, so remove it from the input queue!
                        inv.RemoveItem(targetLog, InventoryManager.InventoryZone.MillInput);
                        
                        // Trigger the sawmill building actually cutting the wood
                        mil.StartCoroutine(mil.ProcessLog(targetLog));
                        
                        // Start the visual wait
                        StartCoroutine(WaitAtSawmill(mil)); 
                    },
                    abortAction      = (_) => 
                    { 
                        Debug.LogWarning($"[Millworker] Walk to sawmill aborted.");
                        targetLog.IsClaimed = false; 
                    },
                    description      = $"[Millworker] Processing {targetLog.species.speciesName} log"
                };
                EnqueueTask(task);
            }
            else
            {
                Debug.Log("[Millworker] No unprocessed logs found at the Sawmill Input. Waiting for Haulers to deliver logs.");
            }
        }

        private IEnumerator WaitAtSawmill(SawmillBuilding sawmill)
        {
            Debug.Log("[Millworker] Waiting at sawmill while processing...");
            IsStationary = true;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            
            float textTimer = 0f;
            float duration = 20f * (sawmill != null ? sawmill.millingSpeedMultiplier : 1f); 
            float elapsed = 0f;

            while (sawmill != null && elapsed < duration)
            {
                if (WorldTextManager.Instance != null && WorldTextManager.Instance.showSawingText && textTimer <= 0f)
                {
                    WorldTextManager.Instance.SpawnFollowingText(transform, WorldTextManager.Instance.sawingText, WorldTextManager.Instance.sawingColor);
                    textTimer = 2.5f;
                }
                
                if (textTimer > 0f) textTimer -= 0.25f;
                elapsed += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            Debug.Log("[Millworker] Finished waiting at sawmill. Board should be produced.");
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            IsStationary = false;
            _scanTimer = taskScanInterval;
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

            if (radius <= 0) radius = 2; // Provide a default tiny wander radius if 0

            int rx = UnityEngine.Random.Range(homeGrid.x - radius, homeGrid.x + radius + 1);
            int ry = UnityEngine.Random.Range(homeGrid.y - radius, homeGrid.y + radius + 1);

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
