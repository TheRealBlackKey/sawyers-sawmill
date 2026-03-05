using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sawmill.Core.Pathfinding;

/// <summary>
/// Base class for all worker characters. Handles movement, task execution,
/// and the idle wandering that makes the game feel alive.
/// </summary>
[System.Serializable]
public struct WorkerSpriteData
{
    public Sprite idleSprite;
    public Sprite walkingSprite;
    public Sprite choppingSprite;
    public Sprite carryingLogSprite;
    public Sprite carryingBoardSprite;
}

[RequireComponent(typeof(SpriteRenderer))]
public abstract class WorkerBase : MonoBehaviour
{
    [Header("Sprites")]
    public WorkerSpriteData sprites;

    [Header("Worker Identity")]
    public string workerName = "Sawyer";
    [TextArea(1, 2)]
    public string bio = "A retired master woodworker.";
    public Sprite portrait;

    [Header("Movement & Timing")]
    public float moveSpeed = 60f;
    public float idleWanderRadius = 40f;
    public float idleWanderInterval = 4f;
    [Tooltip("If checked, Sawyer can walk diagonally. If unchecked, he moves like a rook in chess.")]
    public bool allowDiagonalMovement = false;

    [Tooltip("How many seconds it takes to pick up or put down an item. Applies universally to all tasks.")]
    [SerializeField] protected float baseActionDuration = 5f;

    [Header("Specialization (0-1 bonus multipliers)")]
    public float millingSpeedBonus    = 0f;
    public float kilnSpeedBonus       = 0f;
    public float surfacingSpeedBonus  = 0f;
    public float rareGrainChanceBonus = 0f;

    // ── State ─────────────────────────────────────────────────────────
    public WorkerState CurrentState { get; protected set; } = WorkerState.Idle;
    public WorkerTask  CurrentTask  { get; protected set; }

    protected Queue<WorkerTask> _taskQueue = new Queue<WorkerTask>();
    protected SpriteRenderer    _spriteRenderer;
    protected Vector3           _targetPosition;
    protected bool              _isMoving = false;

    private float _idleTimer = 0f;

    /// <summary>
    /// When true, suppresses idle wandering. Set by subclasses when the worker
    /// must remain stationary (e.g. Sawyer surfacing a board himself).
    /// </summary>
    protected bool IsStationary = false;

