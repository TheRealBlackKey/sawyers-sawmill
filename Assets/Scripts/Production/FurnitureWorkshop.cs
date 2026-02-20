using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// The furniture workshop. Takes surfaced boards and builds furniture items.
/// Products are worth significantly more than raw lumber.
/// Unlocks progressively more complex furniture as reputation grows.
/// </summary>
public class FurnitureWorkshop : MonoBehaviour
{
    public static event Action<FurnitureProduct> OnFurnitureComplete;

    [Header("Building State")]
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private float unlockCost = 500f;
    [SerializeField] private int reputationToUnlock = 50;

    [Header("Furniture Recipes")]
    [SerializeField] private List<FurnitureRecipe> recipes = new List<FurnitureRecipe>();

    [Header("Positions")]
    public Vector3 InputPosition;
    public Vector3 OutputPosition;

    [Header("Visual")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer buildingSprite;
    [SerializeField] private GameObject lockedOverlay;

    private bool _isBuilding = false;
    private FurnitureRecipe _queuedRecipe;
    private List<FurnitureProduct> _completedProducts = new List<FurnitureProduct>();

    private void Start()
    {
        lockedOverlay?.SetActive(!isUnlocked);
        if (isUnlocked)
            StartCoroutine(WorkshopLoop());
    }

    public bool TryUnlock()
    {
        var gm = GameManager.Instance;
        if (gm == null || isUnlocked) return false;
        if (gm.Gold < unlockCost || gm.Reputation < reputationToUnlock) return false;
        if (!gm.SpendGold(unlockCost)) return false;

        isUnlocked = true;
        lockedOverlay?.SetActive(false);
        StartCoroutine(WorkshopLoop());
        Debug.Log("[FurnitureWorkshop] Unlocked!");
        return true;
    }

    // ── Recipe Queue ──────────────────────────────────────────────────
    public bool QueueRecipe(FurnitureRecipe recipe)
    {
        if (!isUnlocked || _isBuilding) return false;
        if (!CanBuildRecipe(recipe)) return false;
        _queuedRecipe = recipe;
        return true;
    }

    public bool CanBuildRecipe(FurnitureRecipe recipe)
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return false;

        var available = inv.GetItems(InventoryManager.InventoryZone.SurfacingOutput);

        // Check we have enough boards of the required types
        foreach (var req in recipe.requirements)
        {
            int count = 0;
            foreach (var item in available)
            {
                if (req.species == null || item.species == req.species)
                    if (item.stage >= req.minimumStage)
                        count++;
            }
            if (count < req.quantity) return false;
        }
        return true;
    }

    // ── Workshop Loop ─────────────────────────────────────────────────
    private IEnumerator WorkshopLoop()
    {
        while (true)
        {
            if (_queuedRecipe != null && !_isBuilding && CanBuildRecipe(_queuedRecipe))
            {
                yield return StartCoroutine(BuildFurniture(_queuedRecipe));
                _queuedRecipe = null;
            }
            else
            {
                // Auto-queue simplest available recipe
                var autoRecipe = GetAutoRecipe();
                if (autoRecipe != null)
                    yield return StartCoroutine(BuildFurniture(autoRecipe));
                else
                    yield return new WaitForSeconds(2f);
            }
        }
    }

    private FurnitureRecipe GetAutoRecipe()
    {
        // Auto-build the simplest recipe that can be fulfilled
        foreach (var recipe in recipes)
        {
            if (!recipe.isUnlocked) continue;
            if (CanBuildRecipe(recipe)) return recipe;
        }
        return null;
    }

    private IEnumerator BuildFurniture(FurnitureRecipe recipe)
    {
        _isBuilding = true;
        _animator?.SetBool("IsBuilding", true);

        // Consume boards from inventory
        var inv = InventoryManager.Instance;
        var consumedItems = new List<LumberItem>();

        foreach (var req in recipe.requirements)
        {
            var available = inv.GetItems(InventoryManager.InventoryZone.SurfacingOutput);
            int needed = req.quantity;
            foreach (var item in available)
            {
                if (needed <= 0) break;
                if (req.species != null && item.species != req.species) continue;
                if (item.stage < req.minimumStage) continue;
                consumedItems.Add(item);
                needed--;
            }
        }

        foreach (var item in consumedItems)
            inv.RemoveItem(item, InventoryManager.InventoryZone.SurfacingOutput);

        // Build time
        yield return new WaitForSeconds(recipe.buildTimeSeconds);

        // Create product
        var product = CreateProduct(recipe, consumedItems);
        _completedProducts.Add(product);
        OnFurnitureComplete?.Invoke(product);

        // Register with game manager and earn gold
        float value = product.marketValue;
        GameManager.Instance?.EarnGold(value);
        GameManager.Instance?.AddReputation(recipe.reputationGain);
        Debug.Log($"[Workshop] Built: {product.productName} worth ${value:F0}");

        _isBuilding = false;
        _animator?.SetBool("IsBuilding", false);
    }

    private FurnitureProduct CreateProduct(FurnitureRecipe recipe, List<LumberItem> materials)
    {
        // Value is based on recipe base + quality of materials used
        float totalMaterialValue = 0f;
        float highestRarity = 0f;
        string primarySpecies = "";
        string primaryGrain = "";

        foreach (var mat in materials)
        {
            totalMaterialValue += mat.CurrentMarketValue;
            if (mat.grainVariant != null && mat.grainVariant.rarityTier > highestRarity)
            {
                highestRarity = mat.grainVariant.rarityTier;
                primaryGrain = mat.grainVariant.variantName + " ";
            }
            if (primarySpecies == "" && mat.species != null)
                primarySpecies = mat.species.speciesName;
        }

        float craftingMultiplier = recipe.craftingValueMultiplier;
        float qualityBonus = 1f + (highestRarity * 0.3f);

        return new FurnitureProduct
        {
            productName = $"{primaryGrain}{primarySpecies} {recipe.productName}",
            recipe = recipe,
            marketValue = (recipe.baseValue + totalMaterialValue) * craftingMultiplier * qualityBonus,
            qualityGrade = (QualityGrade)Mathf.Min((int)highestRarity + 1, (int)QualityGrade.Heirloom)
        };
    }
}

// ── Data Types ────────────────────────────────────────────────────────
[Serializable]
public class FurnitureRecipe
{
    public string productName = "Cutting Board";
    public bool isUnlocked = true;
    public float buildTimeSeconds = 60f;
    public float baseValue = 30f;
    public float craftingValueMultiplier = 3f;  // Value multiply vs raw materials
    public float reputationGain = 2f;
    public List<MaterialRequirement> requirements = new List<MaterialRequirement>();
    public int reputationToUnlock = 0;
    public Sprite productIcon;
}

[Serializable]
public class MaterialRequirement
{
    public WoodSpeciesData species;          // Null = any species
    public int quantity = 1;
    public ProcessingStage minimumStage = ProcessingStage.Surfaced;
}

[Serializable]
public class FurnitureProduct
{
    public string productName;
    public FurnitureRecipe recipe;
    public float marketValue;
    public QualityGrade qualityGrade;
}
