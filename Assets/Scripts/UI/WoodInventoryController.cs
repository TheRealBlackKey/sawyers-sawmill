using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    public class WoodInventoryController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement _speciesListContainer;
        private VisualElement _inventoryDetailsContainer;
        private Button _closeButton;

        // Current selection
        private WoodSpeciesData _selectedSpecies;

        // Data Structure: Species -> Stage -> VariantName -> Count
        private Dictionary<WoodSpeciesData, Dictionary<ProcessingStage, Dictionary<string, int>>> _inventoryData 
            = new Dictionary<WoodSpeciesData, Dictionary<ProcessingStage, Dictionary<string, int>>>();

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
            _speciesListContainer = _root.Q<VisualElement>("SpeciesListContainer");
            _inventoryDetailsContainer = _root.Q<VisualElement>("InventoryDetailsContainer");
            _closeButton = _root.Q<Button>("BtnClose");

            if (_closeButton != null)
            {
                _closeButton.clicked += OnCloseClicked;
            }

            // Populate on open
            RefreshData();
            
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
            if (zone != InventoryManager.InventoryZone.Sold)
            {
                RefreshData();
            }
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        public void RefreshData()
        {
            if (InventoryManager.Instance == null) return;
            if (_speciesListContainer == null || _inventoryDetailsContainer == null) return;

            // 1. Build the data dictionary
            _inventoryData.Clear();

            foreach (InventoryManager.InventoryZone zone in System.Enum.GetValues(typeof(InventoryManager.InventoryZone)))
            {
                if (zone == InventoryManager.InventoryZone.Sold) continue;
                
                var itemsInZone = InventoryManager.Instance.GetItems(zone);
                foreach (var item in itemsInZone)
                {
                    if (item.species == null) continue;
                    
                    var species = item.species;
                    var stage = item.stage;
                    
                    string variantStr = "Standard";
                    if (item.grainVariant != null && item.grainVariant.variantName != "Standard")
                        variantStr = item.grainVariant.variantName;
                    if (item.isSpalted)
                        variantStr = "Spalted " + variantStr;

                    if (!_inventoryData.ContainsKey(species))
                        _inventoryData[species] = new Dictionary<ProcessingStage, Dictionary<string, int>>();
                    
                    if (!_inventoryData[species].ContainsKey(stage))
                        _inventoryData[species][stage] = new Dictionary<string, int>();

                    if (!_inventoryData[species][stage].ContainsKey(variantStr))
                        _inventoryData[species][stage][variantStr] = 0;

                    _inventoryData[species][stage][variantStr]++;
                }
            }

            // 2. Build the Left Panel (Species Index)
            _speciesListContainer.Clear();
            
            // If the selected species is no longer in inventory, clear selection
            if (_selectedSpecies != null && !_inventoryData.ContainsKey(_selectedSpecies))
            {
                _selectedSpecies = null;
            }

            // Fallback selection if nothing is selected
            if (_selectedSpecies == null && _inventoryData.Count > 0)
            {
                var enumerator = _inventoryData.Keys.GetEnumerator();
                enumerator.MoveNext();
                _selectedSpecies = enumerator.Current;
            }

            foreach (var kvp in _inventoryData)
            {
                var species = kvp.Key;
                Button btn = new Button();
                btn.text = species.speciesName;
                btn.AddToClassList("sawmill-button");
                btn.style.marginBottom = 4;
                
                if (species == _selectedSpecies)
                {
                    // Highlight selected
                    btn.style.backgroundColor = new StyleColor(new Color(0.17f, 0.29f, 0.22f)); // Accent Green
                    btn.style.color = new StyleColor(Color.white);
                }

                btn.clicked += () => 
                {
                    _selectedSpecies = species;
                    RefreshData(); // Rebuild UI with new selection
                };

                _speciesListContainer.Add(btn);
            }

            // 3. Build the Right Panel (Inventory Details)
            _inventoryDetailsContainer.Clear();

            if (_selectedSpecies == null)
            {
                Label emptyLabel = new Label("No inventory found.");
                emptyLabel.AddToClassList("unity-label");
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                _inventoryDetailsContainer.Add(emptyLabel);
                return;
            }

            // Header (Species Name)
            Label headerLabel = new Label($"==== {_selectedSpecies.speciesName.ToUpper()} ====");
            headerLabel.AddToClassList("header-text");
            headerLabel.style.fontSize = 24;
            headerLabel.style.marginBottom = 16;
            _inventoryDetailsContainer.Add(headerLabel);

            var stagesObj = _inventoryData[_selectedSpecies];

            // Define a sorted rendering order for stages to make logical sense (Logs -> Sawn -> Dried -> Surfaced)
            ProcessingStage[] stageOrder = { 
                ProcessingStage.Log, 
                ProcessingStage.RoughSawn, 
                ProcessingStage.KilnDried, 
                ProcessingStage.Surfaced, 
                ProcessingStage.Finished 
            };

            foreach (var stage in stageOrder)
            {
                if (!stagesObj.ContainsKey(stage)) continue;

                var variants = stagesObj[stage];
                
                // Calculate total count for this stage across all variants
                int totalStageCount = 0;
                foreach (var count in variants.Values) totalStageCount += count;

                // Stage Header
                VisualElement stageRow = new VisualElement();
                stageRow.style.flexDirection = FlexDirection.Row;
                stageRow.style.justifyContent = Justify.SpaceBetween;
                stageRow.style.borderBottomWidth = 1;
                stageRow.style.borderBottomColor = new StyleColor(new Color(0.36f, 0.25f, 0.2f)); // Wood border
                stageRow.style.paddingBottom = 4;
                stageRow.style.marginTop = 8;
                stageRow.style.marginBottom = 4;

                Label stageLabel = new Label(stage.ToString());
                stageLabel.AddToClassList("unity-label");
                stageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                
                Label stageCountLabel = new Label(totalStageCount.ToString());
                stageCountLabel.AddToClassList("unity-label");
                stageCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                stageRow.Add(stageLabel);
                stageRow.Add(stageCountLabel);
                _inventoryDetailsContainer.Add(stageRow);

                // Variants List
                foreach (var variantKvp in variants)
                {
                    string variantName = variantKvp.Key;
                    int count = variantKvp.Value;

                    // Skip drawing a sub-row if it's just "Standard" and there are no other variants to differentiate from
                    if (variantName == "Standard" && variants.Count == 1) continue;

                    VisualElement variantRow = new VisualElement();
                    variantRow.style.flexDirection = FlexDirection.Row;
                    variantRow.style.justifyContent = Justify.SpaceBetween;
                    variantRow.style.paddingLeft = 16; // Indent
                    variantRow.style.marginBottom = 2;

                    Label variantLabel = new Label(variantName == "Standard" ? "Standard" : $"* {variantName}");
                    variantLabel.AddToClassList("unity-label");
                    variantLabel.style.fontSize = 14;
                    if (variantName != "Standard") variantLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    
                    Label variantCountLabel = new Label(count.ToString());
                    variantCountLabel.AddToClassList("unity-label");
                    variantCountLabel.style.fontSize = 14;

                    variantRow.Add(variantLabel);
                    variantRow.Add(variantCountLabel);
                    _inventoryDetailsContainer.Add(variantRow);
                }
            }
        }
    }
}
