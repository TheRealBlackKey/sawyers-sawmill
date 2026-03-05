using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainHUDController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private UIManager uiManager;

        private UIDocument _uiDocument;
        private Label _lblGold;
        private Label _lblReputation;
        private Label _lblSystemMessage;

        private Button _btnInventory;
        private Button _btnBuild;
        private Button _btnPlant;
        private Button _btnFurniture;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;

            // Labels
            _lblGold = root.Q<Label>("GoldLabel");
            _lblReputation = root.Q<Label>("RepLabel");
            _lblSystemMessage = root.Q<Label>("SystemMessageLabel");

            // Query buttons matches the UXML names in MainHUD.uxml
            _btnInventory = root.Q<Button>("BtnInventory");
            _btnBuild = root.Q<Button>("BtnBuild");
            _btnPlant = root.Q<Button>("BtnPlant");
            _btnFurniture = root.Q<Button>("BtnFurniture");

            // Subscribe to Navigation click events
            if (_btnInventory != null) _btnInventory.clicked += OnInventoryClicked;
            if (_btnBuild != null) _btnBuild.clicked += OnBuildClicked;
            if (_btnPlant != null) _btnPlant.clicked += OnPlantClicked;
            if (_btnFurniture != null) _btnFurniture.clicked += OnFurnitureClicked;

            // Subscribe to GameManager events for live UI updates
            GameManager.OnGoldChanged += UpdateGoldUI;
            GameManager.OnReputationChanged += UpdateReputationUI;
            GameManager.OnDayChanged += ShowSystemMessage;
            GameManager.OnMarketDayBegin += () => ShowSystemMessage("Market Day has begun! Prices are up 30%!");
            GameManager.OnItemProduced += (item) => ShowSystemMessage($"Produced: {item.species.speciesName} Board");
        }

        private void Start()
        {
            // Initial UI sync
            if (GameManager.Instance != null)
            {
                UpdateGoldUI(GameManager.Instance.Gold);
                UpdateReputationUI(GameManager.Instance.Reputation);
                ShowSystemMessage($"Day {GameManager.Instance.CurrentDay} begins...");
            }
        }

        private void OnDisable()
        {
            // Unsubscribe navigation
            if (_btnInventory != null) _btnInventory.clicked -= OnInventoryClicked;
            if (_btnBuild != null) _btnBuild.clicked -= OnBuildClicked;
            if (_btnPlant != null) _btnPlant.clicked -= OnPlantClicked;
            if (_btnFurniture != null) _btnFurniture.clicked -= OnFurnitureClicked;

            // Unsubscribe game events
            GameManager.OnGoldChanged -= UpdateGoldUI;
            GameManager.OnReputationChanged -= UpdateReputationUI;
            GameManager.OnDayChanged -= ShowSystemMessage;
            GameManager.OnMarketDayBegin -= () => ShowSystemMessage("Market Day has begun! Prices are up 30%!");
            GameManager.OnItemProduced -= (item) => ShowSystemMessage($"Produced: {item.species.speciesName} Board");
        }

        // --- Data Binding Methods ---
        
        private void UpdateGoldUI(float newGold)
        {
            if (_lblGold != null) _lblGold.text = $"${newGold:N0}";
        }

        private void UpdateReputationUI(float newRep)
        {
            if (_lblReputation != null) _lblReputation.text = $"{newRep:N0} Rep";
        }

        private void ShowSystemMessage(string msg)
        {
            if (_lblSystemMessage != null) _lblSystemMessage.text = msg;
        }

        // --- Navigation ---

        private void OnInventoryClicked()
        {
            if (uiManager != null) uiManager.OpenWoodInventory();
            else Debug.LogWarning("MainHUDController: UIManager reference missing!");
        }

        private void OnBuildClicked()
        {
            if (uiManager != null) uiManager.OpenBuildMenu();
            else Debug.LogWarning("MainHUDController: UIManager reference missing!");
        }

        private void OnPlantClicked()
        {
            if (uiManager != null) uiManager.OpenPlantingMenu();
            else Debug.LogWarning("MainHUDController: UIManager reference missing!");
        }

        private void OnFurnitureClicked()
        {
            if (uiManager != null) uiManager.OpenFurnitureCreator();
            else Debug.LogWarning("MainHUDController: UIManager reference missing!");
        }
    }
}
