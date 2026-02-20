using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// A tree in the forest zone. Handles growth stages, harvesting, and stump regrowth.
/// Each tree slot in the forest can hold one tree of any unlocked species.
/// </summary>
public class TreeComponent : MonoBehaviour
{
    public static event Action<TreeComponent> OnTreeReady;      // Tree reached full growth
    public static event Action<TreeComponent, int> OnTreeHarvested;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite spriteStump;
    [SerializeField] private Sprite spriteSapling;
    [SerializeField] private Sprite spriteMature;
    [SerializeField] private Sprite spriteHarvest;  // Glow/ready state
    [SerializeField] private GameObject growthParticles;
    [SerializeField] private GameObject harvestParticles;

    // ── State ─────────────────────────────────────────────────────────
    public WoodSpeciesData Species { get; private set; }
    public TreeState State { get; private set; } = TreeState.Empty;
    public bool IsReadyToHarvest => State == TreeState.ReadyToHarvest;
    public bool IsEmpty => State == TreeState.Empty || State == TreeState.Stump;

    private float _growthProgress = 0f;     // 0 to 1
    private float _currentGrowthTime;
    private int _regrowthCount = 0;
    private bool _wasPlantedByPlayer = false;

    // ── Plant a Tree ──────────────────────────────────────────────────
    public void Plant(WoodSpeciesData species, bool playerPlanted = true)
    {
        if (species == null) return;
        Species = species;
        State = TreeState.Sapling;
        _growthProgress = 0f;
        _wasPlantedByPlayer = playerPlanted;
        _currentGrowthTime = species.growthTimeSeconds;
        UpdateSprite();
        StartCoroutine(GrowthRoutine());
    }

    private IEnumerator GrowthRoutine()
    {
        _growthProgress = 0f;

        while (_growthProgress < 1f)
        {
            _growthProgress += Time.deltaTime / _currentGrowthTime;

            // Transition to young tree at 40% growth
            if (_growthProgress >= 0.4f && State == TreeState.Sapling)
            {
                State = TreeState.YoungTree;
                UpdateSprite();
                if (growthParticles != null)
                    Instantiate(growthParticles, transform.position, Quaternion.identity);
            }

            yield return null;
        }

        _growthProgress = 1f;
        State = TreeState.ReadyToHarvest;
        UpdateSprite();
        OnTreeReady?.Invoke(this);
    }

    // ── Harvest ───────────────────────────────────────────────────────
    /// <summary>
    /// Initiates the harvest. Returns the logs produced, adds them to inventory.
    /// </summary>
    public void Harvest()
    {
        if (!IsReadyToHarvest) return;

        int logCount = Species.logsPerLog();
        State = TreeState.Stump;
        UpdateSprite();

        if (harvestParticles != null)
            Instantiate(harvestParticles, transform.position, Quaternion.identity);

        // Produce logs
        var inv = InventoryManager.Instance;
        var gm = GameManager.Instance;

        for (int i = 0; i < logCount; i++)
        {
            var log = new LumberItem(Species, ProcessingStage.Log, LumberItemType.Log);
            inv?.AddItem(log, InventoryManager.InventoryZone.ForestStockpile);
        }

        gm?.RegisterLogHarvested(Species, logCount);
        OnTreeHarvested?.Invoke(this, logCount);

        // Decide whether stump regrows or stays empty
        if (!Species.requiresReplanting)
        {
            _regrowthCount++;
            float regrowthTime = Species.growthTimeSeconds * Species.regrowthMultiplier;
            _currentGrowthTime = regrowthTime;
            StartCoroutine(DelayedRegrowth(3f));  // Stump sits for 3 seconds, then starts regrowing
        }
    }

    private IEnumerator DelayedRegrowth(float delay)
    {
        yield return new WaitForSeconds(delay);
        Plant(Species, false);
    }

    public void ClearTree()
    {
        StopAllCoroutines();
        State = TreeState.Empty;
        Species = null;
        _growthProgress = 0f;
        _regrowthCount = 0;
        UpdateSprite();
    }

    // ── Visuals ───────────────────────────────────────────────────────
    private void UpdateSprite()
    {
        if (_spriteRenderer == null) return;

        switch (State)
        {
            case TreeState.Empty:
                _spriteRenderer.sprite = null;
                _spriteRenderer.color = Color.white;
                break;
            case TreeState.Stump:
                _spriteRenderer.sprite = spriteStump ?? Species?.treeSprite;
                _spriteRenderer.color = new Color(0.6f, 0.4f, 0.2f);
                break;
            case TreeState.Sapling:
                _spriteRenderer.sprite = spriteSapling ?? Species?.treeSprite;
                _spriteRenderer.color = new Color(0.7f, 1f, 0.7f);
                transform.localScale = Vector3.one * 0.3f;
                break;
            case TreeState.YoungTree:
                _spriteRenderer.sprite = Species?.treeSprite;
                _spriteRenderer.color = Color.white;
                transform.localScale = Vector3.one * 0.65f;
                break;
            case TreeState.ReadyToHarvest:
                _spriteRenderer.sprite = spriteHarvest ?? Species?.treeSprite;
                _spriteRenderer.color = Color.white;
                transform.localScale = Vector3.one;
                // Gentle glow pulse handled by shader or animator
                break;
        }
    }

    private void Update()
    {
        // Scale tree as it grows
        if (State == TreeState.Sapling || State == TreeState.YoungTree)
        {
            float scale = Mathf.Lerp(0.3f, 1f, _growthProgress);
            transform.localScale = Vector3.one * scale;
        }
    }

    public float GrowthProgress => _growthProgress;
    public int RegrowthCount => _regrowthCount;
}

// ── Extension: WoodSpeciesData helper ────────────────────────────────
public static class WoodSpeciesExtensions
{
    public static int logsPerLog(this WoodSpeciesData data) => data.logsPerTree;
}

public enum TreeState
{
    Empty,
    Sapling,
    YoungTree,
    ReadyToHarvest,
    Stump
}
