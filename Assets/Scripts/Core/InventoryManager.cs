using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central inventory. Each production building reads/writes here.
/// Uses a staging model: items live in zone-specific queues.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────
    public static event Action<LumberItem, InventoryZone> OnItemAdded;
    public static event Action<LumberItem, InventoryZone> OnItemRemoved;
    public static event Action<InventoryZone> OnZoneChanged;

    // ── Storage Zones (mimic the physical layout of the mill) ─────────
    public enum InventoryZone
    {
        ForestStockpile,        // Logs waiting to be milled
        MillInput,              // Logs queued at the saw
        MillOutput,             // Rough sawn boards, just milled
        KilnInput,              // Boards queued for drying
        KilnOutput,             // Kiln-dried boards
        SurfacingInput,         // Dried boards queued for surfacing
        SurfacingOutput,        // Surfaced boards ready for finishing or sale
        FinishingInput,         // Boards queued for finish application
        FurnitureWorkshop,      // Components waiting for furniture build
        TurningStudio,          // Blanks queued for lathe
        Cooperage,              // Staves/components for barrels
        MarketReady,            // Items staged for sale
        Sold                    // Archive (kept briefly for stats)
    }

    [Header("Capacity Limits (0 = unlimited)")]
    [SerializeField] private int defaultZoneCapacity = 0;

    private Dictionary<InventoryZone, List<LumberItem>> _inventory
        = new Dictionary<InventoryZone, List<LumberItem>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize all zones
        foreach (InventoryZone zone in Enum.GetValues(typeof(InventoryZone)))
            _inventory[zone] = new List<LumberItem>();
    }

    // ── Core Operations ───────────────────────────────────────────────
    public bool AddItem(LumberItem item, InventoryZone zone)
    {
        if (item == null) return false;

        int cap = defaultZoneCapacity;
        if (cap > 0 && _inventory[zone].Count >= cap)
        {
            Debug.LogWarning($"[Inventory] Zone {zone} is full!");
            return false;
        }

        _inventory[zone].Add(item);
        OnItemAdded?.Invoke(item, zone);
        OnZoneChanged?.Invoke(zone);
        return true;
    }

    public bool RemoveItem(LumberItem item, InventoryZone zone)
    {
        if (_inventory[zone].Remove(item))
        {
            OnItemRemoved?.Invoke(item, zone);
            OnZoneChanged?.Invoke(zone);
            return true;
        }
        return false;
    }

    /// <summary>Moves an item from one zone to another atomically.</summary>
    public bool TransferItem(LumberItem item, InventoryZone from, InventoryZone to)
    {
        if (!_inventory[from].Contains(item)) return false;
        _inventory[from].Remove(item);
        _inventory[to].Add(item);
        OnItemRemoved?.Invoke(item, from);
        OnItemAdded?.Invoke(item, to);
        OnZoneChanged?.Invoke(from);
        OnZoneChanged?.Invoke(to);
        return true;
    }

    // ── Queries ───────────────────────────────────────────────────────
    public List<LumberItem> GetItems(InventoryZone zone)
        => new List<LumberItem>(_inventory[zone]);

    public int Count(InventoryZone zone) => _inventory[zone].Count;

    public bool HasItems(InventoryZone zone) => _inventory[zone].Count > 0;

    /// <summary>Peek at the oldest item in a zone without removing.</summary>
    public LumberItem Peek(InventoryZone zone)
        => _inventory[zone].Count > 0 ? _inventory[zone][0] : null;

    /// <summary>Dequeue the oldest item (FIFO).</summary>
    public LumberItem Dequeue(InventoryZone zone)
    {
        if (_inventory[zone].Count == 0) return null;
        var item = _inventory[zone][0];
        _inventory[zone].RemoveAt(0);
        OnItemRemoved?.Invoke(item, zone);
        OnZoneChanged?.Invoke(zone);
        return item;
    }

    /// <summary>Get all items matching a species across a zone.</summary>
    public List<LumberItem> GetBySpecies(InventoryZone zone, WoodSpeciesData species)
        => _inventory[zone].Where(i => i.species == species).ToList();

    /// <summary>Count total market-ready items by stage for the market UI.</summary>
    public Dictionary<ProcessingStage, int> GetMarketSummary()
    {
        var summary = new Dictionary<ProcessingStage, int>();
        foreach (ProcessingStage s in Enum.GetValues(typeof(ProcessingStage)))
            summary[s] = 0;

        foreach (var item in _inventory[InventoryZone.MarketReady])
            summary[item.stage]++;

        return summary;
    }

    public int GetTotalItemCount()
        => _inventory.Values.Sum(list => list.Count);
}
