using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Sawmill.UI
{
    /// <summary>
    /// A dynamic center-screen overlay that pops up when an item is sold.
    /// Highlights unique grain patterns specially.
    /// </summary>
    public class CenterSaleOverlay : MonoBehaviour
    {
        public static CenterSaleOverlay Instance { get; private set; }

        private GameObject _panel;
        private RectTransform _panelRect;
        private CanvasGroup _canvasGroup;
        
        private TextMeshProUGUI _textSold;
        private TextMeshProUGUI _textDetail;
        private TextMeshProUGUI _textPrice;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
        }

        private void OnEnable()
        {
            GameManager.OnItemSold += HandleItemSold;
        }

        private void OnDisable()
        {
            GameManager.OnItemSold -= HandleItemSold;
        }

        private void BuildUI()
        {
            // Setup Canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            gameObject.AddComponent<GraphicRaycaster>();

            // Main Panel
            _panel = new GameObject("SaleOverlayPanel");
            _panel.transform.SetParent(transform, false);
            
            _panelRect = _panel.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(500f, 160f);
            
            // Background Image
            var img = _panel.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            
            _canvasGroup = _panel.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            
            _panel.SetActive(false);

            // Text Setup
            _textSold = CreateText("Header", "SOLD!", 40, new Vector2(0, 45), Color.white, FontStyles.Bold);
            _textDetail = CreateText("Detail", "Item", 24, new Vector2(0, 0), new Color(0.8f, 0.8f, 0.8f), FontStyles.Normal);
            _textPrice = CreateText("Price", "+$0", 36, new Vector2(0, -45), new Color(0.9f, 0.8f, 0.2f), FontStyles.Bold);
        }

        private TextMeshProUGUI CreateText(string name, string defaultText, float size, Vector2 anchoredPos, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_panel.transform, false);
            
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(0, 50);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = style;
            
            return tmp;
        }

        private void HandleItemSold(LumberItem item, float gold)
        {
            bool hasUniqueGrain = item.grainVariant != null && item.grainVariant.rarityTier >= 0;
            
            // Only highlight if grain variant is literally available or spalted
            bool highlight = (item.isGrainRevealed && item.grainVariant != null && item.grainVariant.rarityTier >= 1) || item.isSpalted;

            if (highlight)
            {
                _textSold.text = "UNIQUE SALE!";
                _textSold.color = new Color(0.8f, 0.5f, 0.9f); // Rare color popup
            }
            else
            {
                _textSold.text = "SOLD!";
                _textSold.color = Color.white;
            }

            _textDetail.text = item.DisplayName;
            _textPrice.text = $"+${gold:F2}";

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
            }
            _hideRoutine = StartCoroutine(AnimateOverlay());
        }

        private IEnumerator AnimateOverlay()
        {
            _panel.SetActive(true);
            
            // Pop in
            float fadeTime = 0.25f;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float normalized = t / fadeTime;
                _canvasGroup.alpha = normalized;
                
                // Slide up slightly to center
                _panelRect.anchoredPosition = Vector2.Lerp(new Vector2(0, -40), Vector2.zero, Mathf.SmoothStep(0, 1, normalized));
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _panelRect.anchoredPosition = Vector2.zero;

            // Wait
            yield return new WaitForSeconds(3.5f);

            // Fade out
            fadeTime = 0.5f;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float normalized = t / fadeTime;
                _canvasGroup.alpha = 1f - normalized;
                
                // Slide up while dissolving
                _panelRect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0, 40), Mathf.SmoothStep(0, 1, normalized));
                
                yield return null;
            }
            
            _panel.SetActive(false);
            _hideRoutine = null;
        }
    }
}
