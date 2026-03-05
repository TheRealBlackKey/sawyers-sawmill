using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SawyerSpeciesPickerController : MonoBehaviour
    {
        public static SawyerSpeciesPickerController Instance { get; private set; }

        public bool IsOpen => _root != null && _root.style.display.value == DisplayStyle.Flex;

        public event Action<WoodSpeciesData> OnSpeciesConfirmed;
        public event Action OnPickerCancelled;

        private UIDocument _uiDoc;
        private VisualElement _root;
        private VisualElement _speciesListContainer;
        private Button _btnConfirm;
        private Button _btnCancel;

        private WoodSpeciesData _selectedSpecies;
        private List<Button> _spawnedButtons = new List<Button>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _uiDoc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (_uiDoc == null) return;
            _root = _uiDoc.rootVisualElement;
            if (_root == null) return;

            _speciesListContainer = _root.Q<VisualElement>("SpeciesListContainer");
            _btnConfirm = _root.Q<Button>("BtnConfirm");
            _btnCancel = _root.Q<Button>("BtnCancel");

            if (_speciesListContainer == null)
            {
                Debug.LogError("SawyerSpeciesPickerController: Could not find 'SpeciesListContainer' in UXML! Please ensure SawyerSpeciesPickerController.uxml is assigned to the UIDocument in the Inspector.");
            }

            if (_btnConfirm != null) _btnConfirm.clicked += HandleConfirm;
            if (_btnCancel != null) _btnCancel.clicked += HandleCancel;

            // Hide by default on startup
            _root.style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            if (_btnConfirm != null) _btnConfirm.clicked -= HandleConfirm;
            if (_btnCancel != null) _btnCancel.clicked -= HandleCancel;
        }

        public void Show(List<WoodSpeciesData> unlockedSpecies, bool isFirstZone, float costPerCell, int totalCells)
        {
            if (_root == null) return;

            // Show UI
            _root.style.display = DisplayStyle.Flex;
            _selectedSpecies = null;
            
            if (_speciesListContainer == null)
            {
                Debug.LogError("SawyerSpeciesPickerController: _speciesListContainer is null! Cannot open picker. Assign UXML in inspector.");
                return;
            }

            // Clear old buttons
            _speciesListContainer.Clear();
            _spawnedButtons.Clear();

            if (_btnCancel != null)
            {
                // Cannot cancel first zone
                _btnCancel.style.display = isFirstZone ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (unlockedSpecies == null || unlockedSpecies.Count == 0)
            {
                Debug.LogWarning("SawyerSpeciesPickerController: No species provided!");
                return;
            }

            foreach (var species in unlockedSpecies)
            {
                Button btn = new Button();
                btn.text = isFirstZone ? $"{species.speciesName}  |  Free Starter Zone!" 
                                       : $"{species.speciesName}  |  {totalCells} Cells  |  Total Cost: ${costPerCell * totalCells:F0}";
                btn.AddToClassList("sawmill-button");
                btn.style.marginBottom = 8;
                btn.style.height = 48;
                
                // Store data securely
                btn.userData = species;

                btn.clicked += () => OnSpeciesButtonClicked(btn, species);
                
                _speciesListContainer.Add(btn);
                _spawnedButtons.Add(btn);
            }

            // Select first by default
            if (_spawnedButtons.Count > 0)
            {
                OnSpeciesButtonClicked(_spawnedButtons[0], unlockedSpecies[0]);
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        private void OnSpeciesButtonClicked(Button clickedBtn, WoodSpeciesData species)
        {
            _selectedSpecies = species;

            foreach (var b in _spawnedButtons)
            {
                // Reset styling (removing the active tint if any)
                b.style.backgroundColor = new StyleColor(new Color(0.25f, 0.18f, 0.08f, 1f));
            }

            // Highlight chosen
            clickedBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.25f, 1f));
        }

        private void HandleConfirm()
        {
            if (_selectedSpecies != null)
            {
                Hide();
                OnSpeciesConfirmed?.Invoke(_selectedSpecies);
            }
            else
            {
                Debug.LogWarning("SawyerSpeciesPickerController: Confirmed but no species selected!");
            }
        }

        private void HandleCancel()
        {
            Hide();
            OnPickerCancelled?.Invoke();
        }
    }
}
