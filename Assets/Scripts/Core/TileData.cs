using UnityEngine;

/// <summary>
/// TileData — ScriptableObject that describes a single ground tile variant.
///
/// HOW TO CREATE:
///   Right-click in the Project window → Create → SawyersSawmill → TileData
///   Make one asset per tile variant (e.g. TD_Grass_Plain, TD_Dirt_Patch, TD_Flowers_Yellow).
///
/// SETUP TIPS:
///   • Slice your sprite sheet first (Sprite Editor → Grid By Cell Size).
///   • Assign the sliced sprite to the 'sprite' field.
///   • Use 'weight' to control how often a tile appears relative to others in the same list.
///     E.g. plain grass weight 40, flower tile weight 5 → flowers appear ~11% of the time.
///   • cellTypeOverride only matters for tiles that should mark a cell non-empty
///     (Boulder, Water). Leave it as Empty for all decorative/walkable ground tiles.
/// </summary>
[CreateAssetMenu(menuName = "SawyersSawmill/TileData", fileName = "TD_New")]
public class TileData : ScriptableObject
{
    [Tooltip("Human-readable label shown in the Inspector and used for debug logs.")]
    public string tileName = "New Tile";

    [Tooltip("The sliced sprite for this tile. Assign after slicing your sprite sheet.")]
    public Sprite sprite;

    [Tooltip("Logical category — used by WorldGenerator to know which list this tile belongs to.")]
    public TileCategory category;

    [Tooltip("Can Sawyer walk through this tile? Set false for solid rocks, dense bushes, etc.")]
    public bool isWalkable = true;

    [Tooltip("Higher weight = appears more often. All weights in a list are summed, " +
             "then each tile's share is weight / total. E.g. 40 out of 100 total = 40%.")]
    [Range(1, 100)]
    public int weight = 10;

    [Tooltip("If this tile should mark the underlying GridCell as a specific type, set it here. " +
             "Leave as Empty for standard walkable ground (grass, dirt, decorations).")]
    public CellType cellTypeOverride = CellType.Empty;
}

/// <summary>
/// Logical grouping used by WorldGenerator to pick tiles from the right list.
/// Add new categories here if you introduce new biomes or surface types.
/// </summary>
public enum TileCategory
{
    Grass,          // Base green ground — plain, subtle variants
    Dirt,           // Bare soil patches
    Decoration,     // Rocks, fallen branches, flowers — layered on top of grass
    River,          // Full water tiles (connect to WorldGrid Water cells)
    RiverEdge,      // Transition tiles between water and land
    Rock,           // Large boulder tiles (non-walkable)
    Bush,           // Dense vegetation (non-walkable)
    Path,           // Dirt path tiles
}
