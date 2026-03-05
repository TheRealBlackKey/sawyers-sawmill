using UnityEngine;
using UnityEngine.UIElements;
using Sawmill.Production;

namespace Sawmill.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LumberjackUIController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _overlay;

        private Button _btnFeller;
        private Button _btnHauler;
        private Button _btnForester;
        private Button _btnClose;

        private LumberjackBuilding _activeBuilding;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;

            _overlay = root.Q<VisualElement>("Overlay");

            _btnFeller = root.Q<Button>("BtnFeller");
            _btnHauler = root.Q<Button>("BtnHauler");
            _btnForester = root.Q<Button>("BtnForester");
            _btnClose = root.Q<Button>("BtnClose");

            if (_btnFeller != null) _btnFeller.clicked += () => SetRole(WorkerRole.Feller);
            if (_btnHauler != null) _btnHauler.clicked += () => SetRole(WorkerRole.Hauler);
            if (_btnForester != null) _btnForester.clicked += () => SetRole(WorkerRole.Forester);
            if (_btnClose != null) _btnClose.clicked += CloseMenu;

            // Start hidden
            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            if (_btnFeller != null) _btnFeller.clicked -= () => SetRole(WorkerRole.Feller);
            if (_btnHauler != null) _btnHauler.clicked -= () => SetRole(WorkerRole.Hauler);
            if (_btnForester != null) _btnForester.clicked -= () => SetRole(WorkerRole.Forester);
            if (_btnClose != null) _btnClose.clicked -= CloseMenu;
        }

        public void OpenMenu(LumberjackBuilding building)
        {
            if (_overlay == null) return;

            _activeBuilding = building;
            _overlay.style.display = DisplayStyle.Flex;

            // Highlight current role button
            UpdateButtonHighlights();
        }

        public void CloseMenu()
        {
            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
                _activeBuilding = null;
            }
        }

        private void SetRole(WorkerRole newRole)
        {
            if (_activeBuilding != null && _activeBuilding.SpawnedWorker != null)
            {
                _activeBuilding.SpawnedWorker.SetRole(newRole);
                UpdateButtonHighlights();
                HUDManager.Instance?.ShowNotification("Role Assigned", $"Worker is now a {newRole}.", new Color(0.2f, 0.8f, 0.2f));
            }
        }

        private void UpdateButtonHighlights()
        {
            if (_activeBuilding == null || _activeBuilding.SpawnedWorker == null) return;

            WorkerRole current = _activeBuilding.SpawnedWorker.CurrentRole;

            // Reset colors
            ResetButtonColor(_btnFeller);
            ResetButtonColor(_btnHauler);
            ResetButtonColor(_btnForester);

            // Highlight active
            switch (current)
            {
                case WorkerRole.Feller: HighlightButton(_btnFeller); break;
                case WorkerRole.Hauler: HighlightButton(_btnHauler); break;
                case WorkerRole.Forester: HighlightButton(_btnForester); break;
            }
        }

        private void ResetButtonColor(Button btn)
        {
            if (btn != null) btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        }

        private void HighlightButton(Button btn)
        {
            if (btn != null) btn.style.backgroundColor = new StyleColor(new Color(0.1f, 0.5f, 0.1f));
        }
    }
}
