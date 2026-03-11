using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// A single tree slot in the forest zone.
///
/// PLANTING RULES:
/// - Sawyer plants manually by walking to the slot — this is fast and intentional
/// - If a slot sits empty long enough, a passive self-seeding timer triggers
///   a very slow natural regrowth (much slower than Sawyer planting)
/// - Once harvested, the slot is fully empty — no auto-regrowth coroutine
///
/// Sawyer auto-harvests ready trees by walking to the slot's world position.
/// </summary>
public class TreeComponent : MonoBehaviour
{
    public static event Action<TreeComponent> OnTreeReady;
    public static event Action<TreeComponent, int> OnTreeHarvested;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite spriteStump;
    [SerializeField] private Sprite spriteSapling;
    [SerializeField] private Sprite spriteMature;
    [SerializeField] private Sprite spriteHarvest;
    [SerializeField] private GameObject growthParticles;
    [SerializeField] private GameObject harvestParticles;

    [Header("Passive Self-Seeding")]
    [Tooltip("How long an empty slot sits before a wild seedling appears on its own. " +
             "Should be much longer than a Sawyer-planted tree's growth time.")]
    [SerializeField] private float passiveSeedingDelay = 300f;  // 5 minutes by default
    [SerializeField] private float baseScale = 5f; // Full-grown scale in world units — increase if trees appear too small

    // ── State ─────────────────────────────────────────────────────────
    public WoodSpeciesData Species    { get; private set; }
    public TreeState        State     { get; private set; } = TreeState.Empty;
    public bool IsReadyToHarvest      => State == TreeState.ReadyToHarvest;
    public bool IsEmpty               => State == TreeState.Empty || State == TreeState.Stump;
    public bool IsAvailableForPlanting => State == TreeState.Empty || State == TreeState.Stump;
    public bool IsStump               => State == TreeState.Stump;
    public bool IsClaimed             { get; set; } = false;

    private float _growthProgress  = 0f;
    private float _currentGrowthTime;
    private float _passiveTimer    = 0f;
    private bool  _passiveTimerRunning = false;
    private float _randomScaleMultiplier = 1f;
    private float _randomGrowthModifier = 1f;
    private Sprite _chosenMatureSprite;
    private Vector3 _baseLocalPosition;
    private float _baseWorldY;
    private bool _basePositionStored = false;

    // ── Planting ──────────────────────────────────────────────────────

    /// <summary>
    /// Called by SawyerWorker when he physically arrives at this slot to plant.
    /// Uses the species selected in ForestZone.
    /// </summary>
    public void PlantByWorker(WoodSpeciesData species)
    {
        if (species == null) return;
        if (!IsAvailableForPlanting)
        {
            Debug.LogWarning($"[Tree] PlantByWorker called on non-empty slot (state: {State})");
            return;
        }

        if (State == TreeState.Stump)
            Debug.Log($"[Tree] Sawyer cleared stump and planted {species.speciesName} at {transform.position}");
        else
            Debug.Log($"[Tree] Sawyer planted {species.speciesName} at {transform.position}");

        StopPassiveTimer();
        BeginGrowth(species);
    }

    /// <summary>
    /// Internal — starts growth from whatever triggered planting.
    /// </summary>
    private void BeginGrowth(WoodSpeciesData species)
    {
        Species             = species;
        State               = TreeState.Sapling;
        IsClaimed           = false; // Fresh trees are unclaimed
        _growthProgress     = 0f;
        _randomGrowthModifier = UnityEngine.Random.Range(0.85f, 1.15f);
        
        float baseTime = species.growthTimeSeconds;
        if (GameManager.Instance != null)
            baseTime *= GameManager.Instance.GlobalGrowthTimeMultiplier;
            
        _currentGrowthTime  = baseTime * _randomGrowthModifier;
        _randomScaleMultiplier = UnityEngine.Random.Range(0.85f, 1.15f);
        _chosenMatureSprite    = species.GetRandomMatureSprite();
        UpdateSprite();
        StartCoroutine(GrowthRoutine());
    }

