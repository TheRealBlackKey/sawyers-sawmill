using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages the persistent HUD strip at the bottom/side of the screen.
/// Updates gold, reputation, day, building statuses, and custom orders.
/// Drives the notification system for rare grain finds.
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
    [SerializeField] private TextMeshProUGUI kilnStatus;
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

    // ── References ────────────────────────────────────────────────────
    private SawmillBuilding _sawmill;
    private KilnBuilding _kiln;
    private SurfacingBuilding _surfacing;
    private MarketBuilding _market;

    [Header("Notification Colors")]
    [SerializeField] private Color commonNotifyColor = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color rareNotifyColor = new Color(0.2f, 0.4f, 0.9f);
    [SerializeField] private Color legendaryNotifyColor = new Color(0.8f, 0.5f, 0.1f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameManager.OnGoldChanged += UpdateGold;
        GameManager.OnReputationChanged += UpdateReputation;
        GameManager.OnDayChanged += UpdateDay;
        GameManager.OnMarketDayBegin += ShowMarketDayAlert;
        GameManager.OnMarketDayEnd += HideMarketDayBadge;
        GameManager.OnRareGrainRevealed += ShowRareGrainNotification;
        GameManager.OnItemSold += OnItemSold;
        MarketBuilding.OnCustomOrderReceived += ShowOrderNotification;

        // Find buildings using Unity 6 API
        _sawmill   = FindFirstObjectByType<SawmillBuilding>();
        _kiln      = FindFirstObjectByType<KilnBuilding>();
        _surfacing = FindFirstObjectByType<SurfacingBuilding>();
        _market    = FindFirstObjectByType<MarketBuilding>();

        if (notificationPanel != null)
            notificationPanel.SetActive(false);

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
        GameManager.OnGoldChanged -= UpdateGold;
        GameManager.OnReputationChanged -= UpdateReputation;
        GameManager.OnDayChanged -= UpdateDay;
        GameManager.OnMarketDayBegin -= ShowMarketDayAlert;
        GameManager.OnMarketDayEnd -= HideMarketDayBadge;
        GameManager.OnRareGrainRevealed -= ShowRareGrainNotification;
        GameManager.OnItemSold -= OnItemSold;
        MarketBuilding.OnCustomOrderReceived -= ShowOrderNotification;
    }

    private void Update()
    {
        UpdateBuildingStatuses();
        UpdateTrending();
    }

    private void UpdateBuildingStatuses()
    {
        if (_sawmill != null)
        {
            sawmillStatus?.SetText(_sawmill.GetStatusText());
            if (sawmillProgressBar != null)
                sawmillProgressBar.value = _sawmill.IsProcessing ? _sawmill.ProcessingProgress : 0f;
        }

        if (_kiln != null)
            kilnStatus?.SetText(_kiln.GetStatusText());

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
            if (trendingSpeciesIcon != null && gm.TrendingSpecies.treeSprite != null)
                trendingSpeciesIcon.sprite = gm.TrendingSpecies.treeSprite;
        }
    }

    private void UpdateGold(float gold)
    {
        if (goldText != null)
            goldText.text = $"${gold:F0}";
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

    private void OnItemSold(LumberItem item, float value)
    {
        if (item.grainVariant?.rarityTier >= 1 || item.isSpalted)
            ShowNotification("Sale!", $"{item.DisplayName} sold for ${value:F0}", commonNotifyColor);
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

    public void ShowNotification(string title, string body, Color bgColor)
    {
        StopAllCoroutines();
        StartCoroutine(ShowNotificationRoutine(title, body, bgColor));
    }

    private IEnumerator ShowNotificationRoutine(string title, string body, Color bgColor)
    {
        if (notificationPanel == null) yield break;

        notificationTitle.text = title;
        notificationBody.text = body;
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
}
