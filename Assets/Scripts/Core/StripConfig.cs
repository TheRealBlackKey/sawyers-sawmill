using UnityEngine;

/// <summary>
/// Single source of truth for the game strip dimensions.
/// Change these values to resize the entire play area — WorldGrid,
/// camera bounds, and background all read from this asset.
///
/// Create via: Right-click > Create > SawyersSawmill > Strip Config
///
/// TUNING GUIDE:
///   stripHeight:  How tall the play area is in world units.
///                 At camera ortho size 165, the full screen height is ~330 units.
///                 A stripHeight of 160 = roughly half the screen.
///                 Try 80–200 to find the feel you want.
///
///   groundY:      World Y of the bottom of the strip (where Sawyer walks).
///                 Negative = below world center. Default -60 matches your current scene.
///
///   gridRows:     Derived from stripHeight / cellSize — set automatically.
///                 At cellSize 32, a stripHeight of 160 = 5 rows.
///                 A stripHeight of 320 = 10 rows (Rusty's Retirement feel).
///
/// RECOMMENDED STARTING POINTS:
///   Narrow overlay (Rusty-lite):  stripHeight = 160,  groundY = -60
///   Medium overlay:               stripHeight = 256,  groundY = -60
///   Tall overlay (full grid):     stripHeight = 320,  groundY = -60
/// </summary>
[CreateAssetMenu(fileName = "StripConfig", menuName = "SawyersSawmill/Strip Config")]
public class StripConfig : ScriptableObject
{
    [Header("Strip Dimensions")]
    [Tooltip("Total height of the playable strip in world units. " +
             "Drives grid rows, camera bounds, and background scale.")]
    public float stripHeight = 160f;

    [Tooltip("World Y position of the ground (bottom row of grid). " +
             "Workers walk along this line.")]
    public float groundY = -60f;

    [Header("Grid")]
    [Tooltip("Width of one grid cell in world units. " +
             "32 = tight Rusty's-style, 48 = roomier.")]
    public float cellSize = 32f;

    [Tooltip("Number of grid columns (horizontal extent of map).")]
    public int columns = 60;

    // ── Derived values (read-only, computed from above) ───────────────

    /// <summary>How many full grid rows fit in the strip height.</summary>
    public int Rows => Mathf.Max(1, Mathf.FloorToInt(stripHeight / cellSize));

    /// <summary>World Y of the top edge of the strip.</summary>
    public float StripTopY => groundY + stripHeight;

    /// <summary>World Y of the center of the strip (camera Y target).</summary>
    public float StripCenterY => groundY + stripHeight * 0.5f;

    /// <summary>Total world width of the grid.</summary>
    public float WorldWidth => columns * cellSize;
    
    /// <summary>Total world height of the true snapped grid.</summary>
    public float GridHeight => Rows * cellSize;

    /// <summary>Camera pan bounds derived from strip.</summary>
    public float MinWorldX => -WorldWidth * 0.5f;
    public float MaxWorldX =>  WorldWidth * 0.5f;
    public float MinWorldY => StripTopY - GridHeight;
    public float MaxWorldY => StripTopY;

    /// <summary>
    /// Scale to apply to a Background sprite that is 1 world unit tall.
    /// For a sprite with pixelsPerUnit = 100 and rect height = 100px
    /// (i.e. 1 world unit tall), this gives the correct Y scale.
    /// Adjust backgroundSpriteWorldHeight to match your actual sprite.
    /// </summary>
    [Header("Background Scaling Helper")]
    [Tooltip("How tall your background sprite is in world units at scale 1. " +
             "Check: sprite height in pixels / pixels per unit.")]
    public float backgroundSpriteWorldHeight = 1f;

    public float BackgroundScaleY => GridHeight / backgroundSpriteWorldHeight;
}
