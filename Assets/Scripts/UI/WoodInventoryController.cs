using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    public class WoodInventoryController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset cardTemplate;

        // Displays all active items across the entire sawmill.
        // We no longer filter by a single displayZone.

        private VisualElement _root;
        private VisualElement _cardsContainer;
        private Button _closeButton;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                var go = GameObject.Find("WoodInventory");
                if (go != null) uiDocument = go.GetComponent<UIDocument>();
            }
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;
            _cardsContainer = _root.Q<VisualElement>("CardsContainer");
            _closeButton = _root.Q<Button>("BtnClose");

            if (_closeButton != null)
            {
                _closeButton.clicked += OnCloseClicked;
            }

            // Populate on open
            PopulateWithMarketData();
            
            // Subscribe for live updates while the menu is open
            InventoryManager.OnZoneChanged += HandleZoneChanged;
        }

        private void OnDisable()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseClicked;
            }

            InventoryManager.OnZoneChanged -= HandleZoneChanged;
        }

        private void HandleZoneChanged(InventoryManager.InventoryZone zone)
        {
            // We want to update the UI if any active inventory changes (skip Sold/Archived)
            if (zone != InventoryManager.InventoryZone.Sold)
            {
                PopulateWithMarketData();
            }
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        public void PopulateWithMarketData()
        {
            if (_cardsContainer == null)
            {
                Debug.LogWarning("WoodInventoryController: Cannot populate, CardsContainer is null.");
                return;
            }
            if (cardTemplate == null)
            {
                Debug.LogError("WoodInventoryController: cardTemplate is missing! Please assign WoodInventoryCard.uxml in the Inspector.");
                return;
            }
            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning("WoodInventoryController: InventoryManager.Instance is null.");
                return;
            }
            
            _cardsContainer.Clear();

            // Group identically typed boards/logs together across ALL active zones
            var groupedItems = new Dictionary<string, ItemGroup>();
            
            foreach (InventoryManager.InventoryZone zone in System.Enum.GetValues(typeof(InventoryManager.InventoryZone)))
            {
                if (zone == InventoryManager.InventoryZone.Sold) continue;
                
                var itemsInZone = InventoryManager.Instance.GetItems(zone);
                foreach (var item in itemsInZone)
                {
                    if (item.species == null) continue;
                    
                    // Form a unique key combination for species + finish stage + grain + spalted
                    string grainStr = item.grainVariant != null ? item.grainVariant.variantName : "Standard";
                    string spaltStr = item.isSpalted ? " (Spalted)" : "";
                    string key = $"{item.species.speciesName}_{item.stage}_{grainStr}{spaltStr}";

                    if (!groupedItems.ContainsKey(key))
                    {
                        groupedItems[key] = new ItemGroup 
                        { 
                            ReferenceItem = item, 
                            Count = 0 
                        };
                    }
                    groupedItems[key].Count++;
                }
            }

            // Spawn UI Cards
            foreach (var kvp in groupedItems)
            {
                var group = kvp.Value;
                VisualElement cardInstance = cardTemplate.Instantiate();
                
                Label nameLabel = cardInstance.Q<Label>("WoodName");
                Label hardnessLabel = cardInstance.Q<Label>("HardnessValue");
                Label moistureLabel = cardInstance.Q<Label>("MoistureValue");

                if (nameLabel != null) 
                {
                    string variantText = group.ReferenceItem.grainVariant != null && group.ReferenceItem.grainVariant.variantName != "Standard" 
                        ? $"{group.ReferenceItem.grainVariant.variantName} " 
                        : "";
                    string spaltedText = group.ReferenceItem.isSpalted ? "Spalted " : "";
                    nameLabel.text = $"{variantText}{spaltedText}{group.ReferenceItem.species.speciesName} ({group.ReferenceItem.stage}) x{group.Count}";
                }
                
                if (hardnessLabel != null) hardnessLabel.text = group.ReferenceItem.species.hardness.ToString();
                
                // We calculate a display moisture based on the stage since the species data doesn't track it directly
                if (moistureLabel != null) 
                {
                    float moisture = group.ReferenceItem.stage == ProcessingStage.KilnDried || group.ReferenceItem.stage == ProcessingStage.Surfaced ? 10f : 30f;
                    moistureLabel.text = $"{moisture}%";
                }

                _cardsContainer.Add(cardInstance);
            }
        }

        private class ItemGroup
        {
            public LumberItem ReferenceItem;
            public int Count;
        }
    }
}
