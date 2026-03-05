using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages the persistent HUD strip at the bottom/side of the screen.
/// Updates gold, reputation, day, building statuses, and custom orders.
/// Drives the notification system for rare grain finds and zone depletion.
/// </summary>
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Economy Display")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI reputationText;
    [SerializeField] private Slider reputationBar;
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private GameObject marketDayBadge;

    [Header("Building Status Panels")]
    [SerializeField] private TextMeshProUGUI sawmillStatus;
    [SerializeField] private Slider sawmillProgressBar;
    [SerializeField] private TextMeshProUGUI kilnStatus = null;
    [SerializeField] private Slider kilnProgressBar;
    [SerializeField] private TextMeshProUGUI surfacingStatus;
    [SerializeField] private Slider surfacingProgressBar;
    [SerializeField] private TextMeshProUGUI marketStatus;

    [Header("Notification System")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationTitle;
    [SerializeField] private TextMeshProUGUI notificationBody;
    [SerializeField] private Image notificationIcon;
    [SerializeField] private float notificationDuration = 4f;
    [SerializeField] private Image notificationBackground;

    [Header("Custom Orders Panel")]
    [SerializeField] private Transform ordersContainer;
    [SerializeField] private GameObject orderCardPrefab;

    [Header("Trending Display")]
    [SerializeField] private TextMeshProUGUI trendingText;
    [SerializeField] private Image trendingSpeciesIcon;

    [Header("Roadside Sale (Early Game)")]
    [SerializeField] private GameObject         roadsideSalePanel;
    [SerializeField] private UnityEngine.UI.Button roadsideSaleButton;
    [SerializeField] private TextMeshProUGUI    roadsideSaleLabel = null;
    [SerializeField] private Color              roadsideNotifyColor = new Color(0.55f, 0.35f, 0.1f);

    // ── References ────────────────────────────────────────────────────
    private SawmillBuilding _sawmill;
    private KilnBuilding _kiln;
    private SurfacingBuilding _surfacing;
    private MarketBuilding _market;

    [Header("Notification Colors")]
    [SerializeField] private Color commonNotifyColor    = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color rareNotifyColor      = new Color(0.2f, 0.4f, 0.9f);
    [SerializeField] private Color legendaryNotifyColor = new Color(0.8f, 0.5f, 0.1f);
    [SerializeField] private Color depletionNotifyColor = new Color(0.7f, 0.45f, 0.15f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameManager.OnGoldChanged        += UpdateGold;
        GameManager.OnReputationChanged  += UpdateReputation;
        GameManager.OnDayChanged         += UpdateDay;
        GameManager.OnMarketDayBegin     += ShowMarketDayAlert;
        GameManager.OnMarketDayEnd       += HideMarketDayBadge;
        GameManager.OnRareGrainRevealed  += ShowRareGrainNotification;
        MarketBuilding.OnCustomOrderReceived += ShowOrderNotification;
        ForestZone.OnZoneDepletedGlobal  += OnZoneDepleted;

        // Initialize Center Sale Overlay
        var overlayObj = new GameObject("CenterSaleOverlay");
        overlayObj.transform.SetParent(this.transform);
        overlayObj.AddComponent<Sawmill.UI.CenterSaleOverlay>();

        _sawmill   = FindFirstObjectByType<SawmillBuilding>();
        _kiln      = FindFirstObjectByType<KilnBuilding>();
        _surfacing = FindFirstObjectByType<SurfacingBuilding>();
        _market    = FindFirstObjectByType<MarketBuilding>();

        if (notificationPanel != null)
            notificationPanel.SetActive(false);

        // Wire roadside sale button
        if (roadsideSaleButton != null)
            roadsideSaleButton.onClick.AddListener(OnRoadsideSaleClicked);
        RoadsideSaleManager.OnRoadsideSale += OnRoadsideSaleCompleted;

        var gm = GameManager.Instance;
        if (gm != null)
        {
            UpdateGold(gm.Gold);
            UpdateReputation(gm.Reputation);
            UpdateDay($"Day {gm.CurrentDay}");
        }
    }

    private void OnDestroy()
    {
        GameManager.OnGoldChanged        -= UpdateGold;
        GameManager.OnReputationChanged  -= UpdateReputation;
        GameManager.OnDayChanged         -= UpdateDay;
        GameManager.OnMarketDayBegin     -= ShowMarketDayAlert;
        GameManager.OnMarketDayEnd       -= HideMarketDayBadge;
        GameManager.OnRareGrainRevealed  -= ShowRareGrainNotification;
        MarketBuilding.OnCustomOrderReceived -= ShowOrderNotification;
        ForestZone.OnZoneDepletedGlobal  -= OnZoneDepleted;
        RoadsideSaleManager.OnRoadsideSale -= OnRoadsideSaleCompleted;
    }

    private void Update()
    {
        UpdateBuildingStatuses();
        UpdateTrending();
        UpdateRoadsideSaleButton();
    }

    private void UpdateBuildingStatuses()
    {
        if (_sawmill   == null) _sawmill   = FindFirstObjectByType<SawmillBuilding>();
        if (_kiln      == null) _kiln      = FindFirstObjectByType<KilnBuilding>();
        if (_surfacing == null) _surfacing = FindFirstObjectByType<SurfacingBuilding>();
        if (_market    == null) _market    = FindFirstObjectByType<MarketBuilding>();

        if (_sawmill != null)
        {
            sawmillStatus?.SetText(_sawmill.GetStatusText());
            if (sawmillProgressBar != null)
                sawmillProgressBar.value = _sawmill.IsProcessing ? _sawmill.ProcessingProgress : 0f;
        }

        if (_kiln != null)
        {
            kilnStatus?.SetText(_kiln.GetStatusText());
            if (kilnProgressBar != null)
                kilnProgressBar.value = _kiln.AverageProgress;
        }

        if (_surfacing != null)
        {
            surfacingStatus?.SetText(_surfacing.GetStatusText());
            if (surfacingProgressBar != null)
                surfacingProgressBar.value = _surfacing.IsProcessing ? _surfacing.ProcessingProgress : 0f;
        }

        if (_market != null)
        {
            int marketItems = InventoryManager.Instance?.Count(InventoryManager.InventoryZone.MarketReady) ?? 0;
            marketStatus?.SetText($"Market: {marketItems} item(s) ready");
        }
    }

    private void UpdateTrending()
    {
        var gm = GameManager.Instance;
        if (gm == null || trendingText == null) return;

        if (gm.TrendingSpecies != null)
        {
            trendingText.text = $"{gm.TrendingSpecies.speciesName} x{gm.TrendingValueMultiplier:F1}";
            if (trendingSpeciesIcon != null)
                trendingSpeciesIcon.sprite = gm.TrendingSpecies.GetRandomMatureSprite();
        }
    }

    private void UpdateGold(float gold)
    {
        if (goldText != null)
            goldText.text = $"${Mathf.RoundToInt(gold)}";
    }

    private void UpdateReputation(float rep)
    {
        if (reputationText != null)
            reputationText.text = GetReputationTitle(rep);
        if (reputationBar != null)
        {
            reputationBar.maxValue = GameManager.Instance?.MaxReputation ?? 1000f;
            reputationBar.value = rep;
        }
    }

    private string GetReputationTitle(float rep)
    {
        float max = GameManager.Instance?.MaxReputation ?? 1000f;
        float pct = rep / max;
        return pct switch
        {
            < 0.1f  => "Unknown Mill",
            < 0.25f => "Local Mill",
            < 0.5f  => "Known Craftsman",
            < 0.75f => "Master Miller",
            _       => "Legendary Craftsman"
        };
    }

    private void UpdateDay(string dayText)
    {
        if (this.dayText != null)
            this.dayText.text = dayText;
    }

    private void ShowMarketDayAlert()
    {
        marketDayBadge?.SetActive(true);
        ShowNotification("Market Day!", "Prices are higher today — sell your best pieces!", legendaryNotifyColor);
    }

    private void HideMarketDayBadge()
    {
        marketDayBadge?.SetActive(false);
    }

    private void ShowRareGrainNotification(GrainVariant grain, WoodSpeciesData species)
    {
        if (grain == null || species == null) return;

        Color color = grain.rarityTier switch
        {
            3 => legendaryNotifyColor,
            2 => rareNotifyColor,
            _ => commonNotifyColor
        };

        string tierLabel = grain.rarityTier switch
        {
            3 => "LEGENDARY FIND!",
            2 => "Rare Grain Found!",
            1 => "Uncommon Grain Revealed",
            _ => "Grain Revealed"
        };

        ShowNotification(tierLabel,
            $"{grain.variantName} {species.speciesName}\n{grain.description}\n{grain.valueMultiplier:F1}x value!",
            color);
    }

    private void ShowOrderNotification(CustomOrder order)
    {
        ShowNotification("New Custom Order", order.description, rareNotifyColor);
    }

    /// <summary>
    /// Called when a ForestZone fully depletes — all trees have become stumps.
    /// Alerts the player to plant a new zone.
    /// </summary>
    private void OnZoneDepleted(ForestZone zone)
    {
        string speciesName = zone.AssignedSpecies != null ? zone.AssignedSpecies.speciesName : "your";
        ShowNotification(
            "Forest Zone Depleted",
            $"Your {speciesName} zone is exhausted. Sawyer will clear the stumps. " +
            $"Paint a new zone to keep growing {speciesName}!",
            depletionNotifyColor);
    }

    public void ShowNotification(string title, string body, Color bgColor)
    {
        StopAllCoroutines();
        StartCoroutine(ShowNotificationRoutine(title, body, bgColor));
    }

    private IEnumerator ShowNotificationRoutine(string title, string body, Color bgColor)
    {
        if (notificationPanel == null) yield break;

        notificationTitle.text = title;
        notificationBody.text  = body;
        if (notificationBackground != null) notificationBackground.color = bgColor;

        notificationPanel.SetActive(true);

        var cg = notificationPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; cg.alpha = t / 0.3f; yield return null; }
            cg.alpha = 1f;
        }

        yield return new WaitForSeconds(notificationDuration);

        if (cg != null)
        {
            float t = 0f;
            while (t < 0.5f) { t += Time.deltaTime; cg.alpha = 1f - t / 0.5f; yield return null; }
        }

        notificationPanel.SetActive(false);
    }

    private void UpdateRoadsideSaleButton()
    {
        var rsm = RoadsideSaleManager.Instance;
        if (roadsideSalePanel == null || rsm == null) return;

        bool available = rsm.IsAvailable();
        roadsideSalePanel.SetActive(available);

        if (!available) return;

        int count = rsm.AvailableBoardCount();
        if (roadsideSaleLabel != null)
        {
            roadsideSaleLabel.text = count > 0
                ? $"Sell Boards ({count})"
                : "No Boards";
        }

        if (roadsideSaleButton != null)
            roadsideSaleButton.interactable = count > 0;
    }

    private void OnRoadsideSaleClicked()
    {
        RoadsideSaleManager.Instance?.SellAllRoughSawn();
    }

    private void OnRoadsideSaleCompleted(int count, float gold)
    {
        ShowNotification(
            "Boards Sold!",
            $"Sold {count} rough sawn board(s) roadside for {gold:F0}g\n(Discount rate — build a Market for full price)",
            roadsideNotifyColor);
    }
}
