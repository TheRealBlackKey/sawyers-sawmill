using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the main camera for Sawyer's Sawmill.
///
/// Features:
///   • Click-drag 2D pan (X and Y)
///   • Scroll-wheel zoom (orthographic size)
///   • "Follow Sawyer" mode — camera tracks a target Transform
///   • Smooth lerp on all transitions
///   • Public API for scripted focus (e.g. click a building, camera pans to it)
///
/// IMPORTANT: This camera operates in orthographic mode.
/// Zoom is controlled via Camera.orthographicSize — smaller = more zoomed in.
/// </summary>
public class CameraScroll : MonoBehaviour
{
    // ── Scroll / Pan Bounds ───────────────────────────────────────────
    [Header("Pan Bounds")]
    [SerializeField] private float minX = -600f;
    [SerializeField] private float maxX =  600f;
    [SerializeField] private float minY = -120f;
    [SerializeField] private float maxY =  120f;

    // ── Drag Pan ──────────────────────────────────────────────────────
    [Header("Drag Pan")]
    [SerializeField] private bool  enableDragScroll  = true;
    [SerializeField] private float dragSensitivity   = 1f;

    // ── Edge Scroll ───────────────────────────────────────────────────
    [Header("Edge Scroll (optional)")]
    [SerializeField] private bool  enableEdgeScroll  = false;
    [SerializeField] private float edgeScrollSpeed   = 200f;
    [SerializeField] private float edgeScrollZone    = 60f;

    // ── Zoom ──────────────────────────────────────────────────────────
    [Header("Zoom")]
    [SerializeField] private float minOrthoSize      = 40f;   // Most zoomed in
    [SerializeField] private float maxOrthoSize      = 220f;  // Most zoomed out
    [SerializeField] private float defaultOrthoSize  = 165f;  // Starting zoom
    [SerializeField] private float zoomStep          = 15f;   // Size change per scroll tick
    [SerializeField] private float zoomSmoothing     = 8f;    // How fast zoom animates

    // ── Follow Mode ───────────────────────────────────────────────────
    [Header("Follow Mode")]
    [SerializeField] private bool      followOnStart    = false;
    [SerializeField] private Transform followTarget;           // Assign Sawyer in Inspector
    [SerializeField] private float     followLeadAmount  = 0f; // World units ahead of target's travel direction
    [SerializeField] private float     followSmoothing   = 5f; // Lower = tighter follow

    // ── General Smoothing ─────────────────────────────────────────────
    [Header("Smoothing")]
    [SerializeField] private float panSmoothing = 8f;

    // ── State ─────────────────────────────────────────────────────────
    private Camera _camera;
    private float  _targetX;
    private float  _targetY;
    private float  _targetOrthoSize;

    private bool   _isDragging;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartWorld;

    private bool   _followMode;
    private Vector3 _previousFollowPos;

    // Expose for HUD toggle
    public bool FollowMode
    {
        get => _followMode;
        set
        {
            _followMode = value;

            // When turning follow ON, snap target to current follow position so
            // the lerp starts from where the camera already is.
            if (_followMode && followTarget != null)
            {
                _previousFollowPos = followTarget.position;
            }

            // When turning follow OFF, keep _target where the camera currently is
            // so it doesn't snap or drift on re-enable.
            if (!_followMode)
            {
                _targetX = transform.position.x;
                _targetY = transform.position.y;
            }
        }
    }

    public bool IsFollowing => _followMode;
    public float CurrentOrthoSize => _camera != null ? _camera.orthographicSize : defaultOrthoSize;

    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _camera = GetComponent<Camera>();

        _targetX         = transform.position.x;
        _targetY         = transform.position.y;
        _targetOrthoSize = defaultOrthoSize;

