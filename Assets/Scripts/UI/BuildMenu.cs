using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Build menu UI. Creates building buttons programmatically when opened.
/// Attach to BuildPanel. No entry prefab needed.
/// </summary>
public class BuildMenu : MonoBehaviour
{
    public static BuildMenu Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Transform buildingListParent;

    [Header("All Buildable Buildings")]
    [SerializeField] private List<BuildingData> allBuildings = new List<BuildingData>();

    public bool IsOpen { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            CreateCloseButton();
        }

        PlacementManager.OnBuildingPlaced += OnBuildingPlaced;
        PlacementManager.OnPlacementCancelled += OnPlacementCancelled;
    }

    private void OnDestroy()
    {
        PlacementManager.OnBuildingPlaced -= OnBuildingPlaced;
        PlacementManager.OnPlacementCancelled -= OnPlacementCancelled;
    }

    public void ToggleMenu()
    {
        if (IsOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        IsOpen = true;
        if (menuPanel != null) menuPanel.SetActive(true);
        RefreshEntries();
    }

    public void CloseMenu()
    {
        IsOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // ── Close Button ──────────────────────────────────────────────────
    // Created once on Start, always visible at top of panel

    private void CreateCloseButton()
    {
        RectTransform panelRT = menuPanel.GetComponent<RectTransform>();

        GameObject btnGO = new GameObject("CloseButton", typeof(RectTransform));
        btnGO.transform.SetParent(menuPanel.transform, false);

        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 1f);
        btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot = new Vector2(1f, 1f);
        btnRT.anchoredPosition = new Vector2(-4f, -4f);
        btnRT.sizeDelta = new Vector2(50f, 24f);

        Image btnImage = btnGO.AddComponent<Image>();
        btnImage.color = new Color(0.5f, 0.15f, 0.1f, 1f); // dark red-brown

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(CloseMenu);

        GameObject btnTextGO = new GameObject("CloseText", typeof(RectTransform));
        btnTextGO.transform.SetParent(btnGO.transform, false);

        RectTransform btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "Close";
        btnText.fontSize = 11;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
    }

    // ── Entries ───────────────────────────────────────────────────────

    private void RefreshEntries()
    {
        foreach (Transform child in buildingListParent)
            Destroy(child.gameObject);

        int index = 0;
        foreach (var building in allBuildings)
        {
            if (building == null) continue;
            CreateEntry(building, index);
            index++;
        }
    }

    private void CreateEntry(BuildingData building, int index)
    {
        bool canAfford = GameManager.Instance != null && GameManager.Instance.Gold >= (float)building.goldCost;

        float rowHeight = 44f;
        float rowWidth = 260f;
        float yOffset = -(index * (rowHeight + 4f));

        // ── Root row ──────────────────────────────────────────────────
        GameObject row = new GameObject(building.buildingName + "_Entry", typeof(RectTransform));
        row.transform.SetParent(buildingListParent, false);

        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(0f, 1f);
        rowRT.pivot = new Vector2(0f, 1f);
        rowRT.anchoredPosition = new Vector2(4f, yOffset - 4f);
        rowRT.sizeDelta = new Vector2(rowWidth, rowHeight);

        Image rowBG = row.AddComponent<Image>();
        rowBG.color = new Color(0.55f, 0.35f, 0.15f, 1f);

        // ── Building name ─────────────────────────────────────────────
        GameObject nameGO = new GameObject("NameText", typeof(RectTransform));
        nameGO.transform.SetParent(row.transform, false);

        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0f);
        nameRT.anchorMax = new Vector2(0f, 0f);
        nameRT.pivot = new Vector2(0f, 0f);
        nameRT.anchoredPosition = new Vector2(6f, 6f);
        nameRT.sizeDelta = new Vector2(120f, 32f);

        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = building.buildingName;
        nameText.fontSize = 12;
        nameText.color = new Color(0.95f, 0.90f, 0.78f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;

        // ── Cost ──────────────────────────────────────────────────────
        GameObject costGO = new GameObject("CostText", typeof(RectTransform));
        costGO.transform.SetParent(row.transform, false);

        RectTransform costRT = costGO.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0f, 0f);
        costRT.anchorMax = new Vector2(0f, 0f);
        costRT.pivot = new Vector2(0f, 0f);
        costRT.anchoredPosition = new Vector2(132f, 6f);
        costRT.sizeDelta = new Vector2(50f, 32f);

        TextMeshProUGUI costText = costGO.AddComponent<TextMeshProUGUI>();
        costText.text = $"${building.goldCost}";
        costText.fontSize = 11;
        costText.color = canAfford ? new Color(0.5f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
        costText.alignment = TextAlignmentOptions.MidlineLeft;

        // ── Build button ──────────────────────────────────────────────
        GameObject btnGO = new GameObject("BuildBtn", typeof(RectTransform));
        btnGO.transform.SetParent(row.transform, false);

        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0f, 0f);
        btnRT.anchorMax = new Vector2(0f, 0f);
        btnRT.pivot = new Vector2(0f, 0f);
        btnRT.anchoredPosition = new Vector2(188f, 4f);
        btnRT.sizeDelta = new Vector2(66f, 36f);

        Image btnImage = btnGO.AddComponent<Image>();
        btnImage.color = canAfford ? new Color(0.25f, 0.55f, 0.25f) : new Color(0.35f, 0.35f, 0.35f);

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.interactable = canAfford;

        GameObject btnTextGO = new GameObject("BtnText", typeof(RectTransform));
        btnTextGO.transform.SetParent(btnGO.transform, false);

        RectTransform btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = new Vector2(0f, 0f);
        btnTextRT.anchorMax = new Vector2(0f, 0f);
        btnTextRT.pivot = new Vector2(0f, 0f);
        btnTextRT.anchoredPosition = Vector2.zero;
        btnTextRT.sizeDelta = new Vector2(66f, 36f);

        TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text = canAfford ? "Build" : "No Gold";
        btnText.fontSize = 11;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        BuildingData captured = building;
        btn.onClick.AddListener(() =>
        {
            CloseMenu();
            PlacementManager.Instance?.BeginPlacement(captured);
        });
    }

    private void OnBuildingPlaced(PlacedBuilding placed) { }
    private void OnPlacementCancelled() { }

    public void RegisterBuilding(BuildingData data)
    {
        if (!allBuildings.Contains(data))
        {
            allBuildings.Add(data);
            if (IsOpen) RefreshEntries();
        }
    }
}