    private IEnumerator GrowthRoutine()
    {
        _growthProgress = 0f;

        while (_growthProgress < 1f)
        {
            _growthProgress += Time.deltaTime / _currentGrowthTime;

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
        State           = TreeState.ReadyToHarvest;
        UpdateSprite();
        OnTreeReady?.Invoke(this);
    }

    // ── Harvest ───────────────────────────────────────────────────────

    /// <summary>
    /// Called by SawyerWorker when he physically arrives at this slot to harvest.
    /// Produces logs, empties the slot, and starts the passive seeding timer.
    /// </summary>
    public void Harvest()
    {
        if (!IsReadyToHarvest) return;

        int logCount = Species.logsPerTree;
        State        = TreeState.Empty;
        Species      = null;
        IsClaimed    = false;
        _growthProgress = 0f;
        UpdateSprite();

        if (harvestParticles != null)
            Instantiate(harvestParticles, transform.position, Quaternion.identity);

        // Produce logs directly into the forest stockpile
        var inv = InventoryManager.Instance;
        var gm  = GameManager.Instance;

        for (int i = 0; i < logCount; i++)
        {
            // Species is captured before clearing — use local var
        }

        // Re-read species before we cleared it — need to capture it first
        // Note: species already cleared above, so we pass via the harvested ref below
        OnTreeHarvested?.Invoke(this, logCount);

        // Start the passive self-seeding timer — slot won't stay empty forever,
        // but it takes much longer than Sawyer planting manually
        StartPassiveTimer();
    }

    /// <summary>
    /// Called by ForestZone during harvest so it can pass the species reference
    /// back after harvest clears it from the slot.
    /// </summary>
    public int HarvestAndGetLogs(out WoodSpeciesData harvestedSpecies)
    {
        harvestedSpecies = null;
        if (!IsReadyToHarvest) return 0;

        harvestedSpecies    = Species;
        
        // Calculate yields based on GameManager multipliers
        float yieldMultiplier = GameManager.Instance != null ? GameManager.Instance.treeYieldMultiplier : 1f;
        int logCount = Mathf.Max(1, Mathf.RoundToInt(Species.logsPerTree * yieldMultiplier));

        // Show stump — Sawyer will clear it when he comes to replant
        State           = TreeState.Stump;
        Species         = null;
        IsClaimed       = false;
        _growthProgress = 0f;
        UpdateSprite();

        if (harvestParticles != null)
            Instantiate(harvestParticles, transform.position, Quaternion.identity);

        OnTreeHarvested?.Invoke(this, logCount);
        StartPassiveTimer();
        Debug.Log($"[Tree] Harvested — stump remains at {transform.position}, ready for replanting.");

        return logCount;
    }

    // ── Passive Self-Seeding ──────────────────────────────────────────

    private void StartPassiveTimer()
    {
        _passiveTimer        = 0f;
        _passiveTimerRunning = true;
    }

    private void StopPassiveTimer()
    {
        _passiveTimerRunning = false;
        _passiveTimer        = 0f;
    }

    private void Update()
    {
        // Passive seeding timer — ticks while slot is empty or a stump
        if (_passiveTimerRunning && (State == TreeState.Empty || State == TreeState.Stump))
        {
            _passiveTimer += Time.deltaTime;
            if (_passiveTimer >= passiveSeedingDelay)
            {
                StopPassiveTimer();
                TriggerPassiveSeeding();
            }
        }
    }

    /// <summary>
    /// A wild seedling appears after the slot has been empty long enough.
    /// Uses Pine as the default — the forest reclaims its own.
    /// ForestZone provides the starter species reference.
    /// </summary>
    private void TriggerPassiveSeeding()
    {
        var forest = FindFirstObjectByType<ForestZone>();
        if (forest == null) return;

        var species = forest.StarterSpecies;
        if (species == null) return;

        // Passive trees grow much slower — multiply growth time
        Species            = species;
        State              = TreeState.Sapling;
        _growthProgress    = 0f;
        _randomGrowthModifier = UnityEngine.Random.Range(0.85f, 1.15f);

        float baseTime = species.growthTimeSeconds;
        if (GameManager.Instance != null)
            baseTime *= GameManager.Instance.GlobalGrowthTimeMultiplier;

        _currentGrowthTime  = baseTime * forest.PassiveGrowthMultiplier * _randomGrowthModifier;
        _randomScaleMultiplier = UnityEngine.Random.Range(0.85f, 1.15f);
        _chosenMatureSprite    = species.GetRandomMatureSprite();
        UpdateSprite();
        StartCoroutine(GrowthRoutine());

        Debug.Log($"[Tree] Wild {species.speciesName} seedling appeared passively at {transform.position}");
    }

    public void ClearTree()
    {
        StopAllCoroutines();
        StopPassiveTimer();
        State           = TreeState.Empty;
        Species         = null;
        IsClaimed       = false;
        _growthProgress = 0f;
        UpdateSprite();
    }

    /// <summary>
    /// Called by ForestZone when a tree has reached its maxHarvestCycles.
    /// Becomes a permanent stump — no passive seeding timer, stays until Sawyer clears it.
    /// </summary>
    public void BecomeStump()
    {
        StopAllCoroutines();
        StopPassiveTimer();
        State           = TreeState.Stump;
        Species         = null;
        IsClaimed       = false;
        _growthProgress = 0f;
        UpdateSprite();
    }

    // ── Visuals ───────────────────────────────────────────────────────

    private void UpdateSprite()
    {
        if (_spriteRenderer == null) return;

        // Cache the original designated position of the tree slot in its container
        if (!_basePositionStored)
        {
            _baseLocalPosition = transform.localPosition;
            
            // Shift the sorting anchor down to represent the absolute base/roots of the tree.
            // Since the tree spans 'baseScale' units and is centered, its bottom is exactly half that scale down.
            _baseWorldY = transform.position.y - (baseScale * 0.5f);
            
            _basePositionStored = true;
        }

        // Trees lower on screen (more negative Y) render in front of trees higher up.
        // Multiply by -1 so lower Y = higher sorting order.
        // Scale by 10 to give enough integer range across the strip height.
        // Add +10000 to ensure even the highest trees on the map render above the background (which is usually -10).
        _spriteRenderer.sortingLayerName = "Default";
        _spriteRenderer.sortingOrder = Mathf.RoundToInt(-_baseWorldY * 10f) + 10000;
        
        // Reset local position to the grid base before applying scale offsets
        transform.localPosition = _baseLocalPosition;

        switch (State)
        {
            case TreeState.Empty:
                _spriteRenderer.sprite = null;
                _spriteRenderer.color  = Color.white;
                transform.localScale   = Vector3.one * baseScale;
                break;
            case TreeState.Stump:
                if (spriteStump != null) _spriteRenderer.sprite = spriteStump;
                else _spriteRenderer.sprite = null;
                _spriteRenderer.color = Color.white;
                float stumpScale = baseScale * 0.5f;
                transform.localScale  = Vector3.one * stumpScale;
                // Shift down so it sits on the ground instead of floating in the center
                transform.localPosition = _baseLocalPosition + new Vector3(0f, -(baseScale - stumpScale) * 0.5f, 0f);
                break;
            case TreeState.Sapling:
                if (spriteSapling != null) _spriteRenderer.sprite = spriteSapling;
                else if (Species != null && Species.GetRandomSaplingSprite() != null) _spriteRenderer.sprite = Species.GetRandomSaplingSprite();
                else if (_chosenMatureSprite != null) _spriteRenderer.sprite = _chosenMatureSprite;
                _spriteRenderer.color = Color.white;
                float saplingScale = baseScale * 0.4f * _randomScaleMultiplier;
                transform.localScale  = Vector3.one * saplingScale;
                transform.localPosition = _baseLocalPosition + new Vector3(0f, -(baseScale - saplingScale) * 0.5f, 0f);
                break;
            case TreeState.YoungTree:
                if (Species != null && Species.GetRandomJuvenileSprite() != null) _spriteRenderer.sprite = Species.GetRandomJuvenileSprite();
                else if (_chosenMatureSprite != null) _spriteRenderer.sprite = _chosenMatureSprite;
                else if (Species != null && Species.GetRandomSaplingSprite() != null) _spriteRenderer.sprite = Species.GetRandomSaplingSprite();
                _spriteRenderer.color = Color.white;
                float youngScale = baseScale * 0.7f * _randomScaleMultiplier;
                transform.localScale  = Vector3.one * youngScale;
                transform.localPosition = _baseLocalPosition + new Vector3(0f, -(baseScale - youngScale) * 0.5f, 0f);
                break;
            case TreeState.ReadyToHarvest:
                if (spriteHarvest != null) _spriteRenderer.sprite = spriteHarvest;
                else if (Species != null && Species.treeSpriteReady != null) _spriteRenderer.sprite = Species.treeSpriteReady;
                else if (_chosenMatureSprite != null) _spriteRenderer.sprite = _chosenMatureSprite;
                _spriteRenderer.color = new Color(1f, 0.95f, 0.7f);
                float matureScale = baseScale * _randomScaleMultiplier;
                transform.localScale  = Vector3.one * matureScale;
                transform.localPosition = _baseLocalPosition + new Vector3(0f, -(baseScale - matureScale) * 0.5f, 0f);
                break;
        }
    }

    public float GrowthProgress => _growthProgress;
}

// ── Extension ─────────────────────────────────────────────────────────
public static class WoodSpeciesExtensions
{
    public static int logsPerLog(this WoodSpeciesData data) => data.logsPerTree;
}

public enum TreeState
{
    Empty,
    Stump,          // just harvested — Sawyer will walk over and clear it when replanting
    Sapling,
    YoungTree,
    ReadyToHarvest
}
