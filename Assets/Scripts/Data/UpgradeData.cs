using UnityEngine;

/// <summary>
/// ScriptableObject for a single purchasable upgrade.
/// Create via: Assets > Create > SawyersSawmill > Upgrade
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "SawyersSawmill/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string upgradeName = "Edger Attachment";
    [TextArea(2, 4)]
    public string description = "Reduces waste when milling logs, yielding one extra board per log.";
    public Sprite icon;
    public BuildingTarget targetBuilding;

    [Header("Cost & Unlock")]
    public float goldCost = 100f;
    public int dayUnlocked = 0;          // Day the upgrade becomes available to purchase
    public UpgradeData prerequisite;     // Must purchase this first (null = no prerequisite)
    public int reputationRequired = 0;

    [Header("Effect")]
    public UpgradeEffect effect;
    public float effectValue = 1f;       // Interpreted differently per effect type

    [Header("Visual")]
    public Sprite buildingOverlaySprite; // Optional visual change on building after upgrade

    public bool CanPurchase()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.Gold < goldCost) return false;
        if (gm.Reputation < reputationRequired) return false;
        if (gm.CurrentDay < dayUnlocked) return false;
        if (prerequisite != null && !UpgradeManager.Instance.IsUnlocked(prerequisite)) return false;
        return true;
    }
}

public enum BuildingTarget
{
    Forest,
    Sawmill,
    Kiln,
    Surfacing,
    Finishing,
    FurnitureWorkshop,
    TurningStudio,
    Cooperage,
    Market
}

public enum UpgradeEffect
{
    // Sawmill
    MillingSpeed,           // effectValue = multiplier (e.g., 0.7 = 30% faster)
    BoardsPerLog,           // effectValue = bonus boards (integer)
    UnlockLiveEdge,
    UnlockTurningBlanks,
    ExpandLogQueue,         // effectValue = additional slots

    // Kiln
    KilnUpgrade,            // effectValue = KilnBuilding.KilnType index
    KilnSlots,              // effectValue = additional slots
    ReduceChecking,         // effectValue = reduction factor
    UnlockHumidityChambr,   // Enables spalting mechanics

    // Surfacing
    UnlockJointer,
    UnlockPlaner,
    UnlockDrumSander,
    UnlockWideBeltSander,
    UnlockCNCRouter,
    SurfacingRarityBonus,   // effectValue = additional rarity bonus
    SurfacingSpeed,

    // Forest
    ReplanterCrew,
    IrrigationSystem,
    ForestExpansion,        // effectValue = additional tree slots
    SustainableCert,        // Market price bonus across all wood

    // Market
    WholesaleAccess,
    ExpandMarketOrders,     // effectValue = additional order slots
    ReputationMultiplier,

    // Workers
    HireWorker              // Unlocks a new worker character
}
