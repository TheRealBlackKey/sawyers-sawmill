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

    [Header("Footprint")]
    public Vector2 footprintSize = new Vector2(80f, 50f);  // World units (width x height)

    [Header("Cost & Unlock")]
    public int goldCost = 100;
    public int reputationRequired = 0;
    public int logsHarvestedRequired = 0;
    public List<BuildingData> requiredBuildings = new List<BuildingData>(); // Prerequisites

    [Header("Category")]
    public BuildingCategory category = BuildingCategory.Production;

    [Header("Placement Rules")]
    public bool canPlaceNearWater = false;   // Some buildings (mill) can go near streams
    public bool requiresFlatGround = true;
    public float minDistanceFromSawmill = 0f; // 0 = no restriction

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
