using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Orchestrates reading and writing all game state to a local JSON file (via PlayerPrefs).
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_KEY = "SawyersSawmill_Quicksave";

    [Header("Testing")]
    [Tooltip("If true, ignores existing save data on start (forces a fresh game).")]
    [SerializeField] private bool ignoreSaveOnStart = false;

    [Header("Data References")]
    [Tooltip("Assign all possible buildings here so they can be loaded from save.")]
    [SerializeField] private List<BuildingData> allBuildings;
    [Tooltip("Assign all possible wood species here so they can be loaded from save.")]
    [SerializeField] private List<WoodSpeciesData> allSpecies;

    private bool _hasLoaded = false;

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

    private void Start()
    {
        if (!ignoreSaveOnStart && PlayerPrefs.HasKey(SAVE_KEY))
        {
            LoadGame();
        }
        else
        {
            Debug.Log("[SaveManager] Starting fresh game (No save found or ignoreSaveOnStart is true).");
            _hasLoaded = true;
        }
    }

    private void Update()
    {
        // F5 Quick Save
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveGame();
        }
    }

    public void SaveGame()
    {
        if (!_hasLoaded) return; // Prevent overwriting a good save with early empty state

        SaveData data = new SaveData();

        // 1. Core State (Gold, Reputation, Day)
        if (GameManager.Instance != null)
        {
            data.gold            = GameManager.Instance.Gold;
            data.reputation      = GameManager.Instance.Reputation;
            data.currentDay      = GameManager.Instance.CurrentDay;
            data.totalItemsSold  = GameManager.Instance.TotalItemsSold;
            data.totalGoldEarned = GameManager.Instance.TotalGoldEarned;

            // Dictionaries to Lists for JSON
            foreach (var kvp in GameManager.Instance.TotalBoardsProcessed)
                data.boardsProcessedKeys.Add(kvp.Key);
            foreach (var kvp in GameManager.Instance.TotalBoardsProcessed)
                data.boardsProcessedValues.Add(kvp.Value);

            foreach (var kvp in GameManager.Instance.TotalLogsHarvested)
                data.logsHarvestedKeys.Add(kvp.Key);
            foreach (var kvp in GameManager.Instance.TotalLogsHarvested)
                data.logsHarvestedValues.Add(kvp.Value);
        }

        // 1.5 Map Generation Seeds
        if (WorldGrid.Instance != null)
            data.obstacleSeed = WorldGrid.Instance.obstacleSeed;
            
        var tileManager = FindFirstObjectByType<Sawmill.Core.WorldTileManager>();
        if (tileManager != null)
            data.noiseSeed = tileManager.noiseSeed;

        // 2. Buildings
        if (PlacementManager.Instance != null)
        {
            foreach (var placed in PlacementManager.Instance.PlacedBuildings)
            {
                if (placed.data == null) continue;
                data.buildings.Add(new BuildingSaveData
                {
                    buildingDataName = placed.data.name, // ScriptableObject asset name
                    gridX            = placed.gridOrigin.x,
                    gridY            = placed.gridOrigin.y
                });
            }
        }

        // 3. Forest Zones
        var allZones = FindObjectsByType<ForestZone>(FindObjectsSortMode.None);
        foreach (var zone in allZones)
        {
            if (zone.AssignedSpecies == null) continue;
            data.forestZones.Add(new ForestZoneSaveData
            {
                speciesDataName = zone.AssignedSpecies.name,
                gridX           = zone.RegionStartX,
                gridY           = zone.RegionStartY,
                width           = zone.RegionWidth,
                height          = zone.RegionHeight,
                isDepleted      = zone.IsDepletedZone
            });
            
            // Note: we do not serialize the UnlockedSpecies list per-zone here
            // because GetAllUnlockedSpecies() in ForestZonePainter recalculates it
            // based on the zones currently alive on the map.
        }

        // 4. Inventory
        if (InventoryManager.Instance != null)
        {
            foreach (InventoryManager.InventoryZone zone in System.Enum.GetValues(typeof(InventoryManager.InventoryZone)))
            {
                var items = InventoryManager.Instance.GetItems(zone);
                foreach (var item in items)
                {
                    if (item == null || item.species == null) continue;
                    data.inventoryItems.Add(new ItemSaveData
                    {
                        zone            = (int)zone,
                        speciesDataName = item.species.name,
                        stage           = (int)item.stage,
                        itemType        = (int)item.itemType,
                        isSpalted       = item.isSpalted,
                        // Not perfectly preserving unique GrainVariants yet to keep JSON simple
                    });
                }
            }
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log($"[SaveManager] Quick Save Complete! {data.buildings.Count} buildings, " +
                  $"{data.forestZones.Count} forests, {data.inventoryItems.Count} items.");
                  
        HUDManager.Instance?.ShowNotification("Game Saved", "Quick save complete.", new Color(0.2f, 0.6f, 0.2f));
    }

    public void LoadGame()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;

        string json = PlayerPrefs.GetString(SAVE_KEY);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data == null) return;

        // 1. Core State
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadFromSaveData(
                data.gold, data.reputation, data.currentDay, 
                data.totalItemsSold, data.totalGoldEarned,
                data.boardsProcessedKeys, data.boardsProcessedValues,
                data.logsHarvestedKeys, data.logsHarvestedValues);
        }

        // 1.5 Procedural Map Regeneration
        if (WorldGrid.Instance != null)
        {
            WorldGrid.Instance.obstacleSeed = data.obstacleSeed;
            WorldGrid.Instance.RegenerateObstacles();
        }

        var tileManager = FindFirstObjectByType<Sawmill.Core.WorldTileManager>();
        if (tileManager != null)
        {
            tileManager.noiseSeed = data.noiseSeed;
            tileManager.GenerateTiles();
        }

        // 2. Buildings
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.LoadFromSaveData(data.buildings, allBuildings);
        }

        // 3. Forest Zones
        // Since ForestZonePainter handles spawning cleanly
        var painter = FindFirstObjectByType<ForestZonePainter>();
        if (painter != null)
        {
            painter.LoadFromSaveData(data.forestZones);
        }

        // 4. Inventory
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.LoadFromSaveData(data.inventoryItems, allSpecies);
        }

        _hasLoaded = true;
        Debug.Log("[SaveManager] Game Loaded Successfully.");
        HUDManager.Instance?.ShowNotification("Game Loaded", "Welcome back to the Sawmill.", new Color(0.2f, 0.4f, 0.8f));
    }

    private void OnApplicationQuit()
    {
        if (_hasLoaded) SaveGame();
    }

    private void OnDisable()
    {
        // Fallback for Unity Editor Play Mode stopping, where Quit isn't always reliable
        if (_hasLoaded) SaveGame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Fallback for mobile/backgrounding
        if (pauseStatus && _hasLoaded) SaveGame();
    }
}

