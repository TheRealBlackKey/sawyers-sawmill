using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class BuildMenuController : MonoBehaviour
    {
        [Header("Available Buildings")]
        [SerializeField] private List<BuildingData> availableBuildings;
        [SerializeField] private VisualTreeAsset buildingCardTemplate;

        private UIDocument _uiDocument;
        private Button _btnClose;
        private VisualElement _buildListContainer;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                var go = GameObject.Find("BuildMenu");
                if (go != null) _uiDocument = go.GetComponent<UIDocument>();
            }
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            
            _btnClose = root.Q<Button>("BtnClose");
            _buildListContainer = root.Q<VisualElement>("BuildingsListContainer");

            if (_btnClose != null)
            {
                _btnClose.clicked += OnCloseClicked;
            }

            PopulateBuildMenu();
        }

        private void OnDisable()
        {
            if (_btnClose != null)
            {
                _btnClose.clicked -= OnCloseClicked;
            }
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        private void PopulateBuildMenu()
        {
            if (_buildListContainer == null) return;
            if (buildingCardTemplate == null)
            {
                Debug.LogWarning("BuildMenuController: Missing buildingCardTemplate!");
                return;
            }

            _buildListContainer.Clear();

            foreach (var building in availableBuildings)
            {
                if (building == null) continue;

                VisualElement card = buildingCardTemplate.Instantiate();
                
                // Set text data
                Label nameLabel = card.Q<Label>("BuildingName");
                Label costLabel = card.Q<Label>("BuildingCost");
                Label descLabel = card.Q<Label>("BuildingDesc");

                if (nameLabel != null) nameLabel.text = building.buildingName;
                if (costLabel != null) costLabel.text = $"Cost: ${building.goldCost}";
                if (descLabel != null) descLabel.text = building.description;

                // Bind Button
                Button buildBtn = card.Q<Button>("BtnBuildStructure");
                if (buildBtn != null)
                {
                    buildBtn.clicked += () => OnBuildButtonClicked(building);
                }

                _buildListContainer.Add(card);
            }
        }

        private void OnBuildButtonClicked(BuildingData buildingData)
        {
            if (PlacementManager.Instance != null)
            {
                PlacementManager.Instance.BeginPlacement(buildingData);
                // Hide menu while placing
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("BuildMenuController: PlacementManager is missing from the scene!");
            }
        }
    }
}
