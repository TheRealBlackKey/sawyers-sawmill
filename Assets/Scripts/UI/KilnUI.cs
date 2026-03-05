using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sawmill.UI
{
    /// <summary>
    /// A small pill-shaped world-space UI that floats above the Kiln to show progress.
    /// It is dynamically generated to avoid prefab dependencies.
    /// </summary>
    public class KilnUI : MonoBehaviour
    {
        private KilnBuilding _kiln;
        private Canvas _canvas;
        private RectTransform _fillBarRect;
        private Image _fillImage;
        private TextMeshProUGUI _statusText;

        private float _maxWidth = 100f;

        public void Initialize(KilnBuilding kiln)
        {
            _kiln = kiln;
            BuildUI();
        }

        private void BuildUI()
        {
            // 1. Root Canvas
            var canvasGO = new GameObject("KilnCanvas");
            canvasGO.transform.SetParent(transform, false);
            // Position slightly above the kiln building center
            canvasGO.transform.localPosition = new Vector3(0f, 3f, 0f); 

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 32000; // Force it to draw above all game objects

            // Make sure the canvas is crisp and scaled nicely for orthographic world space
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_maxWidth + 20f, 40f);
            canvasGO.transform.localScale = Vector3.one * 0.1f; // Scale down for world space

            // 2. Background Pill
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.10f, 0.08f, 0.6f); // Transparent dark box
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(_maxWidth, 24f);

            // 3. Foreground Fill Bar
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.color = new Color(0.2f, 0.7f, 0.2f, 0.5f); // Transparent green
            
            _fillBarRect = fillGO.GetComponent<RectTransform>();
            // Anchor left so it grows to the right
            _fillBarRect.anchorMin = new Vector2(0, 0.5f);
            _fillBarRect.anchorMax = new Vector2(0, 0.5f);
            _fillBarRect.pivot     = new Vector2(0, 0.5f);
            _fillBarRect.anchoredPosition = new Vector2(0f, 0f); // Exactly on the left edge
            _fillBarRect.sizeDelta = new Vector2(0f, 24f); // Start empty width at full height

            // 4. Text Label
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(canvasGO.transform, false);
            _statusText = textGO.AddComponent<TextMeshProUGUI>();
            _statusText.text = "0/4";
            _statusText.fontSize = 14f;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = Color.white;
            _statusText.textWrappingMode = TextWrappingModes.NoWrap;
            _statusText.fontStyle = FontStyles.Bold;
            // Add a slight outline for readability
            _statusText.outlineWidth = 0.15f;
            _statusText.outlineColor = new Color(0f,0f,0f,0.8f);

            var textRt = textGO.GetComponent<RectTransform>();
            textRt.sizeDelta = new Vector2(_maxWidth, 24f);
            textRt.anchoredPosition = new Vector2(0, 1f); // Nudge up to center vertically
            
            Debug.Log("[KilnUI] Successfully built KilnUI canvas!");
        }

        private void Update()
        {
            if (_kiln == null || _canvas == null) return;

            int activeCount = _kiln.GetSlotInfo().Count;
            int readyCount = InventoryManager.Instance?.Count(InventoryManager.InventoryZone.KilnOutput) ?? 0;

            // 1. Completely Empty
            if (activeCount == 0 && readyCount == 0)
            {
                _fillBarRect.sizeDelta = new Vector2(0f, 24f);
                _statusText.text = "Empty";
                _fillImage.color = new Color(0,0,0,0); // Hide the fill color
                return;
            }

            // 2. Not drying anything, but boards are waiting for Sawyer to pick them up
            if (activeCount == 0 && readyCount > 0)
            {
                _fillBarRect.sizeDelta = new Vector2(_maxWidth, 24f); // Full bar
                _statusText.text = $"{readyCount} Ready";
                _fillImage.color = new Color(0.2f, 0.4f, 0.8f, 0.5f); // Transparent blue
                return;
            }

            // 3. Actively drying boards
            if (activeCount > 0)
            {
                float avgProgress = _kiln.AverageProgress;
                float currentWidth = _maxWidth * avgProgress;
                _fillBarRect.sizeDelta = new Vector2(currentWidth, 24f);

                // If some boards are drying and some are waiting, show both
                if (readyCount > 0)
                {
                    _statusText.text = $"{activeCount}/{_kiln.slotCapacity} | {readyCount} Ready";
                }
                else
                {
                    _statusText.text = $"{activeCount}/{_kiln.slotCapacity}";
                }

                _fillImage.color = new Color(0.2f, 0.7f, 0.2f, 0.5f); // Static transparent green
            }
        }
    }
}
