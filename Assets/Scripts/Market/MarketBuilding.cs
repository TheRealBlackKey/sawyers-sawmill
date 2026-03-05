using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Sawmill.UI;

/// <summary>
/// The market building. Auto-sells items from the MarketReady zone,
/// handles market day price spikes, custom orders, and the wholesale channel.
///
/// FIX (Session 5): InputPosition is now set via Initialize(worldPosition)
/// called by PlacementManager, not derived in Start() from transform which
/// could be (0,0,0) before the position was applied.
/// </summary>
public class MarketBuilding : MonoBehaviour
{
    public static event Action<LumberItem, float> OnSaleComplete;
    public static event Action<CustomOrder> OnCustomOrderReceived;
    public static event Action<CustomOrder> OnCustomOrderFulfilled;

    [Header("Positions")]
    public Vector3 InputPosition;
    [SerializeField] private float inputOffsetX = 0f;

    [Header("Sale Settings")]
    [SerializeField] private float autoSellInterval  = 5f;
    [SerializeField] private float wholesaleDiscount  = 0.6f;
    [SerializeField] private bool  autoSellEnabled    = true;

    [Header("Custom Orders")]
    [SerializeField] private float customOrderCheckInterval = 30f;
    [SerializeField] private int   maxActiveOrders          = 3;
    [SerializeField] private WoodSpeciesData[] availableSpecies;

    // ── State ─────────────────────────────────────────────────────────
    private List<CustomOrder> _activeOrders  = new List<CustomOrder>();
    private bool              _initialized   = false;

    [Header("Visual")]
    [SerializeField] private Animator _animator;

    // ── Initialization ────────────────────────────────────────────────

    /// <summary>
    /// Called by PlacementManager immediately after placing this building.
    /// </summary>
    public void Initialize(Vector3 worldPosition)
    {
        InputPosition = new Vector3(worldPosition.x + inputOffsetX, worldPosition.y, 0f);
        _initialized  = true;

        Debug.Log($"[Market] Initialized at {worldPosition} — InputPosition: {InputPosition}");
    }

    private void Start()
    {
        // Fall back if not placed via PlacementManager
        if (!_initialized)
        {
            Initialize(transform.position);
        }

        GameManager.OnMarketDayBegin += OnMarketDayBegin;
        GameManager.OnMarketDayEnd   += OnMarketDayEnd;

        StartCoroutine(AutoSellLoop());
        StartCoroutine(CustomOrderLoop());
    }

    private void OnDestroy()
    {
        GameManager.OnMarketDayBegin -= OnMarketDayBegin;
        GameManager.OnMarketDayEnd   -= OnMarketDayEnd;
    }

    // ── Auto-Sell ─────────────────────────────────────────────────────
    private IEnumerator AutoSellLoop()
    {
        while (true)
        {
            if (!autoSellEnabled)
            {
                yield return new WaitForSeconds(1f); // Check occasionally if re-enabled
                continue;
            }

            yield return new WaitForSeconds(autoSellInterval);

            var inv = InventoryManager.Instance;
            var gm  = GameManager.Instance;

            if (inv == null || gm == null) continue;

            // First try to fill active custom orders
            foreach (var order in new List<CustomOrder>(_activeOrders))
            {
                var match = FindItemForOrder(order);
                if (match != null)
                {
                    SellItem(match, order.rewardMultiplier, true);
                    order.itemsFulfilled++;
                    if (order.itemsFulfilled >= order.quantityRequired)
                    {
                        _activeOrders.Remove(order);
                        gm.AddReputation(order.reputationReward);
                        OnCustomOrderFulfilled?.Invoke(order);
                        Debug.Log($"[Market] Custom order fulfilled: {order.description}");
                    }
                    break;
                }
            }

            // Then auto-sell anything in the market-ready zone
            if (inv.HasItems(InventoryManager.InventoryZone.MarketReady))
            {
                var item = inv.Dequeue(InventoryManager.InventoryZone.MarketReady);
                if (item != null)
                    SellItem(item, 1f, false);
            }
        }
    }

    private void SellItem(LumberItem item, float multiplier, bool isCustomOrder)
    {
        var inv = InventoryManager.Instance;
        var gm  = GameManager.Instance;
        if (gm == null) return;

        inv?.RemoveItem(item, InventoryManager.InventoryZone.MarketReady);

        float baseValue      = item.CurrentMarketValue;
        float saleMultiplier = gm.GetSaleMultiplier(item.species) * multiplier;
        float finalValue     = baseValue * saleMultiplier;

        gm.EarnGold(finalValue);
        gm.RegisterItemSold(item, finalValue);
        OnSaleComplete?.Invoke(item, finalValue);

        Debug.Log($"[Market] Sold: {item.DisplayName} for {finalValue:F2}g (x{saleMultiplier:F2})");

        // Offset the text exactly to the top center of the massive market building
        float gridOffset = WorldGrid.Instance != null ? WorldGrid.Instance.CellSize : 32f;
        Vector3 textPos = transform.position + new Vector3(0f, gridOffset * 0.8f, 0f);

        WorldTextManager.Instance?.SpawnStaticText(textPos, $"+${finalValue:F0}", new Color(0.9f, 0.8f, 0.2f));
    }

