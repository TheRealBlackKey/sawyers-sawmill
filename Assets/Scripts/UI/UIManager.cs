using UnityEngine;

namespace Sawmill.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Document References")]
        [Tooltip("The GameObject containing the WoodInventory UIDocument")]
        [SerializeField] private GameObject woodInventoryScreen;
        
        [Tooltip("The GameObject containing the BuildMenu UIDocument")]
        [SerializeField] private GameObject buildMenuScreen;
        
        [Tooltip("The GameObject containing the PlantingMenu UIDocument")]
        [SerializeField] private GameObject plantingMenuScreen;
        
        [Tooltip("The GameObject containing the FurnitureCreator UIDocument")]
        [SerializeField] private GameObject furnitureCreatorScreen;

        public static UIManager Instance { get; private set; }

        public bool IsAnyMenuOpen => 
            (woodInventoryScreen != null && woodInventoryScreen.activeInHierarchy) ||
            (buildMenuScreen != null && buildMenuScreen.activeInHierarchy) ||
            (plantingMenuScreen != null && plantingMenuScreen.activeInHierarchy) ||
            (furnitureCreatorScreen != null && furnitureCreatorScreen.activeInHierarchy);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Ensure all sub-menus are turned off when the game starts
            CloseAllMenus();
        }

        public void OpenWoodInventory()
        {
            CloseAllMenus();
            if (woodInventoryScreen != null) woodInventoryScreen.SetActive(true);
            else Debug.LogWarning("UIManager: WoodInventoryScreen is not assigned in the Inspector!");
        }

        public void OpenBuildMenu()
        {
            CloseAllMenus();
            if (buildMenuScreen != null) buildMenuScreen.SetActive(true);
            else Debug.LogWarning("UIManager: BuildMenuScreen is not assigned in the Inspector!");
        }

        public void OpenPlantingMenu()
        {
            CloseAllMenus();
            if (plantingMenuScreen != null) plantingMenuScreen.SetActive(true);
            else Debug.LogWarning("UIManager: PlantingMenuScreen is not assigned in the Inspector!");
        }

        public void OpenFurnitureCreator()
        {
            CloseAllMenus();
            if (furnitureCreatorScreen != null) furnitureCreatorScreen.SetActive(true);
            else Debug.LogWarning("UIManager: FurnitureCreatorScreen is not assigned in the Inspector!");
        }

        public void CloseAllMenus()
        {
            if (woodInventoryScreen != null) woodInventoryScreen.SetActive(false);
            if (buildMenuScreen != null) buildMenuScreen.SetActive(false);
            if (plantingMenuScreen != null) plantingMenuScreen.SetActive(false);
            if (furnitureCreatorScreen != null) furnitureCreatorScreen.SetActive(false);
            
            // Cancel any active grid-based tools so they don't block clicks or persist across menus
            if (PlacementManager.Instance != null && PlacementManager.Instance.IsPlacing)
            {
                PlacementManager.Instance.CancelPlacement();
            }
            if (ForestZonePainter.Instance != null && ForestZonePainter.Instance.IsPainting)
            {
                ForestZonePainter.Instance.CancelPainting();
            }
        }
    }
}
