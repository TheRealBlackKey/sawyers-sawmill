#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility — creates BD_Surfacing and BD_Market ScriptableObject assets
/// with all fields pre-filled correctly.
///
/// Run via:  Tools > Sawyer's Sawmill > Create Surfacing + Market Assets
///
/// After running, both .asset files appear in Assets/Data/Buildings/
/// Then drag them into BuildMenu.allBuildings in the Inspector.
/// </summary>
public static class CreateBuildingAssets
{
    private const string FolderPath = "Assets/Data/Buildings";

    [MenuItem("Tools/Sawyer's Sawmill/Create Surfacing + Market Assets")]
    public static void CreateAll()
    {
        EnsureFolderExists(FolderPath);

        CreateSurfacing();
        CreateMarket();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Sawyer's Sawmill] ✅ BD_Surfacing and BD_Market created in Assets/Data/Buildings/");
        EditorUtility.DisplayDialog(
            "Assets Created!",
            "BD_Surfacing and BD_Market have been created in:\n\nAssets/Data/Buildings/\n\n" +
            "Next step: Open the BuildMenu GameObject in your scene,\n" +
            "find the 'All Buildings' list, and drag both new assets in.",
            "Got it");
    }

    // ── Surfacing Station ─────────────────────────────────────────────

    private static void CreateSurfacing()
    {
        string assetPath = $"{FolderPath}/BD_Surfacing.asset";

        // Don't overwrite if it already exists
        if (File.Exists(Application.dataPath + assetPath.Substring(6)))
        {
            Debug.LogWarning($"[Sawyer's Sawmill] BD_Surfacing already exists at {assetPath} — skipping.");
            return;
        }

        var asset = ScriptableObject.CreateInstance<BuildingData>();

        asset.buildingName         = "Surfacing Station";
        asset.description          = "Reveals the hidden grain figure in kiln-dried boards. " +
                                     "Each board is a surprise — straight grain, wavy figure, " +
                                     "or rare burl patterns worth far more at market.";
        asset.goldCost             = 120;
        asset.reputationRequired   = 0;
        asset.logsHarvestedRequired = 0;
        asset.gridWidth            = 2;   // 2 cells wide (64 world units)
        asset.gridHeight           = 1;   // 1 cell tall  (32 world units)
        asset.category             = BuildingCategory.Production;
        asset.canPlaceNearWater    = false;
        asset.scriptTypeName       = "SurfacingBuilding";  // Must match class name exactly

        // Sprites left null — assign in Inspector once you have the art
        asset.previewSprite = null;
        asset.builtSprite   = null;

        AssetDatabase.CreateAsset(asset, assetPath);
        Debug.Log($"[Sawyer's Sawmill] Created {assetPath}");
    }

    // ── Market ────────────────────────────────────────────────────────

    private static void CreateMarket()
    {
        string assetPath = $"{FolderPath}/BD_Market.asset";

        if (File.Exists(Application.dataPath + assetPath.Substring(6)))
        {
            Debug.LogWarning($"[Sawyer's Sawmill] BD_Market already exists at {assetPath} — skipping.");
            return;
        }

        var asset = ScriptableObject.CreateInstance<BuildingData>();

        asset.buildingName         = "Market Stall";
        asset.description          = "Sells finished lumber to buyers in town. " +
                                     "Higher quality boards and rare grain patterns " +
                                     "command premium prices. Watch for market days!";
        asset.goldCost             = 150;
        asset.reputationRequired   = 0;
        asset.logsHarvestedRequired = 0;
        asset.gridWidth            = 3;   // 3 cells wide (96 world units)
        asset.gridHeight           = 2;   // 2 cells tall  (64 world units)
        asset.category             = BuildingCategory.Commerce;
        asset.canPlaceNearWater    = false;
        asset.scriptTypeName       = "MarketBuilding";  // Must match class name exactly

        asset.previewSprite = null;
        asset.builtSprite   = null;

        AssetDatabase.CreateAsset(asset, assetPath);
        Debug.Log($"[Sawyer's Sawmill] Created {assetPath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void EnsureFolderExists(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            // Create intermediate folders if needed
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
