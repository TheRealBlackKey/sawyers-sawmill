using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Central game manager singleton. Owns global state, coordinates between systems,
/// and handles save/load. Place on a persistent GameObject in your bootstrap scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── Events (subscribe from any system) ────────────────────────────
    public static event Action<float> OnGoldChanged;
    public static event Action<float> OnReputationChanged;
    public static event Action<float> OnSawdustChanged;
    public static event Action<LumberItem> OnItemProduced;
    public static event Action<LumberItem, float> OnItemSold;
    public static event Action<GrainVariant, WoodSpeciesData> OnRareGrainRevealed;
    public static event Action<string> OnDayChanged;         // Day/night cycle events
    public static event Action OnMarketDayBegin;
    public static event Action OnMarketDayEnd;

    // ── Economy State ─────────────────────────────────────────────────
    [Header("Economy")]
    [SerializeField] private float startingGold = 50f;
    private float _gold;
    public float Gold
    {
        get => _gold;
        private set
        {
            _gold = Mathf.Max(0f, value);
            OnGoldChanged?.Invoke(_gold);
        }
    }

    private float _reputation;
    public float Reputation
    {
        get => _reputation;
        private set
        {
            _reputation = Mathf.Clamp(value, 0f, MaxReputation);
            OnReputationChanged?.Invoke(_reputation);
        }
    }

    [SerializeField] private float maxReputation = 1000f;
    public float MaxReputation => maxReputation;
    
    // ── Secondary Resources ───────────────────────────────────────────
    private float _sawdust;
    public float Sawdust
    {
        get => _sawdust;
        private set
        {
            _sawdust = Mathf.Max(0f, value);
            OnSawdustChanged?.Invoke(_sawdust);
        }
    }

    // ── Time System ───────────────────────────────────────────────────
    [Header("Time")]
    [SerializeField] private float realSecondsPerGameDay = 300f; // 5 real minutes = 1 game day
    private float _dayTimer;
    private int _currentDay = 1;
    public int CurrentDay => _currentDay;

    [SerializeField] private int marketDayInterval = 7; // Every 7 game days = market day
    private bool _isMarketDay = false;
    public bool IsMarketDay => _isMarketDay;

    // ── Global Stats ──────────────────────────────────────────────────
    public Dictionary<string, int> TotalBoardsProcessed = new Dictionary<string, int>();
    public Dictionary<string, int> TotalLogsHarvested = new Dictionary<string, int>();
    public int TotalItemsSold = 0;
    public float TotalGoldEarned = 0f;
    public float HighestSingleSaleValue = 0f;

    // ── Trending System ───────────────────────────────────────────────
    public WoodSpeciesData TrendingSpecies { get; private set; }
    public float TrendingValueMultiplier { get; private set; } = 2f;

    [SerializeField] private WoodSpeciesData[] allSpecies;

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [Header("Testing")]
    [Tooltip("When ON, ignores any saved game and always starts fresh with Starting Gold.")]
    [SerializeField] private bool ignoreSaveOnStart = false;

    private void Start()
    {
        // Don't auto-set Gold here if loading a game
        if (ignoreSaveOnStart)
        {
            Gold = startingGold;
        }
        
        RollTrendingSpecies();
    }

    private void Update()
    {
        TickDayCycle();
    }

    // ── Economy ───────────────────────────────────────────────────────
    public bool SpendGold(float amount)
    {
        if (_gold < amount) return false;
        Gold -= amount;
        return true;
    }

    public void EarnGold(float amount)
    {
        Gold += amount;
        TotalGoldEarned += amount;
        if (amount > HighestSingleSaleValue)
            HighestSingleSaleValue = amount;
    }

    public void AddSawdust(float amount)
    {
        Sawdust += amount;
    }

    public void AddReputation(float amount)
    {
        Reputation += amount;
    }

    // ── Item Tracking ─────────────────────────────────────────────────
    public void RegisterItemProduced(LumberItem item)
    {
        OnItemProduced?.Invoke(item);

        if (item.species != null)
        {
            string key = item.species.speciesName;
            if (!TotalBoardsProcessed.ContainsKey(key))
                TotalBoardsProcessed[key] = 0;
            TotalBoardsProcessed[key]++;
        }
    }

    public void RegisterItemSold(LumberItem item, float goldAmount)
    {
        TotalItemsSold++;
        OnItemSold?.Invoke(item, goldAmount);

        // Reputation gain — exotic and rare items give more
        float repGain = 1f;
        if (item.species != null && item.species.isExotic) repGain += 2f;
        if (item.grainVariant != null && item.grainVariant.rarityTier >= 2) repGain += item.grainVariant.rarityTier;
        if (item.isSpalted) repGain += 1f;
        AddReputation(repGain);
    }

    public void RegisterLogHarvested(WoodSpeciesData species, int count = 1)
    {
        string key = species.speciesName;
        if (!TotalLogsHarvested.ContainsKey(key))
            TotalLogsHarvested[key] = 0;
        TotalLogsHarvested[key] += count;
    }

    public int GetBoardsProcessed(WoodSpeciesData species)
    {
        if (species == null) return 0;
        return TotalBoardsProcessed.TryGetValue(species.speciesName, out int count) ? count : 0;
    }

    public int GetLogsHarvested(WoodSpeciesData species)
    {
        if (species == null) return 0;
        return TotalLogsHarvested.TryGetValue(species.speciesName, out int count) ? count : 0;
    }

    // ── Day / Market Cycle ────────────────────────────────────────────
    private void TickDayCycle()
    {
        _dayTimer += Time.deltaTime;
        if (_dayTimer >= realSecondsPerGameDay)
        {
            _dayTimer = 0f;
            _currentDay++;
            OnDayChanged?.Invoke($"Day {_currentDay}");

            bool wasMarketDay = _isMarketDay;
            _isMarketDay = (_currentDay % marketDayInterval == 0);

            if (_isMarketDay && !wasMarketDay)
            {
                OnMarketDayBegin?.Invoke();
                RollTrendingSpecies();
                Debug.Log($"[GameManager] Market Day! Trending: {TrendingSpecies?.speciesName}");
            }
            else if (!_isMarketDay && wasMarketDay)
            {
                OnMarketDayEnd?.Invoke();
            }
        }
    }

    private void RollTrendingSpecies()
    {
        if (allSpecies == null || allSpecies.Length == 0) return;
        TrendingSpecies = allSpecies[UnityEngine.Random.Range(0, allSpecies.Length)];
        TrendingValueMultiplier = UnityEngine.Random.Range(1.5f, 2.5f);
    }

    public float GetSaleMultiplier(WoodSpeciesData species)
    {
        float mult = 1f;
        if (IsMarketDay) mult *= 1.3f;
        if (TrendingSpecies != null && species == TrendingSpecies)
            mult *= TrendingValueMultiplier;
        mult *= GetReputationMultiplier();
        return mult;
    }

    private float GetReputationMultiplier()
    {
        // 0 rep = 0.8x, max rep = 1.5x
        return Mathf.Lerp(0.8f, 1.5f, _reputation / maxReputation);
    }

    // ── Notify Rare Find ──────────────────────────────────────────────
    public void NotifyRareGrainRevealed(GrainVariant variant, WoodSpeciesData species)
    {
        OnRareGrainRevealed?.Invoke(variant, species);
    }

    // ── Save / Load (Orchestrated by SaveManager) ─────────────────────
    
    public void LoadFromSaveData(
        float loadedGold, float loadedRep, float loadedSawdust, int loadedDay,
        int itemsSold, float goldEarned,
        List<string> bpKeys, List<int> bpValues,
        List<string> lhKeys, List<int> lhValues)
    {
        // Only load gold if it's greater than starting, or if we actually made progress
        if (loadedGold > startingGold || itemsSold > 0)
            Gold = loadedGold;
        else 
            Gold = startingGold;

        Reputation      = loadedRep;
        Sawdust         = loadedSawdust;
        _currentDay     = loadedDay;
        TotalItemsSold  = itemsSold;
        TotalGoldEarned = goldEarned;

        TotalBoardsProcessed.Clear();
        for (int i = 0; i < bpKeys.Count; i++)
        {
            if (i < bpValues.Count)
                TotalBoardsProcessed[bpKeys[i]] = bpValues[i];
        }

        TotalLogsHarvested.Clear();
        for (int i = 0; i < lhKeys.Count; i++)
        {
            if (i < lhValues.Count)
                TotalLogsHarvested[lhKeys[i]] = lhValues[i];
        }

        Debug.Log($"[GameManager] State restored. Gold: {Gold}, Day: {_currentDay}");
    }
}
