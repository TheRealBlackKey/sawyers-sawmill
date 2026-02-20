using UnityEngine;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Makes Sawyer's Sawmill behave as a desktop overlay — transparent background,
/// always on top, and click-through when not hovering over game elements.
///
/// SETUP INSTRUCTIONS:
/// 1. In Player Settings: set "Window Mode" to Windowed
/// 2. Set background color to (0,0,0,0) fully transparent in camera
/// 3. Attach this to a persistent GameObject
/// 4. In Build Settings: enable "Allow Transparent Background" if available
///
/// NOTE: Transparent window overlay requires a standalone build.
///       This will not work in the Unity Editor — use #if !UNITY_EDITOR guards.
/// </summary>
public class DesktopOverlay : MonoBehaviour
{
    public static DesktopOverlay Instance { get; private set; }

    [Header("Overlay Settings")]
    [SerializeField] private bool startAsOverlay = true;
    [SerializeField] private int overlayHeight = 200;       // Pixels tall for the strip
    [SerializeField] private OverlayPosition overlayPosition = OverlayPosition.Bottom;
    [SerializeField] private bool clickThroughWhenIdle = true;

    private bool _isOverlayMode = false;
    private bool _isClickThrough = false;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // ── Windows API ───────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private IntPtr _windowHandle;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _windowHandle = GetActiveWindow();
        if (startAsOverlay)
            EnableOverlayMode();
#else
        Debug.Log("[DesktopOverlay] Overlay mode only works in standalone Windows build.");
#endif
    }

    // ── Public Controls ───────────────────────────────────────────────
    public void EnableOverlayMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Make window always-on-top and transparent
        SetWindowPos(_windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        uint style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        SetWindowLong(_windowHandle, GWL_EXSTYLE, style | WS_EX_LAYERED);

        // Position the window at the screen edge
        PositionWindow();

        _isOverlayMode = true;
        Debug.Log("[DesktopOverlay] Overlay mode enabled.");
#endif
    }

    public void DisableOverlayMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        SetWindowPos(_windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        uint style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        style &= ~WS_EX_LAYERED;
        style &= ~(uint)WS_EX_TRANSPARENT;
        SetWindowLong(_windowHandle, GWL_EXSTYLE, style);

        _isOverlayMode = false;
        _isClickThrough = false;
#endif
    }

    /// <summary>
    /// Enable click-through so the underlying desktop receives clicks.
    /// Call this when no UI elements are under the cursor.
    /// </summary>
    public void SetClickThrough(bool enabled)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_isClickThrough == enabled) return;

        uint style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        if (enabled)
            style |= WS_EX_TRANSPARENT;
        else
            style &= ~(uint)WS_EX_TRANSPARENT;

        SetWindowLong(_windowHandle, GWL_EXSTYLE, style);
        _isClickThrough = enabled;
#endif
    }

    private void PositionWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        int screenW = Display.main.systemWidth;
        int screenH = Display.main.systemHeight;
        int windowW = screenW;
        int windowH = overlayHeight;

        int posX = 0;
        int posY = overlayPosition switch
        {
            OverlayPosition.Bottom => screenH - overlayHeight,
            OverlayPosition.Top    => 0,
            _ => screenH - overlayHeight
        };

        Screen.SetResolution(windowW, windowH, FullScreenMode.Windowed);
#endif
    }

    private void Update()
    {
        if (!_isOverlayMode || !clickThroughWhenIdle) return;

        // Check if mouse is over any UI element
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        SetClickThrough(!overUI);
    }

    private void OnApplicationQuit()
    {
        if (_isOverlayMode)
            DisableOverlayMode();
    }

    // ── Camera Setup ──────────────────────────────────────────────────
    /// <summary>Call this to configure the main camera for transparent rendering.</summary>
    public static void SetupTransparentCamera(Camera cam)
    {
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }
}

public enum OverlayPosition { Bottom, Top }
