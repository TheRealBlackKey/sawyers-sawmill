using UnityEngine;

namespace Sawmill.Core 
{
    /// <summary>
    /// Scales a single static background sprite to match the entire StripConfig width and height.
    /// Acts as the base layer behind all other tiles or buildings.
    /// </summary>
    public class StripBackground : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private StripConfig config = null;

        private void Start()
        {
            if (config == null) return;
            ScaleBackground();
        }

        private void ScaleBackground()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            // Position the background so it perfectly matches the logic grid
            // regardless of where the sprite's pivot is located.
            
            // Re-center before math so bounds are clean
            transform.position = Vector3.zero;
            transform.localScale = Vector3.one;

            float totalWidth = config.WorldWidth;
            float totalHeight = config.GridHeight;

            // Handle Sliced/Tiled vs Simple stretching
            if (sr.drawMode == SpriteDrawMode.Tiled || sr.drawMode == SpriteDrawMode.Sliced)
            {
                sr.size = new Vector2(totalWidth, totalHeight);
            }
            else
            {
                float spriteW = sr.sprite.bounds.size.x;
                float spriteH = sr.sprite.bounds.size.y;
                transform.localScale = new Vector3(totalWidth / spriteW, totalHeight / spriteH, 1f);
            }

            // Calculate normalized pivot [0..1] where 0.5 is perfectly centered
            float pivotNormX = sr.sprite.pivot.x / sr.sprite.rect.width;
            float pivotNormY = sr.sprite.pivot.y / sr.sprite.rect.height;

            // When placed at Vector3.zero, the bottom-left corner sits exactly at minus the pivot percentage of the total dimensions
            float bottomVisualEdge = -pivotNormY * totalHeight;
            float leftVisualEdge   = -pivotNormX * totalWidth;

            // The exact bottom-left constraints for the grid logic
            float targetBottom = config.MinWorldY;
            float targetLeft   = config.MinWorldX;

            // Shove the background layer so its bottom-left exactly hits the grid's bottom-left
            float offsetX = targetLeft - leftVisualEdge;
            float offsetY = targetBottom - bottomVisualEdge;

            transform.position = new Vector3(offsetX, offsetY, 0f);
        }
    }
}
