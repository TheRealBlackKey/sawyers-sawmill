using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// The forest zone. Manages a grid of tree slots that can hold different species.
/// Handles auto-harvest when workers are available, and player-initiated planting.
/// </summary>
public class ForestZone : MonoBehaviour
{
    public static event Action<WoodSpeciesData> OnSpeciesUnlocked;

    [Header("Layout")]
    [SerializeField] private int initialTreeSlots = 6;
    [SerializeField] private float slotSpacing = 48f;       // Pixels between tree slots
    [SerializeField] private GameObject treeSlotPrefab;
    [SerializeField] private Transform treeContainer;

    [Header("Auto-Harvest")]
    [SerializeField] private bool autoHarvestEnabled = true;
    [SerializeField] private float autoHarvestCheckInterval = 3f;

    [Header("Default Starting Species")]
    [SerializeField] private WoodSpeciesData starterSpecies;   // Pine

    [Header("All Species (assign in inspector)")]
    [SerializeField] private WoodSpeciesData[] allSpecies;

    // ── State ─────────────────────────────────────────────────────────
    private List<TreeComponent> _treeSlots = new List<TreeComponent>();
    private List<WoodSpeciesData> _unlockedSpecies = new List<WoodSpeciesData>();
    private WoodSpeciesData _selectedSpeciesForPlanting;
    private float _autoHarvestTimer;

    private void Start()
    {
        // Unlock starter species
        if (starterSpecies != null)
        {
            UnlockSpecies(starterSpecies);
            _selectedSpeciesForPlanting = starterSpecies;
        }

        // Initialize tree slots only if prefab is assigned
        if (treeSlotPrefab != null)
            InitializeSlots(initialTreeSlots);
        else
            Debug.LogWarning("[ForestZone] treeSlotPrefab not assigned — skipping slot initialization. Assign it in the Inspector.");

        // Subscribe to events
        TreeComponent.OnTreeReady += OnTreeReady;
        GameManager.OnItemProduced += CheckSpeciesUnlocks;
    }

    private void OnDestroy()
    {
        TreeComponent.OnTreeReady -= OnTreeReady;
        GameManager.OnItemProduced -= CheckSpeciesUnlocks;
    }

    private void Update()
    {
        if (autoHarvestEnabled)
        {
            _autoHarvestTimer += Time.deltaTime;
            if (_autoHarvestTimer >= autoHarvestCheckInterval)
            {
                _autoHarvestTimer = 0f;
                AutoHarvestReady();
                AutoPlantEmpty();
            }
        }
    }

    // ── Slot Management ───────────────────────────────────────────────
    private void InitializeSlots(int count)
    {
        for (int i = 0; i < count; i++)
            AddTreeSlot();

        // Plant starter trees
        foreach (var slot in _treeSlots)
            slot.Plant(starterSpecies);
    }

    public TreeComponent AddTreeSlot()
    {
        Vector3 pos = transform.position + Vector3.right * (_treeSlots.Count * slotSpacing);
        var slotGO = Instantiate(treeSlotPrefab, pos, Quaternion.identity, treeContainer);
        var tree = slotGO.GetComponent<TreeComponent>();
        _treeSlots.Add(tree);
        return tree;
    }

    public void ExpandSlots(int additionalSlots)
    {
        for (int i = 0; i < additionalSlots; i++)
            AddTreeSlot();
    }

    // ── Planting ──────────────────────────────────────────────────────
    /// <summary>Called when player clicks on an empty tree slot.</summary>
    public void PlantInSlot(TreeComponent slot)
    {
        if (!slot.IsEmpty) return;
        if (_selectedSpeciesForPlanting == null) return;
        slot.Plant(_selectedSpeciesForPlanting);
    }

    public void SelectSpeciesForPlanting(WoodSpeciesData species)
    {
        if (_unlockedSpecies.Contains(species))
            _selectedSpeciesForPlanting = species;
    }

    // ── Auto-behaviors ────────────────────────────────────────────────
    private void AutoHarvestReady()
    {
        // Check if there's storage capacity before harvesting
        int currentLogs = InventoryManager.Instance?.Count(InventoryManager.InventoryZone.ForestStockpile) ?? 0;
        if (currentLogs >= 20) return; // Don't overflow the stockpile

        foreach (var tree in _treeSlots)
        {
            if (tree.IsReadyToHarvest)
            {
                tree.Harvest();
                break; // Harvest one per tick to avoid flood
            }
        }
    }

    private void AutoPlantEmpty()
    {
        if (_selectedSpeciesForPlanting == null) return;

        foreach (var tree in _treeSlots)
        {
            if (tree.State == TreeState.Empty)
            {
                tree.Plant(_selectedSpeciesForPlanting);
                break; // Plant one per tick
            }
        }
    }

    // ── Species Unlock System ─────────────────────────────────────────
    private void CheckSpeciesUnlocks(LumberItem item)
    {
        // Don't check on every item — only logs
        if (item.itemType != LumberItemType.Log) return;

        foreach (var species in allSpecies)
        {
            if (_unlockedSpecies.Contains(species)) continue;
            if (species.totalLogsToUnlock == 0) continue;

            int harvested = GameManager.Instance?.GetLogsHarvested(species) ?? 0;
            // Actually check against a general counter
            int totalHarvested = GetTotalLogsHarvested();

            if (totalHarvested >= species.totalLogsToUnlock && species.unlockCost == 0)
            {
                UnlockSpecies(species);
            }
        }
    }

    private int GetTotalLogsHarvested()
    {
        int total = 0;
        if (GameManager.Instance == null) return 0;
        foreach (var kvp in GameManager.Instance.TotalLogsHarvested)
            total += kvp.Value;
        return total;
    }

    public void UnlockSpecies(WoodSpeciesData species)
    {
        if (species == null || _unlockedSpecies.Contains(species)) return;
        _unlockedSpecies.Add(species);
        OnSpeciesUnlocked?.Invoke(species);
        Debug.Log($"[Forest] Unlocked species: {species.speciesName}");
    }

    /// <summary>Purchase unlock of a species that requires gold.</summary>
    public bool PurchaseSpeciesUnlock(WoodSpeciesData species)
    {
        if (_unlockedSpecies.Contains(species)) return false;
        if (species.unlockCost > 0 && !GameManager.Instance.SpendGold(species.unlockCost)) return false;
        UnlockSpecies(species);
        return true;
    }

    private void OnTreeReady(TreeComponent tree)
    {
        // Visual notification handled by HUD
        // Auto-harvest happens on next interval tick
    }

    // ── Accessors ─────────────────────────────────────────────────────
    public List<WoodSpeciesData> UnlockedSpecies => new List<WoodSpeciesData>(_unlockedSpecies);
    public WoodSpeciesData SelectedSpecies => _selectedSpeciesForPlanting;
    public int TotalSlots => _treeSlots.Count;
    public int ReadyToHarvestCount => _treeSlots.FindAll(t => t.IsReadyToHarvest).Count;
    public int GrowingCount => _treeSlots.FindAll(t => t.State == TreeState.Sapling || t.State == TreeState.YoungTree).Count;
    public int EmptyCount => _treeSlots.FindAll(t => t.IsEmpty).Count;
    public bool AutoHarvestEnabled => autoHarvestEnabled;
    public void SetAutoHarvest(bool enabled) => autoHarvestEnabled = enabled;
}