    // ── Custom Orders ─────────────────────────────────────────────────
    private IEnumerator CustomOrderLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(customOrderCheckInterval);

            if (_activeOrders.Count < maxActiveOrders)
            {
                var newOrder = GenerateCustomOrder();
                if (newOrder != null)
                {
                    _activeOrders.Add(newOrder);
                    OnCustomOrderReceived?.Invoke(newOrder);
                    Debug.Log($"[Market] New order: {newOrder.description}");
                }
            }
        }
    }

    private CustomOrder GenerateCustomOrder()
    {
        if (availableSpecies == null || availableSpecies.Length == 0) return null;

        var species = availableSpecies[UnityEngine.Random.Range(0, availableSpecies.Length)];
        var stages  = new[] { ProcessingStage.KilnDried, ProcessingStage.Surfaced, ProcessingStage.Finished };
        var stage   = stages[UnityEngine.Random.Range(0, stages.Length)];
        int qty     = UnityEngine.Random.Range(1, 4);

        GrainVariant requestedGrain = null;
        if (species.grainVariants.Count > 0 && UnityEngine.Random.value < 0.3f)
        {
            var unlocked = species.grainVariants.FindAll(v => v.isUnlocked);
            if (unlocked.Count > 0)
                requestedGrain = unlocked[UnityEngine.Random.Range(0, unlocked.Count)];
        }

        float reputationReward = 5f + (requestedGrain != null ? requestedGrain.rarityTier * 10f : 0f);
        float rewardMultiplier = requestedGrain != null ? 1.5f + requestedGrain.rarityTier * 0.5f : 1.2f;

        string grainNote  = requestedGrain != null ? $" ({requestedGrain.variantName})" : "";
        string stageName  = stage.ToString().Replace("_", " ");

        return new CustomOrder
        {
            description      = $"{qty}x {species.speciesName} {stageName}{grainNote}",
            requiredSpecies  = species,
            requiredStage    = stage,
            requiredGrain    = requestedGrain,
            quantityRequired = qty,
            rewardMultiplier = rewardMultiplier,
            reputationReward = reputationReward,
            expiresAfterDays = 5
        };
    }

    private LumberItem FindItemForOrder(CustomOrder order)
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return null;

        var candidates = inv.GetItems(InventoryManager.InventoryZone.MarketReady);
        foreach (var item in candidates)
        {
            if (item.species     != order.requiredSpecies) continue;
            if (item.stage       != order.requiredStage)   continue;
            if (order.requiredGrain != null && item.grainVariant != order.requiredGrain) continue;
            return item;
        }
        return null;
    }

    // ── Wholesale ─────────────────────────────────────────────────────
    public int WholesaleAll(ProcessingStage stage)
    {
        var inv = InventoryManager.Instance;
        var gm  = GameManager.Instance;
        if (inv == null || gm == null) return 0;

        var items = inv.GetItems(InventoryManager.InventoryZone.MarketReady);
        int sold  = 0;

        foreach (var item in items)
        {
            if (item.stage != stage) continue;
            float value = item.CurrentMarketValue * wholesaleDiscount;
            inv.RemoveItem(item, InventoryManager.InventoryZone.MarketReady);
            gm.EarnGold(value);
            gm.RegisterItemSold(item, value);
            OnSaleComplete?.Invoke(item, value);
            sold++;
        }

        if (sold > 0)
        {
            // Calculate total wholesale value for the floating text
            float totalValue = 0f;
            foreach (var item in items) 
            {
                if (item.stage == stage) totalValue += item.CurrentMarketValue * wholesaleDiscount; 
            }

            float gridOffset = WorldGrid.Instance != null ? WorldGrid.Instance.CellSize : 32f;
            Vector3 textPos = transform.position + new Vector3(0f, gridOffset * 0.8f, 0f);
            WorldTextManager.Instance?.SpawnStaticText(textPos, $"Wholesale +${totalValue:F0}", new Color(0.9f, 0.8f, 0.2f));
        }

        return sold;
    }

    // ── Market Day Events ─────────────────────────────────────────────
    private void OnMarketDayBegin()
    {
        autoSellInterval = 2f;
        Debug.Log("[Market] Market day! Prices are higher, selling faster.");
    }

    private void OnMarketDayEnd()
    {
        autoSellInterval = 5f;
    }

    // ── Accessors ─────────────────────────────────────────────────────
    public List<CustomOrder> ActiveOrders  => new List<CustomOrder>(_activeOrders);
    public bool              IsAutoSelling => autoSellEnabled;
    public void SetAutoSell(bool enabled)  => autoSellEnabled = enabled;
}

// ── Supporting Types ──────────────────────────────────────────────────
[Serializable]
public class CustomOrder
{
    public string           description;
    public WoodSpeciesData  requiredSpecies;
    public ProcessingStage  requiredStage;
    public GrainVariant     requiredGrain;
    public int              quantityRequired;
    public int              itemsFulfilled;
    public float            rewardMultiplier;
    public float            reputationReward;
    public int              expiresAfterDays;
}
