using UnityEngine;
using UnityEngine.UIElements;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PlantingMenuController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private Button _btnClose;
        private Button _btnPlantZone;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                var go = GameObject.Find("PlantingMenu");
                if (go != null) _uiDocument = go.GetComponent<UIDocument>();
            }
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            _btnClose = root.Q<Button>("BtnClose");
            // Find the big action button from PlantingMenu.uxml
            _btnPlantZone = root.Q<Button>("BtnPlantZone");

            if (_btnClose != null)
            {
                _btnClose.clicked -= OnCloseClicked;
                _btnClose.clicked += OnCloseClicked;
                Debug.Log("PlantingMenuController: Close button found and bound.");
            }
            else
            {
                Debug.LogError("PlantingMenuController: Close button not found!");
            }

            if (_btnPlantZone != null)
            {
                _btnPlantZone.clicked -= OnPlantZoneClicked;
                _btnPlantZone.clicked += OnPlantZoneClicked;
                Debug.Log("PlantingMenuController: Plant Zone button found and bound.");
            }
            else
            {
                Debug.LogError("PlantingMenuController: Plant Zone button not found!");
            }
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

        private void OnPlantZoneClicked()
        {
            Debug.Log("PlantingMenuController: OnPlantZoneClicked fired!");

            // The menu is just a gateway to the zone painter mechanic
            if (ForestZonePainter.Instance != null)
            {
                Debug.Log("PlantingMenuController: Calling ForestZonePainter.BeginPainting()...");
                ForestZonePainter.Instance.BeginPainting();
                // Close the planting menu so they can see the screen to paint
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("PlantingMenuController: ForestZonePainter is missing from the scene!");
            }
        }
    }
}
