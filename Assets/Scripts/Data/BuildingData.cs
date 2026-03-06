using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject defining a buildable structure in the world.
/// Create via: Right-click > Create > SawyersSawmill > Building Data
/// </summary>
[CreateAssetMenu(fileName = "New Building", menuName = "SawyersSawmill/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingName = "New Building";
    [TextArea(1, 3)]
    public string description = "";
    public Sprite previewSprite;       // Shown in build menu and as ghost
    public Sprite builtSprite;         // Shown when placed in world

    [Header("Art Alignment")]
    [Tooltip("Multiplier to artificially zoom the sprite if the PNG has a lot of empty transparent padding.")]
    public float visualScaleModifier = 1f;

    [Tooltip("How many grid cells UP from the bottom edge should the depth-sorting evaluate? Base 1 for typical buildings.")]
    public float depthSortRowOffset = 1f;

    [Header("Grid Footprint (in cells)")]
    [Tooltip("How many grid columns this building occupies. 1 cell = 32 world units.")]
    public int gridWidth  = 2;
    [Tooltip("How many grid rows this building occupies. 1 cell = 32 world units.")]
    public int gridHeight = 2;

    [Tooltip("The operational radius of this building in grid cells (0 means no area effect). Used for Helpers.")]
    public int effectRadius = 0;

    [Tooltip("The worker prefab to spawn when this building is created (used for Quarters).")]
    public GameObject helperPrefab;

    // Legacy world-unit footprint — kept for any scripts that still reference it.
    // Prefer gridWidth/gridHeight for all new placement logic.
    [HideInInspector] public Vector2 footprintSize = new Vector2(80f, 50f);

    [Header("Cost & Unlock")]
    public int goldCost = 100;
    public int reputationRequired = 0;
    public int logsHarvestedRequired = 0;
    public List<BuildingData> requiredBuildings = new List<BuildingData>(); // Prerequisites

    [Header("Category")]
    public BuildingCategory category = BuildingCategory.Production;

    [Header("Placement Rules")]
    public bool canPlaceNearWater = false;   // Reflected in WorldGrid cell queries (future)


    [Header("Runtime Reference")]
    public string scriptTypeName = "";  // e.g. "KilnBuilding" — attached when placed
}

public enum BuildingCategory
{
    Production,     // Kiln, Surfacing, Sawmill upgrades
    Forestry,       // Nursery, forest expansions
    Crafting,       // Turning studio, furniture workshop, cooperage
    Commerce,       // Market, wholesale
    Worker,         // Apprentice cabin
    Decoration      // Fences, paths, etc
}
