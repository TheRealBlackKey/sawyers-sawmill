using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all purchased upgrades. Apply effects to buildings immediately on purchase.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public static event System.Action<UpgradeData> OnUpgradePurchased;

    [Header("Building References")]
    [SerializeField] private SawmillBuilding sawmill;
    [SerializeField] private KilnBuilding kiln;
    [SerializeField] private SurfacingBuilding surfacing;
    [SerializeField] private MarketBuilding market;

    [Header("All Available Upgrades")]
    [SerializeField] private UpgradeData[] allUpgrades;

    [Header("Overlay Sprite Offsets")]
    [Tooltip("World-space offsets for each upgrade's overlay sprite, matched by upgrade name")]
    [SerializeField] private List<UpgradeOverlayOffset> overlayOffsets = new List<UpgradeOverlayOffset>();

    private HashSet<string> _purchasedUpgrades = new HashSet<string>();
    private Dictionary<string, GameObject> _spawnedOverlays = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool IsUnlocked(UpgradeData upgrade)
        => upgrade != null && _purchasedUpgrades.Contains(upgrade.name);

    public bool TryPurchase(UpgradeData upgrade)
    {
        if (upgrade == null) return false;
        if (IsUnlocked(upgrade)) return false;
        if (!upgrade.CanPurchase()) return false;
        if (!GameManager.Instance.SpendGold(upgrade.goldCost)) return false;

        _purchasedUpgrades.Add(upgrade.name);
        ApplyUpgrade(upgrade);
        SpawnOverlaySprite(upgrade);
        OnUpgradePurchased?.Invoke(upgrade);
        Debug.Log($"[Upgrades] Purchased: {upgrade.upgradeName}");
        return true;
    }

    private void ApplyUpgrade(UpgradeData upgrade)
    {
        switch (upgrade.effect)
        {
            // ── Sawmill ───────────────────────────────────────────────
            case UpgradeEffect.MillingSpeed:
                if (sawmill) sawmill.millingSpeedMultiplier *= upgrade.effectValue;
                break;
            case UpgradeEffect.BoardsPerLog:
                if (sawmill) sawmill.boardsPerLogBonus += Mathf.RoundToInt(upgrade.effectValue);
                break;
            case UpgradeEffect.UnlockLiveEdge:
                if (sawmill) sawmill.canProduceLiveEdge = true;
                break;
            case UpgradeEffect.UnlockTurningBlanks:
                if (sawmill) sawmill.canProduceTurningBlanks = true;
                break;
            case UpgradeEffect.ExpandLogQueue:
                if (sawmill) sawmill.logQueueCapacity += Mathf.RoundToInt(upgrade.effectValue);
                break;

            // ── Kiln ─────────────────────────────────────────────────
            case UpgradeEffect.KilnSlots:
                if (kiln) kiln.slotCapacity += Mathf.RoundToInt(upgrade.effectValue);
                break;
            case UpgradeEffect.ReduceChecking:
                if (kiln) kiln.checkingChance *= upgrade.effectValue;
                break;
            case UpgradeEffect.UnlockHumidityChambr:
                if (kiln) kiln.spaltingChance = 0.08f;
                break;

            // ── Surfacing ─────────────────────────────────────────────
            case UpgradeEffect.UnlockJointer:
                if (surfacing) surfacing.hasJointer = true;
                break;
            case UpgradeEffect.UnlockPlaner:
                if (surfacing) surfacing.hasPlaner = true;
                break;
            case UpgradeEffect.UnlockDrumSander:
                if (surfacing) surfacing.hasDrumSander = true;
                break;
            case UpgradeEffect.UnlockWideBeltSander:
                if (surfacing) surfacing.hasWideBeltSander = true;
                break;
            case UpgradeEffect.UnlockCNCRouter:
                if (surfacing) surfacing.hasCNCRouter = true;
                break;
            case UpgradeEffect.SurfacingRarityBonus:
                if (surfacing) surfacing.rarityBonus += upgrade.effectValue;
                break;
            case UpgradeEffect.SurfacingSpeed:
                if (surfacing) surfacing.speedMultiplier *= upgrade.effectValue;
                break;

            // ── Market ───────────────────────────────────────────────
            case UpgradeEffect.WholesaleAccess:
                // UI unlocks the wholesale button — market building already has the logic
                break;

            default:
                Debug.LogWarning($"[Upgrades] Unhandled upgrade effect: {upgrade.effect}");
                break;
        }
    }

    public List<UpgradeData> GetAvailableUpgrades()
    {
        var available = new List<UpgradeData>();
        foreach (var u in allUpgrades)
        {
            if (!IsUnlocked(u) && u.CanPurchase())
                available.Add(u);
        }
        return available;
    }

    public List<UpgradeData> GetAllUpgradesForBuilding(BuildingTarget target)
    {
        var list = new List<UpgradeData>();
        foreach (var u in allUpgrades)
            if (u.targetBuilding == target) list.Add(u);
        return list;
    }

    // ── Visual Overlay Spawning ───────────────────────────────────────

    private void SpawnOverlaySprite(UpgradeData upgrade)
    {
        if (upgrade.buildingOverlaySprite == null) return;

        // Find the parent building GameObject for this upgrade
        GameObject parentBuilding = GetBuildingObject(upgrade.targetBuilding);
        if (parentBuilding == null)
        {
            Debug.LogWarning($"[Upgrades] No building GameObject found for {upgrade.targetBuilding} — overlay not spawned.");
            return;
        }

        // Find offset for this upgrade
        Vector2 offset = Vector2.zero;
        foreach (var o in overlayOffsets)
            if (o.upgradeName == upgrade.upgradeName) { offset = o.offset; break; }

        // Spawn overlay as child of building
        var go = new GameObject($"Overlay_{upgrade.upgradeName}");
        go.transform.SetParent(parentBuilding.transform);
        go.transform.localPosition = new Vector3(offset.x, offset.y, -0.1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = upgrade.buildingOverlaySprite;
        sr.sortingOrder = 2;

        // Register with ObstacleRegistry so it blocks future placement
        if (ObstacleRegistry.Instance != null)
        {
            Vector2 worldPos = (Vector2)go.transform.position;
            Vector2 spriteSize = new Vector2(
                upgrade.buildingOverlaySprite.rect.width / upgrade.buildingOverlaySprite.pixelsPerUnit,
                upgrade.buildingOverlaySprite.rect.height / upgrade.buildingOverlaySprite.pixelsPerUnit
            );
            ObstacleRegistry.Instance.RegisterBounds(worldPos, spriteSize, ObstacleType.Building, go);
        }

        _spawnedOverlays[upgrade.name] = go;
        Debug.Log($"[Upgrades] Spawned overlay for {upgrade.upgradeName} on {parentBuilding.name}");
    }

    private GameObject GetBuildingObject(BuildingTarget target)
    {
        return target switch
        {
            BuildingTarget.Sawmill   => sawmill != null ? sawmill.gameObject : null,
            BuildingTarget.Kiln      => kiln != null ? kiln.gameObject : null,
            BuildingTarget.Surfacing => surfacing != null ? surfacing.gameObject : null,
            BuildingTarget.Market    => market != null ? market.gameObject : null,
            _ => null
        };
    }

    // ── Save/Load ─────────────────────────────────────────────────────
    public string[] GetSaveData() => new List<string>(_purchasedUpgrades).ToArray();

    public void LoadSaveData(string[] purchasedNames)
    {
        _purchasedUpgrades.Clear();
        foreach (var name in purchasedNames)
        {
            _purchasedUpgrades.Add(name);
            // Re-apply upgrade effects and visuals
            foreach (var upgrade in allUpgrades)
            {
                if (upgrade.name == name)
                {
                    ApplyUpgrade(upgrade);
                    SpawnOverlaySprite(upgrade);
                    break;
                }
            }
        }
    }
}

/// <summary>
/// Maps an upgrade name to a world-space offset from its parent building.
/// Configure these in the UpgradeManager Inspector to position each
/// overlay sprite correctly relative to the building it belongs to.
/// </summary>
[System.Serializable]
public class UpgradeOverlayOffset
{
    public string upgradeName;      // Must match UpgradeData.upgradeName exactly
    public Vector2 offset;          // Local offset from parent building center
}