        if (_camera != null)
            _camera.orthographicSize = defaultOrthoSize;
    }

    private void Start()
    {
        if (followOnStart && followTarget != null)
            FollowMode = true;
    }

    private void Update()
    {
        HandleZoom();

        if (_followMode && followTarget != null)
            UpdateFollowTarget();
        else
            HandleDragPan();

        if (enableEdgeScroll && !_followMode)
            HandleEdgeScroll();

        ApplyMovement();
    }

    // ── Zoom ──────────────────────────────────────────────────────────
    private void HandleZoom()
    {
        if (_camera == null || !_camera.orthographic) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        // scroll > 0 = scroll up = zoom in (decrease ortho size)
        float direction = scroll > 0f ? -1f : 1f;
        _targetOrthoSize = Mathf.Clamp(
            _targetOrthoSize + direction * zoomStep,
            minOrthoSize,
            maxOrthoSize);

        // Optional: zoom toward mouse position in world space
        // (keeps the point under the cursor stationary as you zoom)
        if (!_followMode)
        {
            Vector3 mouseScreen = new Vector3(
                mouse.position.value.x,
                mouse.position.value.y,
                0f);
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(mouseScreen);

            // Proportionally shift pan target toward mouse world position
            float zoomRatio = 1f - (_targetOrthoSize / _camera.orthographicSize);
            _targetX = Mathf.Lerp(_targetX, mouseWorld.x, zoomRatio * 0.35f);
            _targetY = Mathf.Lerp(_targetY, mouseWorld.y, zoomRatio * 0.35f);
        }
    }

    // ── Drag Pan ──────────────────────────────────────────────────────
    private void HandleDragPan()
    {
        if (!enableDragScroll) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _isDragging     = true;
            _dragStartMouse = mouse.position.value;
            _dragStartWorld = new Vector2(transform.position.x, transform.position.y);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            _isDragging = false;

        if (_isDragging)
        {
            Vector2 mouseDelta = (Vector2)mouse.position.value - _dragStartMouse;

            // Scale delta from screen pixels → world units
            float worldWidth  = GetWorldWidth();
            float worldHeight = GetWorldHeight();
            float worldDeltaX = mouseDelta.x * (worldWidth  / Screen.width)  * dragSensitivity;
            float worldDeltaY = mouseDelta.y * (worldHeight / Screen.height) * dragSensitivity;

            _targetX = _dragStartWorld.x - worldDeltaX;
            _targetY = _dragStartWorld.y - worldDeltaY;
        }
    }

    // ── Edge Scroll ───────────────────────────────────────────────────
    private void HandleEdgeScroll()
    {
        if (_isDragging) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        float mouseX = mouse.position.value.x;
        float mouseY = mouse.position.value.y;
        float dt     = Time.deltaTime;

        if (mouseX < edgeScrollZone)
            _targetX -= edgeScrollSpeed * (1f - mouseX / edgeScrollZone) * dt;
        else if (mouseX > Screen.width - edgeScrollZone)
            _targetX += edgeScrollSpeed * ((mouseX - (Screen.width - edgeScrollZone)) / edgeScrollZone) * dt;

        if (mouseY < edgeScrollZone)
            _targetY -= edgeScrollSpeed * (1f - mouseY / edgeScrollZone) * dt;
        else if (mouseY > Screen.height - edgeScrollZone)
            _targetY += edgeScrollSpeed * ((mouseY - (Screen.height - edgeScrollZone)) / edgeScrollZone) * dt;
    }

    // ── Follow Mode ───────────────────────────────────────────────────
    private void UpdateFollowTarget()
    {
        if (followTarget == null) return;

        Vector3 targetPos = followTarget.position;

        // Optional lead: offset in the direction Sawyer is currently moving
        if (followLeadAmount > 0.1f)
        {
            Vector3 velocity = targetPos - _previousFollowPos;
            if (velocity.sqrMagnitude > 0.001f)
                targetPos += velocity.normalized * followLeadAmount;
        }
        _previousFollowPos = followTarget.position;

        // Lerp _target toward Sawyer — panSmoothing controls tightness
        _targetX = Mathf.Lerp(_targetX, targetPos.x, Time.deltaTime * followSmoothing);
        _targetY = Mathf.Lerp(_targetY, targetPos.y, Time.deltaTime * followSmoothing);
    }

    // ── Apply Position and Zoom ───────────────────────────────────────
    private void ApplyMovement()
    {
        // Clamp pan targets to world bounds
        _targetX = Mathf.Clamp(_targetX, minX, maxX);
        _targetY = Mathf.Clamp(_targetY, minY, maxY);

        // Smooth pan
        float newX = Mathf.Lerp(transform.position.x, _targetX, Time.deltaTime * panSmoothing);
        float newY = Mathf.Lerp(transform.position.y, _targetY, Time.deltaTime * panSmoothing);
        transform.position = new Vector3(newX, newY, transform.position.z);

        // Smooth zoom
        if (_camera != null && _camera.orthographic)
        {
            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize,
                _targetOrthoSize,
                Time.deltaTime * zoomSmoothing);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private float GetWorldWidth()
    {
        if (_camera != null && _camera.orthographic)
            return _camera.orthographicSize * 2f * _camera.aspect;
        return 400f;
    }

    private float GetWorldHeight()
    {
        if (_camera != null && _camera.orthographic)
            return _camera.orthographicSize * 2f;
        return 200f;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Instantly teleport camera to a world position (no lerp).</summary>
    public void SnapTo(float worldX, float worldY)
    {
        _targetX = Mathf.Clamp(worldX, minX, maxX);
        _targetY = Mathf.Clamp(worldY, minY, maxY);
        transform.position = new Vector3(_targetX, _targetY, transform.position.z);
    }

    /// <summary>Smoothly pan the camera to focus on a world position.</summary>
    public void PanTo(float worldX, float worldY)
    {
        if (_followMode) FollowMode = false;  // Disengage follow when manually panning
        _targetX = Mathf.Clamp(worldX, minX, maxX);
        _targetY = Mathf.Clamp(worldY, minY, maxY);
    }

    /// <summary>Smoothly pan to a world position AND set a target zoom level.</summary>
    public void FocusOn(float worldX, float worldY, float orthoSize)
    {
        PanTo(worldX, worldY);
        _targetOrthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
    }

    /// <summary>Convenience: focus on a specific Transform (e.g. a building).</summary>
    public void FocusOn(Transform target, float orthoSize = 80f)
    {
        if (target == null) return;
        FocusOn(target.position.x, target.position.y, orthoSize);
    }

    /// <summary>Reset zoom to default level without changing pan position.</summary>
    public void ResetZoom()
    {
        _targetOrthoSize = defaultOrthoSize;
    }

    /// <summary>Set the follow target at runtime (e.g. when a new worker is hired).</summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    /// <summary>Expand world bounds when new zones are unlocked.</summary>
    public void ExpandBounds(float newMinX, float newMaxX, float newMinY = -120f, float newMaxY = 120f)
    {
        minX = newMinX;
        maxX = newMaxX;
        minY = newMinY;
        maxY = newMaxY;
    }

    // Legacy single-axis API kept for backward compatibility
    public void SnapToX(float worldX)  => SnapTo(worldX, transform.position.y);
    public void ScrollToX(float worldX) => PanTo(worldX, transform.position.y);

    public float CurrentX => transform.position.x;
    public float CurrentY => transform.position.y;
    public float MinX     => minX;
    public float MaxX     => maxX;
}
