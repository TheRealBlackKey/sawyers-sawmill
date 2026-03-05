using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWoodSpecies", menuName = "SawyersSawmill/Wood Species")]
public class WoodSpeciesData : ScriptableObject
{
    [Header("Identity")]
    public string speciesName = "Pine";
    public string scientificName = "Pinus strobus";
    [TextArea(2, 4)]
    public string flavorText = "A softwood workhorse - fast growing and easy to work.";

    [Header("Tree Sprites (Growth Stages)")]
    [Tooltip("Multiple sprites can be assigned here. Trees will randomly pick one to give the forest variety.")]
    public Sprite[] treeSpritesSapling;
    [Tooltip("Multiple sprites can be assigned here. Trees will randomly pick one to give the forest variety.")]
    public Sprite[] treeSpritesJuvenile;
    [Tooltip("Multiple sprites can be assigned here. Trees will randomly pick one when they mature to give the forest variety.")]
    public Sprite[] treeSpritesMature;
    public Sprite treeSpriteReady;
    public Sprite logSprite;
    public Sprite boardSprite;
    public Color barkColor = Color.gray;
    public Color heartwoodColor = new Color(0.9f, 0.8f, 0.6f);

    [Header("Forest / Growth")]
    public float growthTimeSeconds = 60f;
    public float regrowthMultiplier = 1f;
    public int logsPerTree = 3;
    public int unlockCost = 0;
    public int totalLogsToUnlock = 0;
    public bool requiresReplanting = false;
    [Tooltip("How many times this tree can be harvested before becoming a permanent stump. Pine=3, Oak=5, Walnut=6.")]
    public int maxHarvestCycles = 3;
    [Tooltip("Gold cost to plant one sapling of this species. Deducted when the player clicks to replant a stump or empty slot.")]
    public float saplingCost = 5f;

    [Header("Market Values (Base Gold)")]
    public float valuePerLogRaw = 2f;
    public float valuePerBoardRoughSawn = 5f;
    public float valuePerBoardKilnDried = 8f;
    public float valuePerBoardSurfaced = 14f;

    [Header("Processing")]
    public float kilnDryTimeSeconds = 120f;
    public float surfaceTimeSeconds = 30f;
    public float millingTimePerLog = 20f;
    public int boardsPerLog = 4;

    [Header("Grain and Figure System")]
    public List<GrainVariant> grainVariants = new List<GrainVariant>();

    [Header("Special Properties")]
    public bool isAromatic = false;
    public bool isSteamBendable = false;
    public bool isExotic = false;
    public float hardness = 0.5f;

    public GrainVariant RollGrainVariant(float rarityBonus = 0f)
    {
        float roll = Random.value - rarityBonus;
        float cumulative = 0f;
        var sorted = new List<GrainVariant>(grainVariants);
        sorted.Sort((a, b) => a.dropChance.CompareTo(b.dropChance));
        foreach (var variant in sorted)
        {
            cumulative += variant.dropChance;
            if (roll <= cumulative) return variant;
        }
        return grainVariants.Count > 0 ? grainVariants[0] : null;
    }

    public float GetBoardValue(ProcessingStage stage, GrainVariant grain = null)
    {
        float baseValue = stage switch
        {
            ProcessingStage.RoughSawn => valuePerBoardRoughSawn,
            ProcessingStage.KilnDried => valuePerBoardKilnDried,
            ProcessingStage.Surfaced  => valuePerBoardSurfaced,
            _                         => valuePerBoardRoughSawn
        };
        float multiplier = grain != null ? grain.valueMultiplier : 1f;
        return baseValue * multiplier;
    }

    public Sprite GetRandomMatureSprite()
    {
        if (treeSpritesMature == null || treeSpritesMature.Length == 0) return null;
        if (treeSpritesMature.Length == 1) return treeSpritesMature[0];
        return treeSpritesMature[Random.Range(0, treeSpritesMature.Length)];
    }

    public Sprite GetRandomSaplingSprite()
    {
        if (treeSpritesSapling == null || treeSpritesSapling.Length == 0) return null;
        if (treeSpritesSapling.Length == 1) return treeSpritesSapling[0];
        return treeSpritesSapling[Random.Range(0, treeSpritesSapling.Length)];
    }

    public Sprite GetRandomJuvenileSprite()
    {
        if (treeSpritesJuvenile == null || treeSpritesJuvenile.Length == 0) return null;
        if (treeSpritesJuvenile.Length == 1) return treeSpritesJuvenile[0];
        return treeSpritesJuvenile[Random.Range(0, treeSpritesJuvenile.Length)];
    }
}

[System.Serializable]
public class GrainVariant
{
    public string variantName = "Straight Grain";
    [TextArea(1, 2)]
    public string description = "Clean, consistent grain pattern.";
    public Sprite boardSprite;
    public float dropChance = 0.6f;
    public float valueMultiplier = 1f;
    public bool isUnlocked = false;
    public int boardsToUnlock = 0;
    [Range(0, 3)]
    public int rarityTier = 0;
    public Color grainTintColor = Color.white;
    public bool triggerRevealEffect = false;
}

public enum ProcessingStage
{
    Log,
    RoughSawn,
    KilnDried,
    Surfaced,
    Finished
}
