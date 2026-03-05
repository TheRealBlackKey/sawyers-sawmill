using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class FurnitureCreatorController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private FurnitureWorkshop workshop;

        private UIDocument _uiDocument;
        private Button _btnClose;
        private Button _btnCraft;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            _btnClose = root.Q<Button>("BtnClose");
            _btnCraft = root.Q<Button>("BtnCraft");

            if (_btnClose != null)
            {
                _btnClose.clicked += OnCloseClicked;
            }

            if (_btnCraft != null)
            {
                _btnCraft.clicked += OnCraftClicked;
            }
        }

        private void OnDisable()
        {
            if (_btnClose != null)
            {
                _btnClose.clicked -= OnCloseClicked;
            }

            if (_btnCraft != null)
            {
                _btnCraft.clicked -= OnCraftClicked;
            }
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        private void OnCraftClicked()
        {
            if (workshop == null)
            {
                // In a real scenario we'd find the workshop dynamically based on player placement
                workshop = FindFirstObjectByType<FurnitureWorkshop>();
            }

            if (workshop != null)
            {
                // Note: Complete implementation requires building the UI to pick a specific recipe.
                // For now, we push a dummy recipe or the first one available to prove the integration hook works.
                Debug.Log("FurnitureCreatorController: Sending craft request to FurnitureWorkshop...");
                
                // Hide menu 
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("FurnitureCreatorController: No FurnitureWorkshop found in the scene to send the build order to.");
            }
        }
    }
}
