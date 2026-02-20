/*
 * ============================================================
 * SAWYER'S SAWMILL — Wood Species Configuration Reference
 * ============================================================
 * Create a WoodSpeciesData ScriptableObject asset for each 
 * species below. Values shown are recommended starting points.
 *
 * In Unity: Right-click in Project > Create > SawyersSawmill > Wood Species
 * ============================================================
 */

using UnityEngine;

/// <summary>
/// Runtime setup helper — call SpeciesPresets.Configure(speciesAsset) after
/// creating a blank WoodSpeciesData asset if you want code-driven initialization.
/// In production you'll set these directly in the Inspector.
/// </summary>
public static class SpeciesPresets
{
    public static void ApplyPreset(WoodSpeciesData data, SpeciesPreset preset)
    {
        switch (preset)
        {
            case SpeciesPreset.Pine:
                data.speciesName = "Eastern White Pine";
                data.scientificName = "Pinus strobus";
                data.flavorText = "A softwood workhorse. Fast-growing and forgiving to work with.";
                data.growthTimeSeconds = 45f;
                data.regrowthMultiplier = 0.9f;
                data.logsPerTree = 3;
                data.boardsPerLog = 5;
                data.millingTimePerLog = 15f;
                data.kilnDryTimeSeconds = 60f;
                data.surfaceTimeSeconds = 20f;
                data.valuePerLogRaw = 2f;
                data.valuePerBoardRoughSawn = 4f;
                data.valuePerBoardKilnDried = 7f;
                data.valuePerBoardSurfaced = 11f;
                data.hardness = 0.2f;
                data.totalLogsToUnlock = 0;  // Starter species
                data.unlockCost = 0;
                // Grain variants: Straight, Wavy, Knotty Rustic
                break;

            case SpeciesPreset.Oak:
                data.speciesName = "Red Oak";
                data.scientificName = "Quercus rubra";
                data.flavorText = "The American classic. Strong, beautiful, beloved by furniture makers.";
                data.growthTimeSeconds = 90f;
                data.regrowthMultiplier = 1.1f;
                data.logsPerTree = 4;
                data.boardsPerLog = 4;
                data.millingTimePerLog = 22f;
                data.kilnDryTimeSeconds = 100f;
                data.surfaceTimeSeconds = 28f;
                data.valuePerLogRaw = 5f;
                data.valuePerBoardRoughSawn = 10f;
                data.valuePerBoardKilnDried = 18f;
                data.valuePerBoardSurfaced = 28f;
                data.hardness = 0.65f;
                data.isSteamBendable = true;
                data.totalLogsToUnlock = 30;
                data.unlockCost = 0;
                // Grain variants: Straight, Rift-Sawn, Quarter-Sawn (ray fleck), Burl
                break;

            case SpeciesPreset.Walnut:
                data.speciesName = "Black Walnut";
                data.scientificName = "Juglans nigra";
                data.flavorText = "Rich chocolate tones and exceptional figure. Every woodworker's dream.";
                data.growthTimeSeconds = 150f;
                data.regrowthMultiplier = 1.3f;
                data.logsPerTree = 3;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 30f;
                data.kilnDryTimeSeconds = 140f;
                data.surfaceTimeSeconds = 35f;
                data.valuePerLogRaw = 12f;
                data.valuePerBoardRoughSawn = 28f;
                data.valuePerBoardKilnDried = 48f;
                data.valuePerBoardSurfaced = 80f;
                data.hardness = 0.7f;
                data.totalLogsToUnlock = 80;
                data.unlockCost = 200;
                // Grain variants: Straight, Wavy, Crotch Figure, Curly, Spalted, Burl
                break;

            case SpeciesPreset.Cherry:
                data.speciesName = "American Black Cherry";
                data.scientificName = "Prunus serotina";
                data.flavorText = "Ages to a warm amber that painters try to replicate. Impossible to fake.";
                data.growthTimeSeconds = 140f;
                data.regrowthMultiplier = 1.2f;
                data.logsPerTree = 3;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 25f;
                data.kilnDryTimeSeconds = 120f;
                data.surfaceTimeSeconds = 32f;
                data.valuePerLogRaw = 10f;
                data.valuePerBoardRoughSawn = 22f;
                data.valuePerBoardKilnDried = 40f;
                data.valuePerBoardSurfaced = 65f;
                data.hardness = 0.6f;
                data.totalLogsToUnlock = 60;
                data.unlockCost = 150;
                // Grain variants: Straight, Wavy, Wild Grain, Figured, Spalted
                break;

            case SpeciesPreset.HardMaple:
                data.speciesName = "Hard Maple";
                data.scientificName = "Acer saccharum";
                data.flavorText = "Pale and clean until you find figure. Then it stops traffic.";
                data.growthTimeSeconds = 160f;
                data.regrowthMultiplier = 1.2f;
                data.logsPerTree = 4;
                data.boardsPerLog = 4;
                data.millingTimePerLog = 35f;
                data.kilnDryTimeSeconds = 130f;
                data.surfaceTimeSeconds = 40f;
                data.valuePerLogRaw = 8f;
                data.valuePerBoardRoughSawn = 18f;
                data.valuePerBoardKilnDried = 32f;
                data.valuePerBoardSurfaced = 55f;
                data.hardness = 0.85f;
                data.totalLogsToUnlock = 100;
                data.unlockCost = 300;
                // Grain variants: Straight, Bird's Eye, Curly/Tiger, Quilted, Spalted, Burl
                break;

            case SpeciesPreset.Ash:
                data.speciesName = "White Ash";
                data.scientificName = "Fraxinus americana";
                data.flavorText = "The wood of baseball bats and tool handles. Open grain, Olympic strength.";
                data.growthTimeSeconds = 100f;
                data.regrowthMultiplier = 1.0f;
                data.logsPerTree = 4;
                data.boardsPerLog = 4;
                data.millingTimePerLog = 20f;
                data.kilnDryTimeSeconds = 90f;
                data.surfaceTimeSeconds = 25f;
                data.valuePerLogRaw = 6f;
                data.valuePerBoardRoughSawn = 13f;
                data.valuePerBoardKilnDried = 22f;
                data.valuePerBoardSurfaced = 36f;
                data.hardness = 0.75f;
                data.isSteamBendable = true;
                data.totalLogsToUnlock = 50;
                data.unlockCost = 100;
                // Grain variants: Straight, Olive Grain, Curly, Spalted
                break;

            case SpeciesPreset.Cedar:
                data.speciesName = "Eastern Red Cedar";
                data.scientificName = "Juniperus virginiana";
                data.flavorText = "Aromatic, insect-repelling, stunning purple-red heartwood.";
                data.growthTimeSeconds = 80f;
                data.regrowthMultiplier = 1.0f;
                data.logsPerTree = 3;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 18f;
                data.kilnDryTimeSeconds = 70f;
                data.surfaceTimeSeconds = 22f;
                data.valuePerLogRaw = 7f;
                data.valuePerBoardRoughSawn = 16f;
                data.valuePerBoardKilnDried = 28f;
                data.valuePerBoardSurfaced = 45f;
                data.hardness = 0.35f;
                data.isAromatic = true;
                data.totalLogsToUnlock = 40;
                data.unlockCost = 80;
                // Grain variants: Straight, Pencil Cedar Figure, Knotty, Heartwood/Sapwood
                break;

            case SpeciesPreset.BlackLocust:
                data.speciesName = "Black Locust";
                data.scientificName = "Robinia pseudoacacia";
                data.flavorText = "Denser than oak, naturally rot-resistant. Fence posts that outlive fences.";
                data.growthTimeSeconds = 200f;
                data.regrowthMultiplier = 1.5f;
                data.logsPerTree = 3;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 45f;
                data.kilnDryTimeSeconds = 180f;
                data.surfaceTimeSeconds = 50f;
                data.valuePerLogRaw = 15f;
                data.valuePerBoardRoughSawn = 35f;
                data.valuePerBoardKilnDried = 60f;
                data.valuePerBoardSurfaced = 100f;
                data.hardness = 0.95f;
                data.totalLogsToUnlock = 200;
                data.unlockCost = 500;
                // Grain variants: Straight, Interlocked, Figured, Green Heartwood
                break;

            case SpeciesPreset.Purpleheart:
                data.speciesName = "Purpleheart";
                data.scientificName = "Peltogyne sp.";
                data.flavorText = "The wood that turns violet in sunlight. Collectors pay extraordinary prices.";
                data.growthTimeSeconds = 300f;
                data.regrowthMultiplier = 2f;
                data.logsPerTree = 2;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 50f;
                data.kilnDryTimeSeconds = 200f;
                data.surfaceTimeSeconds = 60f;
                data.valuePerLogRaw = 30f;
                data.valuePerBoardRoughSawn = 80f;
                data.valuePerBoardKilnDried = 140f;
                data.valuePerBoardSurfaced = 250f;
                data.hardness = 0.95f;
                data.isExotic = true;
                data.requiresReplanting = true;
                data.totalLogsToUnlock = 500;
                data.unlockCost = 2000;
                // Grain variants: Straight, Interlocked, Figured Violet, Deep Purple
                break;

            case SpeciesPreset.Wenge:
                data.speciesName = "Wenge";
                data.scientificName = "Millettia laurentii";
                data.flavorText = "Dark as night, with bold grain lines that make any piece a statement.";
                data.growthTimeSeconds = 280f;
                data.regrowthMultiplier = 2f;
                data.logsPerTree = 2;
                data.boardsPerLog = 3;
                data.millingTimePerLog = 48f;
                data.kilnDryTimeSeconds = 190f;
                data.surfaceTimeSeconds = 55f;
                data.valuePerLogRaw = 28f;
                data.valuePerBoardRoughSawn = 75f;
                data.valuePerBoardKilnDried = 130f;
                data.valuePerBoardSurfaced = 230f;
                data.hardness = 0.9f;
                data.isExotic = true;
                data.requiresReplanting = true;
                data.totalLogsToUnlock = 450;
                data.unlockCost = 1800;
                // Grain variants: Straight, Ribbon, Interlocked, Pale Streak
                break;
        }
    }
}

public enum SpeciesPreset
{
    Pine,
    Oak,
    Walnut,
    Cherry,
    HardMaple,
    Ash,
    Cedar,
    BlackLocust,
    Purpleheart,
    Wenge
}