// ── Serializable Data Structures ──────────────────────────────────────

[System.Serializable]
public class SaveData
{
    // Economy & Stats
    public float gold;
    public float reputation;
    public int   currentDay;
    public int   totalItemsSold;
    public float totalGoldEarned;

    // Seeds for procedural generation
    public int   obstacleSeed;
    public float noiseSeed;

    public List<string> boardsProcessedKeys   = new List<string>();
    public List<int>    boardsProcessedValues = new List<int>();
    
    public List<string> logsHarvestedKeys     = new List<string>();
    public List<int>    logsHarvestedValues   = new List<int>();

    // Entities
    public List<BuildingSaveData>   buildings      = new List<BuildingSaveData>();
    public List<ForestZoneSaveData> forestZones    = new List<ForestZoneSaveData>();
    public List<ItemSaveData>       inventoryItems = new List<ItemSaveData>();
}

[System.Serializable]
public class BuildingSaveData
{
    public string buildingDataName; // Matches the ScriptableObject asset name
    public int    gridX;
    public int    gridY;
}

[System.Serializable]
public class ForestZoneSaveData
{
    public string speciesDataName; // Matches the ScriptableObject asset name
    public int    gridX;
    public int    gridY;
    public int    width;
    public int    height;
    public bool   isDepleted;
}

[System.Serializable]
public class ItemSaveData
{
    public int    zone; // Cast back to InventoryManager.InventoryZone
    public string speciesDataName;
    public int    stage; // Cast to ProcessingStage
    public int    itemType; // Cast to LumberItemType
    public bool   isSpalted;
}