    protected virtual void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // Workers always render in front of everything including trees.
        // Trees use Y-based sorting (Y * 10), so we use the max value to guarantee Sawyer is always on top.
        if (_spriteRenderer != null)
            _spriteRenderer.sortingOrder = 32000;
    }

    protected virtual void Start()
    {
        StartCoroutine(TaskLoop());
    }

    protected virtual void Update()
    {
        HandleMovement();
    }

    // ── Task System ───────────────────────────────────────────────────

    public void EnqueueTask(WorkerTask task)
    {
        _taskQueue.Enqueue(task);
    }

    public void ClearTaskQueue()
    {
        _taskQueue.Clear();
        StopAllCoroutines();
        _isMoving     = false;
        CurrentState  = WorkerState.Idle;
        StartCoroutine(TaskLoop());
    }

    private IEnumerator TaskLoop()
    {
        while (true)
        {
            if (_taskQueue.Count > 0)
            {
                _idleTimer   = 0f;
                CurrentTask  = _taskQueue.Dequeue();
                CurrentState = WorkerState.Walking;

                yield return StartCoroutine(ExecuteTask(CurrentTask));

                CurrentTask  = null;
                CurrentState = WorkerState.Idle;
                UpdateSpriteState(null, WorkerState.Idle);

                yield return null;
            }
            else
            {
                CurrentState = WorkerState.Idle;

                _idleTimer += 0.2f;
                if (_idleTimer >= idleWanderInterval)
                {
                    _idleTimer = 0f;
                    yield return StartCoroutine(DoIdleWander());
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
    }

    protected virtual IEnumerator ExecuteTask(WorkerTask task)
    {
        // 1. Walk to source position
        yield return StartCoroutine(WalkTo(task.sourcePosition));

        // 2. Pick up item at source (takes baseActionDuration)
        if (task.pickupAction != null)
        {
            CurrentState = WorkerState.Working;
            UpdateSpriteState(task, WorkerState.Working, false);
            if (task.hideOnPickup && _spriteRenderer != null) _spriteRenderer.enabled = false;
            
            yield return new WaitForSeconds(baseActionDuration);
            
            if (task.hideOnPickup && _spriteRenderer != null) _spriteRenderer.enabled = true;
            task.pickupAction.Invoke();
        }

        // 3. Walk to destination (Now carrying the item)
        if (task.destinationPosition != task.sourcePosition)
        {
            UpdateSpriteState(task, WorkerState.Walking, task.targetItem != null);
            yield return StartCoroutine(WalkTo(task.destinationPosition));
        }

        // 4. Work duration at station (either explicit duration, or baseActionDuration if completionAction exists)
        if (task.workDuration > 0f)
        {
            CurrentState = WorkerState.Working;
            UpdateSpriteState(task, WorkerState.Working, task.targetItem != null);
            if (task.hideOnCompletion && _spriteRenderer != null) _spriteRenderer.enabled = false;
            
            yield return new WaitForSeconds(ApplySpeedBonus(task.workDuration, task.taskType));
            
            if (task.hideOnCompletion && _spriteRenderer != null) _spriteRenderer.enabled = true;
        }
        else if (task.completionAction != null)
        {
            // Even if no explicit work duration exists, putting down the item takes time
            CurrentState = WorkerState.Working;
            UpdateSpriteState(task, WorkerState.Working, task.targetItem != null);
            if (task.hideOnCompletion && _spriteRenderer != null) _spriteRenderer.enabled = false;
            
            yield return new WaitForSeconds(baseActionDuration);
            
            if (task.hideOnCompletion && _spriteRenderer != null) _spriteRenderer.enabled = true;
        }

        // 5. Complete task
        if (task.asyncCompletionAction != null)
        {
            CurrentState = WorkerState.Working;
            UpdateSpriteState(task, WorkerState.Working, task.targetItem != null);
            yield return StartCoroutine(task.asyncCompletionAction(this));
        }
        else if (task.completionAction != null)
        {
            task.completionAction.Invoke(this);
        }
    }

    // ── Movement ──────────────────────────────────────────────────────

    protected IEnumerator WalkTo(Vector3 target)
    {
        if (Vector3.Distance(transform.position, target) <= 2f)
            yield break;

        CurrentState = WorkerState.Walking;
        if (CurrentTask == null || CurrentTask.targetItem == null)
            UpdateSpriteState(null, WorkerState.Walking);

        List<Vector3> path = GridPathfinder.FindPath(transform.position, target, allowDiagonalMovement);
        if (path == null || path.Count == 0)
        {
            path = new List<Vector3> { target };
        }

        _isMoving = true;

        for (int i = 0; i < path.Count; i++)
        {
            _targetPosition = path[i];

            if (_spriteRenderer != null && Mathf.Abs(_targetPosition.x - transform.position.x) > 0.5f)
            {
                _spriteRenderer.flipX = _targetPosition.x < transform.position.x;
            }

            float threshold = (i == path.Count - 1) ? 2f : 1f;

            while (Vector3.Distance(transform.position, _targetPosition) > threshold)
            {
                yield return null;
            }
        }

        _isMoving = false;
        if (CurrentState == WorkerState.Walking && CurrentTask == null)
            UpdateSpriteState(null, WorkerState.Idle);
    }

    private void HandleMovement()
    {
        if (!_isMoving) return;
        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            moveSpeed * Time.deltaTime);
    }

    // ── Idle Wander ───────────────────────────────────────────────────

    protected virtual IEnumerator DoIdleWander()
    {
        if (IsStationary) yield break;

        // Wander in 2D within the strip — X and Y both randomized
        float wanderY = 0f;
        if (WorldGrid.Instance != null)
        {
            // Stay within grid vertical bounds
            wanderY = Random.Range(
                WorldGrid.Instance.BottomWorldY + WorldGrid.Instance.CellSize,
                WorldGrid.Instance.TopWorldY    - WorldGrid.Instance.CellSize);
        }
        else
        {
            wanderY = transform.position.y + Random.Range(-idleWanderRadius * 0.3f,
                                                           idleWanderRadius * 0.3f);
        }

        Vector3 wanderTarget = new Vector3(
            transform.position.x + Random.Range(-idleWanderRadius, idleWanderRadius),
            wanderY,
            transform.position.z);

        List<Vector3> path = GridPathfinder.FindPath(transform.position, wanderTarget, allowDiagonalMovement);
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

    // ── Sprite State Machine ──────────────────────────────────────────

    protected void UpdateSpriteState(WorkerTask task, WorkerState state, bool hasItem = false)
    {
        if (_spriteRenderer == null) return;

        Sprite targetSprite = sprites.idleSprite;

        if (state == WorkerState.Idle)
        {
            targetSprite = sprites.idleSprite;
        }
        else if (state == WorkerState.Walking)
        {
            if (hasItem && task != null && task.targetItem != null)
            {
                targetSprite = task.targetItem.itemType == LumberItemType.Log ? sprites.carryingLogSprite : sprites.carryingBoardSprite;
            }
            else
            {
                targetSprite = sprites.walkingSprite;
            }
        }
        else if (state == WorkerState.Working)
        {
            if (task != null && task.taskType == TaskType.Harvest)
            {
                targetSprite = sprites.choppingSprite;
            }
            else if (hasItem && task != null && task.targetItem != null)
            {
                targetSprite = task.targetItem.itemType == LumberItemType.Log ? sprites.carryingLogSprite : sprites.carryingBoardSprite;
            }
            else
            {
                targetSprite = sprites.idleSprite;
            }
        }

        if (targetSprite != null)
            _spriteRenderer.sprite = targetSprite;
    }

    // ── Speed Bonuses ─────────────────────────────────────────────────

    protected float ApplySpeedBonus(float baseDuration, TaskType type)
    {
        float bonus = type switch
        {
            TaskType.Milling   => millingSpeedBonus,
            TaskType.Kiln      => kilnSpeedBonus,
            TaskType.Surfacing => surfacingSpeedBonus,
            _ => 0f
        };
        return baseDuration * (1f - Mathf.Clamp01(bonus));
    }

    protected Vector3 GetBuildingBottomCenter(MonoBehaviour building, Vector3 fallback)
    {
        if (building == null) return fallback;
        
        SpriteRenderer[] renderers = building.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length > 0)
        {
            Bounds bestBounds = renderers[0].bounds;
            float maxVolume = bestBounds.size.sqrMagnitude;

            for (int i = 1; i < renderers.Length; i++)
            {
                // Find the single largest sprite (which should be the main building drawing)
                // We do NOT use Encapsulate, because smoke particles or floating UI
                // might balloon the bounds and throw off the center calculation.
                if (renderers[i].GetComponent<ParticleSystem>() == null)
                {
                    float vol = renderers[i].bounds.size.sqrMagnitude;
                    if (vol > maxVolume)
                    {
                        maxVolume = vol;
                        bestBounds = renderers[i].bounds;
                    }
                }
            }
            return new Vector3(bestBounds.center.x, bestBounds.min.y, 0f);
        }
        
        // If NO SpriteRenderer exists (like an empty GameManager/Building script object),
        // we MUST fall back to the building's defined InputPosition if possible!
        if (building is SawmillBuilding sawmill) return sawmill.InputPosition;
        if (building is KilnBuilding kiln) return kiln.InputPosition;
        if (building is SurfacingBuilding surf) return surf.InputPosition;

        return new Vector3(building.transform.position.x, fallback.y, 0f);
    }

    public abstract void AssignDefaultTasks();
}

// ── Supporting Types ──────────────────────────────────────────────────

public enum WorkerState { Idle, Walking, Working, Carrying }

public enum TaskType
{
    Harvest, Transport, Milling, Kiln,
    Surfacing, Finishing, Furniture, Turning, Selling,
    Planting    // Sawyer walks to an empty tree slot and plants a seedling
}

public class WorkerTask
{
    public TaskType   taskType;
    public Vector3    sourcePosition;
    public Vector3    destinationPosition;
    public float      workDuration = 0f;
    public LumberItem targetItem;
    public System.Action               pickupAction;
    public System.Action<WorkerBase>   completionAction;
    public System.Func<WorkerBase, System.Collections.IEnumerator> asyncCompletionAction;
    public string description = "";
    
    public bool hideOnPickup = false;
    public bool hideOnCompletion = false;

    public WorkerTask(TaskType type, Vector3 src, Vector3 dest, float duration = 0f)
    {
        taskType            = type;
        sourcePosition      = src;
        destinationPosition = dest;
        workDuration        = duration;
    }
}
