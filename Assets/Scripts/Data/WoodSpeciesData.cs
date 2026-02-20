using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject defining all properties of a wood species.
/// Create via: Assets > Create > SawyersSawmill > Wood Species
/// </summary>
[CreateAssetMenu(fileName = "NewWoodSpecies", menuName = "SawyersSawmill/Wood Species")]
public class WoodSpeciesData : ScriptableObject
{
    [Header("Identity")]
    public string speciesName = "Pine";
    public string scientificName = "Pinus strobus";
    [TextArea(2, 4)]
    public string flavorText = "A softwood workhorse - fast growing and easy to work.";
    public Sprite treeSprite;
    public Sprite logSprite;
    public Sprite boardSprite;
    public Color barkColor = Color.gray;
    public Color heartwoodColor = new Color(0.9f, 0.8f, 0.6f);

    [Header("Forest / Growth")]
    public float growthTimeSeconds = 60f;       // Time from sapling to harvestable tree
    public float regrowthMultiplier = 1f;        // Multiplier on growthTime for subsequent harvests (stumps regrow)
    public int logsPerTree = 3;                  // How many logs one tree yields
    public int unlockCost = 0;                   // Gold cost to unlock this species
    public int totalLogsToUnlock = 0;            // Must harvest this many total logs to unlock (0 = available from start)
    public bool requiresReplanting = false;      // If true, stump does not regrow; needs replanting

    [Header("Market Values (Base Gold)")]
    public float valuePerLogRaw = 2f;
    public float valuePerBoardRoughSawn = 5f;
    public float valuePerBoardKilnDried = 8f;
    public float valuePerBoardSurfaced = 14f;

    [Header("Processing")]
    public float kilnDryTimeSeconds = 120f;       // Time to kiln dry one board
    public float surfaceTimeSeconds = 30f;        // Time to surface one board
    public float millingTimePerLog = 20f;         // Time to mill one log into boards
    public int boardsPerLog = 4;                  // Average boards yielded per log (before edger upgrade)

    [Header("Grain & Figure System")]
    public List<GrainVariant> grainVariants = new List<GrainVariant>();

    [Header("Special Properties")]
    public bool isAromatic = false;               // Cedar etc. — unlocks aromatic product line
    public bool isSteamBendable = false;          // Ash, Oak — unlocks cooperage
    public bool isExotic = false;                 // Affects market reputation gain
    public float hardness = 0.5f;                 // 0-1, affects tool wear rate

    // ── Runtime helpers ──────────────────────────────────────────────
    public GrainVariant RollGrainVariant(float rarityBonus = 0f)
    {
        float roll = Random.value - rarityBonus;
        float cumulative = 0f;

        // Sort by rarity ascending so we check common first
        var sorted = new List<GrainVariant>(grainVariants);
        sorted.Sort((a, b) => a.dropChance.CompareTo(b.dropChance));

        foreach (var variant in sorted)
        {
            cumulative += variant.dropChance;
            if (roll <= cumulative)
                return variant;
        }

        return grainVariants.Count > 0 ? grainVariants[0] : null;
    }

    public float GetBoardValue(ProcessingStage stage, GrainVariant grain = null)
    {
        float baseValue = stage switch
        {
            ProcessingStage.RoughSawn   => valuePerBoardRoughSawn,
            ProcessingStage.KilnDried   => valuePerBoardKilnDried,
            ProcessingStage.Surfaced    => valuePerBoardSurfaced,
            _                           => valuePerBoardRoughSawn
        };

        float multiplier = grain != null ? grain.valueMultiplier : 1f;
        return baseValue * multiplier;
    }
}

[System.Serializable]
public class GrainVariant
{
    public string variantName = "Straight Grain";
    [TextArea(1, 2)]
    public string description = "Clean, consistent grain pattern.";
    public Sprite boardSprite;          // Override sprite when this grain is revealed
    public float dropChance = 0.6f;     // 0-1 probability weight
    public float valueMultiplier = 1f;
    public bool isUnlocked = false;     // Starts locked; revealed after processing threshold
    public int boardsToUnlock = 0;      // How many boards of this species to process before it can drop
    [Range(0, 3)]
    public int rarityTier = 0;          // 0=Common, 1=Uncommon, 2=Rare, 3=Legendary
    public Color grainTintColor = Color.white;
    public bool triggerRevealEffect = false; // Play the dramatic sawdust-reveal animation
}

public enum ProcessingStage
{
    Log,
    RoughSawn,
    KilnDried,
    Surfaced,
    Finished
}
