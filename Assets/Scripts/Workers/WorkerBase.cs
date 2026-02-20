using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Base class for all worker characters. Handles movement, task execution,
/// and the idle wandering that makes the game feel alive.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public abstract class WorkerBase : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Worker Identity")]
    public string workerName = "Sawyer";
    [TextArea(1, 2)]
    public string bio = "A retired master woodworker.";
    public Sprite portrait;

    [Header("Movement")]
    public float moveSpeed = 60f;           // Pixels per second
    public float idleWanderRadius = 40f;
    public float idleWanderInterval = 4f;

    [Header("Specialization (0-1 bonus multipliers)")]
    public float millingSpeedBonus = 0f;
    public float kilnSpeedBonus = 0f;
    public float surfacingSpeedBonus = 0f;
    public float rareGrainChanceBonus = 0f;  // Added to grain roll for this worker

    // ── State ─────────────────────────────────────────────────────────
    public WorkerState CurrentState { get; protected set; } = WorkerState.Idle;
    public WorkerTask CurrentTask { get; protected set; }

    protected Queue<WorkerTask> _taskQueue = new Queue<WorkerTask>();
    protected Animator _animator;
    protected SpriteRenderer _spriteRenderer;
    protected Vector3 _targetPosition;
    protected bool _isMoving = false;

    // ── Animation Hash IDs ────────────────────────────────────────────
    private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int AnimIsWorking = Animator.StringToHash("IsWorking");
    private static readonly int AnimTaskType  = Animator.StringToHash("TaskType");

    protected virtual void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected virtual void Start()
    {
        StartCoroutine(TaskLoop());
        StartCoroutine(IdleWander());
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
        CurrentState = WorkerState.Idle;
        StartCoroutine(TaskLoop());
        StartCoroutine(IdleWander());
    }

    private IEnumerator TaskLoop()
    {
        while (true)
        {
            if (_taskQueue.Count > 0)
            {
                CurrentTask = _taskQueue.Dequeue();
                yield return StartCoroutine(ExecuteTask(CurrentTask));
                CurrentTask = null;
            }
            else
            {
                CurrentState = WorkerState.Idle;
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    protected virtual IEnumerator ExecuteTask(WorkerTask task)
    {
        // 1. Walk to source
        CurrentState = WorkerState.Walking;
        yield return StartCoroutine(WalkTo(task.sourcePosition));

        // 2. Pick up item
        if (task.pickupAction != null)
        {
            task.pickupAction.Invoke();
        }

        // 3. Walk to destination (if different from source)
        if (task.destinationPosition != task.sourcePosition)
        {
            yield return StartCoroutine(WalkTo(task.destinationPosition));
        }

        // 4. Perform work at station
        if (task.workDuration > 0f)
        {
            CurrentState = WorkerState.Working;
            SetWorkingAnimation(task.taskType);
            float adjustedDuration = ApplySpeedBonus(task.workDuration, task.taskType);
            yield return new WaitForSeconds(adjustedDuration);
        }

        // 5. Complete task
        task.completionAction?.Invoke(this);
        CurrentState = WorkerState.Idle;
        SetIdleAnimation();
    }

    // ── Movement ──────────────────────────────────────────────────────
    protected IEnumerator WalkTo(Vector3 target)
    {
        _targetPosition = target;
        _isMoving = true;
        if (_animator != null) _animator.SetBool(AnimIsWalking, true);

        // Flip sprite based on direction
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = target.x < transform.position.x;

        while (Vector3.Distance(transform.position, _targetPosition) > 2f)
        {
            yield return null;
        }

        _isMoving = false;
        if (_animator != null) _animator.SetBool(AnimIsWalking, false);
    }

    private void HandleMovement()
    {
        if (!_isMoving) return;
        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    private IEnumerator IdleWander()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleWanderInterval);

            if (CurrentState == WorkerState.Idle && _taskQueue.Count == 0)
            {
                Vector3 wanderTarget = transform.position + new Vector3(
                    Random.Range(-idleWanderRadius, idleWanderRadius), 0f, 0f);
                _targetPosition = wanderTarget;
                _isMoving = true;
                if (_animator != null) _animator.SetBool(AnimIsWalking, true);
                yield return new WaitForSeconds(1.5f);
                _isMoving = false;
                if (_animator != null) _animator.SetBool(AnimIsWalking, false);
            }
        }
    }

    // ── Animations ────────────────────────────────────────────────────
    private void SetWorkingAnimation(TaskType type)
    {
        if (_animator == null) return;
        _animator.SetBool(AnimIsWorking, true);
        _animator.SetInteger(AnimTaskType, (int)type);
    }

    private void SetIdleAnimation()
    {
        if (_animator == null) return;
        _animator.SetBool(AnimIsWorking, false);
        _animator.SetBool(AnimIsWalking, false);
    }

    // ── Speed Bonuses ─────────────────────────────────────────────────
    protected float ApplySpeedBonus(float baseDuration, TaskType type)
    {
        float bonus = type switch
        {
            TaskType.Milling    => millingSpeedBonus,
            TaskType.Kiln       => kilnSpeedBonus,
            TaskType.Surfacing  => surfacingSpeedBonus,
            _ => 0f
        };
        return baseDuration * (1f - Mathf.Clamp01(bonus));
    }

    public abstract void AssignDefaultTasks(); // Each worker subclass knows its default role
}

// ── Supporting Types ──────────────────────────────────────────────────
public enum WorkerState { Idle, Walking, Working, Carrying }

public enum TaskType
{
    Harvest,
    Transport,
    Milling,
    Kiln,
    Surfacing,
    Finishing,
    Furniture,
    Turning,
    Selling
}

public class WorkerTask
{
    public TaskType taskType;
    public Vector3 sourcePosition;
    public Vector3 destinationPosition;
    public float workDuration = 0f;
    public LumberItem targetItem;
    public System.Action pickupAction;
    public System.Action<WorkerBase> completionAction;
    public string description = "";

    public WorkerTask(TaskType type, Vector3 src, Vector3 dest, float duration = 0f)
    {
        taskType = type;
        sourcePosition = src;
        destinationPosition = dest;
        workDuration = duration;
    }
}
